using System.Net;
using FluentValidation;
using Majlis.Framework.Application.Dtos;
using Majlis.Framework.Application.Email;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Application.Services;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Framework.Domain.Security;
using Majlis.Identity.Domain.Constants;
using Majlis.Identity.Domain.Entities;
using Majlis.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;

namespace Majlis.Identity.Application.Invitations;

public sealed record InvitationDto(Guid Id, string Email, Guid? WorkspaceId, string? WorkspaceName, string? WorkspaceRole, string InvitedByDisplayName, InvitationStatus Status, DateTime ExpiresAt, DateTime CreationTime);

public sealed record InvitationIdDto(Guid Id);

/// <summary><c>AcceptedImmediately</c>: the email already belongs to a tenant user, who was added right away (no email sent).</summary>
public sealed record InvitationResultDto(Guid Id, bool AcceptedImmediately);

public sealed record FilterInvitationDto : BaseFilterRequestDto
{
    public Guid? WorkspaceId { get; init; }
}

public sealed record CreateInvitationDto
{
    public string Email { get; init; } = string.Empty;
    public Guid? WorkspaceId { get; init; }
    public string? WorkspaceName { get; init; }
    public string? WorkspaceRole { get; init; }
    public string Language { get; init; } = "ar";
}

public sealed class CreateInvitationValidator : AbstractValidator<CreateInvitationDto>
{
    private static readonly string[] Roles = ["Owner", "Admin", "Contributor", "Viewer"];

    public CreateInvitationValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("General:Fields:Required").EmailAddress().WithMessage("General:Fields:Invalid").MaximumLength(IdentityFieldDefinitions.MaxEmailLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Language).Must(l => l is "ar" or "en").WithMessage("General:Fields:Invalid");
        RuleFor(x => x.WorkspaceRole).Must(r => r is null || Roles.Contains(r)).WithMessage("General:Fields:Invalid");
        RuleFor(x => x.WorkspaceRole).NotEmpty().When(x => x.WorkspaceId is not null).WithMessage("General:Fields:Required");
        RuleFor(x => x.WorkspaceName).MaximumLength(IdentityFieldDefinitions.MaxGroupNameLength).WithMessage("General:Fields:MaxLength");
    }
}

public sealed class InvitationOptions
{
    /// <summary>Public base url of the auth pages (the BFF issuer); the accept link is <c>{PublicUrl}Account/Register?token=…</c>.</summary>
    public string PublicUrl { get; set; } = "http://localhost:7000/";
}

/// <summary>
/// Invitations by email (FR-IDN-005 / FR-WSP-002). An existing tenant user is added at once; anyone else gets a link
/// to the registration page, valid for seven days. Workspaces checks on acceptance that the inviter still manages the
/// workspace, so inviting into a workspace is only effective for its owners and admins. Routed as <c>/api/identity/invitations/*</c>.
/// </summary>
[Route("invitations")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public partial class InvitationsAppService(
    MajlisIdentityDbContext db,
    UserManager<MajlisUser> users,
    IEventPublisher publisher,
    IEmailSender email,
    IOptions<InvitationOptions> options,
    ICurrentUser currentUser,
    TimeProvider clock,
    ILogger<InvitationsAppService> logger,
    IValidator<CreateInvitationDto> createValidator) : ApplicationService
{
    [HttpPost("list")]
    [HasPermission(IdentityPermissions.InviteUsers)]
    public async Task<PagedResultDto<InvitationDto>> GetListAsync(FilterInvitationDto input, CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.GetRequiredTenantId();
        var now = clock.GetUtcNow().UtcDateTime;
        var query = db.Invitations.Where(i => i.TenantId == tenantId && i.Status == InvitationStatus.Pending && i.ExpiresAt > now
            && (input.WorkspaceId == null || i.WorkspaceId == input.WorkspaceId));
        var total = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(i => i.CreationTime).Skip(input.SkipCount).Take(Math.Clamp(input.MaxResultCount, 1, 100)).ToListAsync(cancellationToken);
        return new PagedResultDto<InvitationDto>(page.Select(ToDto).ToList(), total);
    }

    [HttpPost("")]
    [HasPermission(IdentityPermissions.InviteUsers)]
    public async Task<InvitationResultDto> CreateAsync(CreateInvitationDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(createValidator, input, cancellationToken);
        var tenantId = currentUser.GetRequiredTenantId();
        var inviterId = currentUser.GetRequiredId();
        var inviterName = currentUser.DisplayName ?? string.Empty;
        var address = Invitation.NormalizeEmail(input.Email);
        var now = clock.GetUtcNow().UtcDateTime;

        // Someone who already has an account in the tenant is added straight away (FR-WSP-002), no email.
        var normalized = users.NormalizeEmail(address);
        var existing = await users.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.NormalizedEmail == normalized, cancellationToken);
        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                throw new CustomValidationException(IdentityErrors.UserNotInTenant);
            }

            var accepted = new Invitation(Guid.NewGuid(), tenantId, address, Invitation.Hash(Invitation.NewToken()), inviterId, inviterName, input.WorkspaceId, Clean(input.WorkspaceName), input.WorkspaceRole, input.Language, now.AddDays(1));
            accepted.Accept(existing.Id, now);
            db.Invitations.Add(accepted);
            await db.SaveChangesAsync(cancellationToken);
            await publisher.PublishAsync(new UserInvitationAccepted(tenantId, accepted.Id, existing.Id, existing.DisplayName, input.WorkspaceId, input.WorkspaceRole, inviterId), cancellationToken);
            return new InvitationResultDto(accepted.Id, true);
        }

        if (await db.Invitations.AnyAsync(i => i.TenantId == tenantId && i.Email == address && i.WorkspaceId == input.WorkspaceId && i.Status == InvitationStatus.Pending && i.ExpiresAt > now, cancellationToken))
        {
            throw new CustomValidationException(IdentityErrors.InvitationPending);
        }

        var token = Invitation.NewToken();
        var invitation = new Invitation(Guid.NewGuid(), tenantId, address, Invitation.Hash(token), inviterId, inviterName, input.WorkspaceId, Clean(input.WorkspaceName), input.WorkspaceRole, input.Language, now.AddDays(IdentityFieldDefinitions.InvitationDays))
        {
            CreatorId = inviterId,
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);
        await SendAsync(invitation, token, cancellationToken);
        return new InvitationResultDto(invitation.Id, false);
    }

    /// <summary>The inviter or a tenant admin (anyone with <c>ManageGroups</c>) withdraws a pending invitation.</summary>
    [HttpPost("revoke")]
    [HasPermission(IdentityPermissions.InviteUsers)]
    public async Task RevokeAsync(InvitationIdDto input, CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.GetRequiredTenantId();
        var invitation = await db.Invitations.FirstOrDefaultAsync(i => i.Id == input.Id && i.TenantId == tenantId, cancellationToken) ?? throw new EntityNotFoundException();
        if (invitation.InvitedByUserId != currentUser.GetRequiredId() && !currentUser.Roles.Contains(MajlisRoles.TenantAdmin) && !currentUser.Roles.Contains(MajlisRoles.TenantOwner))
        {
            throw new ForbiddenException();
        }

        invitation.Revoke();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SendAsync(Invitation invitation, string token, CancellationToken cancellationToken)
    {
        var link = options.Value.PublicUrl.TrimEnd('/') + "/Account/Register?token=" + Uri.EscapeDataString(token);
        var ar = invitation.Language != "en";
        var where = invitation.WorkspaceName is null ? string.Empty : ar ? $" في مساحة العمل «{invitation.WorkspaceName}»" : $" in the workspace \"{invitation.WorkspaceName}\"";
        var subject = ar ? "مجلس: دعوة للانضمام" : "Majlis: you are invited";
        var text = ar
            ? $"دعاك {invitation.InvitedByDisplayName} للانضمام إلى مجلس{where}. تنتهي الدعوة خلال {IdentityFieldDefinitions.InvitationDays} أيام."
            : $"{invitation.InvitedByDisplayName} invited you to Majlis{where}. The invitation expires in {IdentityFieldDefinitions.InvitationDays} days.";
        var html = $"<div dir=\"{(ar ? "rtl" : "ltr")}\" style=\"font-family:Segoe UI,Tahoma,sans-serif;font-size:15px;line-height:1.6\"><p>{WebUtility.HtmlEncode(text)}</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">{(ar ? "قبول الدعوة" : "Accept the invitation")}</a></p></div>";
        try
        {
            await email.SendAsync(new EmailMessage(invitation.Email, null, subject, html, $"{text}\n{link}"), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.EmailFailed(logger, invitation.Id, ex); // the invitation stays; an admin can revoke and invite again
        }
    }

    private static InvitationDto ToDto(Invitation i) => new(i.Id, i.Email, i.WorkspaceId, i.WorkspaceName, i.WorkspaceRole, i.InvitedByDisplayName, i.Status, i.ExpiresAt, i.CreationTime);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static partial class Log
    {
        [LoggerMessage(EventId = 1201, Level = LogLevel.Warning, Message = "Invitation {InvitationId} email failed")]
        public static partial void EmailFailed(ILogger logger, Guid invitationId, Exception exception);
    }
}
