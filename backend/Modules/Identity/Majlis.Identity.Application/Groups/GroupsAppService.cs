using FluentValidation;
using Majlis.Framework.Application.Dtos;
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
using OpenIddict.Validation.AspNetCore;

namespace Majlis.Identity.Application.Groups;

public sealed record GroupMemberDto(Guid UserId, string DisplayName, DateTime JoinedAt);

public sealed record GroupDto(Guid Id, string Name, string? Description, IReadOnlyList<GroupMemberDto> Members, DateTime CreationTime);

public sealed record GroupListDto(Guid Id, string Name, string? Description, int MemberCount, DateTime CreationTime);

public sealed record GroupIdDto(Guid Id);

public sealed record FilterGroupDto : BaseFilterRequestDto;

public sealed record CreateGroupDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed record UpdateGroupDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed record SetGroupMemberDto
{
    public Guid GroupId { get; init; }
    public Guid UserId { get; init; }
}

public sealed class CreateGroupValidator : AbstractValidator<CreateGroupDto>
{
    public CreateGroupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("General:Fields:Required").MaximumLength(IdentityFieldDefinitions.MaxGroupNameLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Description).MaximumLength(IdentityFieldDefinitions.MaxDescriptionLength).WithMessage("General:Fields:MaxLength");
    }
}

public sealed class UpdateGroupValidator : AbstractValidator<UpdateGroupDto>
{
    public UpdateGroupValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("General:Fields:Required");
        RuleFor(x => x.Name).NotEmpty().WithMessage("General:Fields:Required").MaximumLength(IdentityFieldDefinitions.MaxGroupNameLength).WithMessage("General:Fields:MaxLength");
        RuleFor(x => x.Description).MaximumLength(IdentityFieldDefinitions.MaxDescriptionLength).WithMessage("General:Fields:MaxLength");
    }
}

/// <summary>
/// Tenant groups (FR-IDN-005). Tenant admins manage them; every member may look them up for pickers. Changes are
/// published for Workspaces, which expands group grants into per-user memberships. Routed as <c>/api/identity/groups/*</c>.
/// </summary>
[Route("groups")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public class GroupsAppService(
    MajlisIdentityDbContext db,
    UserManager<MajlisUser> users,
    IEventPublisher publisher,
    ICurrentUser currentUser,
    IValidator<CreateGroupDto> createValidator,
    IValidator<UpdateGroupDto> updateValidator) : ApplicationService
{
    [HttpPost("list")]
    [HasPermission(IdentityPermissions.ViewUsers)]
    public async Task<PagedResultDto<GroupListDto>> GetListAsync(FilterGroupDto input, CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.GetRequiredTenantId();
        var filter = input.FilterText?.Trim();
        var query = db.Groups.Where(g => g.TenantId == tenantId && (string.IsNullOrEmpty(filter) || g.Name.Contains(filter)));
        var total = await query.CountAsync(cancellationToken);
        var page = await query.OrderBy(g => g.Name).Skip(input.SkipCount).Take(Math.Clamp(input.MaxResultCount, 1, 100))
            .Select(g => new GroupListDto(g.Id, g.Name, g.Description, g.Members.Count, g.CreationTime))
            .ToListAsync(cancellationToken);
        return new PagedResultDto<GroupListDto>(page, total);
    }

    [HttpPost("getbyid")]
    [HasPermission(IdentityPermissions.ViewUsers)]
    public async Task<GroupDto> GetAsync(GroupIdDto input, CancellationToken cancellationToken = default)
        => ToDto(await LoadAsync(input.Id, cancellationToken));

    [HttpPost("")]
    [HasPermission(IdentityPermissions.ManageGroups)]
    public async Task<Guid> CreateAsync(CreateGroupDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(createValidator, input, cancellationToken);
        var tenantId = currentUser.GetRequiredTenantId();
        await EnsureNameFreeAsync(tenantId, input.Name.Trim(), null, cancellationToken);
        var group = new Group(Guid.NewGuid(), tenantId, input.Name.Trim(), Clean(input.Description)) { CreatorId = currentUser.GetRequiredId() };
        db.Groups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        return group.Id;
    }

    [HttpPut("")]
    [HasPermission(IdentityPermissions.ManageGroups)]
    public async Task UpdateAsync(UpdateGroupDto input, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(updateValidator, input, cancellationToken);
        var group = await LoadAsync(input.Id, cancellationToken);
        await EnsureNameFreeAsync(group.TenantId, input.Name.Trim(), group.Id, cancellationToken);
        var renamed = group.Name != input.Name.Trim();
        group.Update(input.Name.Trim(), Clean(input.Description));
        await db.SaveChangesAsync(cancellationToken);
        if (renamed)
        {
            await publisher.PublishAsync(new GroupRenamed(group.TenantId, group.Id, group.Name), cancellationToken);
        }
    }

    [HttpDelete("")]
    [HasPermission(IdentityPermissions.ManageGroups)]
    public async Task DeleteAsync(GroupIdDto input, CancellationToken cancellationToken = default)
    {
        var group = await LoadAsync(input.Id, cancellationToken);
        db.Groups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
        await publisher.PublishAsync(new GroupDeleted(group.TenantId, group.Id), cancellationToken);
    }

    /// <summary>Adds a tenant user to the group (a repeat only refreshes the name snapshot).</summary>
    [HttpPost("members")]
    [HasPermission(IdentityPermissions.ManageGroups)]
    public async Task<GroupDto> SetMemberAsync(SetGroupMemberDto input, CancellationToken cancellationToken = default)
    {
        var group = await LoadAsync(input.GroupId, cancellationToken);
        var user = await users.Users.FirstOrDefaultAsync(u => u.Id == input.UserId && u.TenantId == group.TenantId && u.IsActive, cancellationToken)
            ?? throw new CustomValidationException(IdentityErrors.UserNotInTenant);
        var added = group.AddMember(user.Id, user.DisplayName);
        await db.SaveChangesAsync(cancellationToken);
        if (added)
        {
            await publisher.PublishAsync(new GroupMemberAdded(group.TenantId, group.Id, group.Name, user.Id, user.DisplayName), cancellationToken);
        }

        return ToDto(group);
    }

    [HttpDelete("members")]
    [HasPermission(IdentityPermissions.ManageGroups)]
    public async Task<GroupDto> RemoveMemberAsync(SetGroupMemberDto input, CancellationToken cancellationToken = default)
    {
        var group = await LoadAsync(input.GroupId, cancellationToken);
        if (group.RemoveMember(input.UserId))
        {
            await db.SaveChangesAsync(cancellationToken);
            await publisher.PublishAsync(new GroupMemberRemoved(group.TenantId, group.Id, input.UserId), cancellationToken);
        }

        return ToDto(group);
    }

    private async Task<Group> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetRequiredTenantId();
        return await db.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id && g.TenantId == tenantId, cancellationToken)
            ?? throw new EntityNotFoundException();
    }

    private async Task EnsureNameFreeAsync(Guid tenantId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await db.Groups.AnyAsync(g => g.TenantId == tenantId && g.Name == name && g.Id != exceptId, cancellationToken))
        {
            throw new CustomValidationException(IdentityErrors.GroupNameTaken);
        }
    }

    private static GroupDto ToDto(Group group) => new(group.Id, group.Name, group.Description,
        group.Members.OrderBy(m => m.DisplayName).Select(m => new GroupMemberDto(m.UserId, m.DisplayName, m.CreationTime)).ToList(), group.CreationTime);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
