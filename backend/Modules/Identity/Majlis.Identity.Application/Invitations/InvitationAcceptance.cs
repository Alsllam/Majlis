using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Identity.Domain.Constants;
using Majlis.Identity.Domain.Entities;
using Majlis.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Majlis.Identity.Application.Invitations;

/// <summary>The registration page's back end: find an open invitation by its token, create the account, accept.</summary>
public sealed class InvitationAcceptance(MajlisIdentityDbContext db, UserManager<MajlisUser> users, IEventPublisher publisher, TimeProvider clock)
{
    public async Task<Invitation?> FindOpenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = Invitation.Hash(token);
        var invitation = await db.Invitations.FirstOrDefaultAsync(i => i.TokenHash == hash, cancellationToken);
        return invitation?.IsOpen(clock.GetUtcNow().UtcDateTime) == true ? invitation : null;
    }

    /// <summary>Creates the user as a tenant Member, accepts the invitation and tells Workspaces. Returns the new user.</summary>
    public async Task<MajlisUser> AcceptAsync(string token, string displayName, string password, CancellationToken cancellationToken)
    {
        var invitation = await FindOpenAsync(token, cancellationToken) ?? throw new CustomValidationException(IdentityErrors.InvitationNotOpen);
        var user = new MajlisUser
        {
            Id = Guid.NewGuid(),
            UserName = invitation.Email,
            Email = invitation.Email,
            EmailConfirmed = true,
            DisplayName = displayName.Trim(),
            PreferredLanguage = invitation.Language,
            TenantId = invitation.TenantId,
        };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            throw new CustomValidationException(IdentityErrors.RegistrationFailed, string.Join("; ", created.Errors.Select(e => e.Description)));
        }

        await users.AddToRoleAsync(user, MajlisRoles.Member);
        invitation.Accept(user.Id, clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);
        await publisher.PublishAsync(new UserInvitationAccepted(invitation.TenantId, invitation.Id, user.Id, user.DisplayName, invitation.WorkspaceId, invitation.WorkspaceRole, invitation.InvitedByUserId), cancellationToken);
        return user;
    }
}
