using Majlis.Approvals.Domain.Constants;
using Majlis.Approvals.Domain.Entities;
using Majlis.Approvals.Domain.Enums;
using Majlis.Framework.Domain.Exceptions;

namespace Majlis.Approvals.Domain.DomainServices;

/// <summary>
/// Who may decide (FR-APR-005). The MVP ships the default policy only; per-workspace policies replace it later:
/// low risk → the requester (driver) or any contributor; medium and high → an owner or admin who is not the requester.
/// </summary>
public static class ApprovalPolicyManager
{
    public static void EnsureMayDecide(ApprovalRequest request, WorkspaceMembership approver)
    {
        if (request.Risk == RiskLevel.Low)
        {
            if (!approver.CanContribute)
            {
                throw new ForbiddenException(ApprovalsErrors.ApproverRoleRequired);
            }

            return;
        }

        if (approver.UserId == request.RequestedByUserId)
        {
            throw new ForbiddenException(ApprovalsErrors.SelfApprovalNotAllowed);
        }

        if (!approver.CanManage)
        {
            throw new ForbiddenException(ApprovalsErrors.ApproverRoleRequired);
        }
    }

    /// <summary>For lists: can this member decide this pending request?</summary>
    public static bool MayDecide(ApprovalRequest request, WorkspaceMembership approver)
    {
        try
        {
            EnsureMayDecide(request, approver);
            return request.IsPending;
        }
        catch (ForbiddenException)
        {
            return false;
        }
    }
}
