using System.Security.Cryptography;
using System.Text;
using Majlis.Framework.Domain.Entities;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Identity.Domain.Constants;

namespace Majlis.Identity.Domain.Entities;

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Revoked = 2,
    Expired = 3,
}

/// <summary>
/// An invitation by email (FR-IDN-005), optionally into a workspace with a role (FR-WSP-002). Only the SHA-256 of
/// the token is stored; the link carries the token once, in the email.
/// </summary>
public class Invitation : CreationAuditedEntity<Guid>, IMultiTenant
{
    protected Invitation()
    {
    }

    public Invitation(Guid id, Guid tenantId, string email, string tokenHash, Guid invitedByUserId, string invitedByDisplayName, Guid? workspaceId, string? workspaceName, string? workspaceRole, string language, DateTime expiresAt)
        : base(id)
    {
        TenantId = tenantId;
        Email = email;
        TokenHash = tokenHash;
        InvitedByUserId = invitedByUserId;
        InvitedByDisplayName = invitedByDisplayName;
        WorkspaceId = workspaceId;
        WorkspaceName = workspaceName;
        WorkspaceRole = workspaceRole;
        Language = language;
        ExpiresAt = expiresAt;
    }

    public Guid TenantId { get; private set; }

    /// <summary>Normalized (lower case, trimmed).</summary>
    public string Email { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;
    public Guid InvitedByUserId { get; private set; }
    public string InvitedByDisplayName { get; private set; } = string.Empty;
    public Guid? WorkspaceId { get; private set; }
    public string? WorkspaceName { get; private set; }
    public string? WorkspaceRole { get; private set; }

    /// <summary>Language of the invitation email and the registration page (`ar` / `en`).</summary>
    public string Language { get; private set; } = "ar";

    public DateTime ExpiresAt { get; private set; }
    public InvitationStatus Status { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public Guid? AcceptedUserId { get; private set; }

    public bool IsOpen(DateTime now) => Status == InvitationStatus.Pending && ExpiresAt > now;

    public void Accept(Guid userId, DateTime now)
    {
        if (!IsOpen(now))
        {
            throw new CustomValidationException(IdentityErrors.InvitationNotOpen);
        }

        Status = InvitationStatus.Accepted;
        AcceptedAt = now;
        AcceptedUserId = userId;
    }

    public void Revoke()
    {
        if (Status == InvitationStatus.Pending)
        {
            Status = InvitationStatus.Revoked;
        }
    }

    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
