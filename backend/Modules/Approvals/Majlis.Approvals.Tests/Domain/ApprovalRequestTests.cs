using Majlis.Approvals.Domain.DomainServices;
using Majlis.Approvals.Domain.Entities;
using Majlis.Approvals.Domain.Enums;
using Majlis.Framework.Domain.Exceptions;

namespace Majlis.Approvals.Tests.Domain;

public class ApprovalRequestTests
{
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc);

    private static ApprovalRequest Pending(RiskLevel risk = RiskLevel.Low)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "create_task", """{"title":"x"}""", "إنشاء مهمة", null, risk, Requester, "سارة", Now.AddHours(24));

    [Fact]
    public void Approve_ShouldUseEditedArgs_WhenProvided()
    {
        var request = Pending();

        request.Approve(Guid.NewGuid(), "خالد", """{"title":"y"}""", "تعديل بسيط", Now);

        Assert.Equal(ApprovalStatus.Approved, request.Status);
        Assert.Equal("""{"title":"y"}""", request.EffectiveArgsJson);
        Assert.False(request.IsPending);
    }

    [Fact]
    public void Reject_ShouldThrow_WhenReasonMissing()
        => Assert.Throws<CustomValidationException>(() => Pending().Reject(Guid.NewGuid(), "خالد", " ", Now));

    [Fact]
    public void Approve_ShouldThrowConflict_WhenAlreadyDecided()
    {
        var request = Pending();
        request.Reject(Guid.NewGuid(), "خالد", "غير مناسب", Now);

        Assert.Throws<ConflictException>(() => request.Approve(Guid.NewGuid(), "خالد", null, null, Now));
    }

    [Fact]
    public void Expire_ShouldOnlyExpire_WhenPendingAndPastDue()
    {
        var request = Pending();

        Assert.False(request.Expire(Now.AddHours(1)));
        Assert.True(request.Expire(Now.AddHours(25)));
        Assert.Equal(ApprovalStatus.Expired, request.Status);
        Assert.False(request.MarkExecuted(Guid.NewGuid(), "x"));
    }

    [Fact]
    public void MarkExecuted_ShouldBeIdempotent_WhenReportedTwice()
    {
        var request = Pending();
        request.Approve(Guid.NewGuid(), "خالد", null, null, Now);
        var entityId = Guid.NewGuid();

        Assert.True(request.MarkExecuted(entityId, "المهمة"));
        Assert.False(request.MarkExecuted(Guid.NewGuid(), "أخرى"));
        Assert.Equal(entityId, request.ResultEntityId);
        Assert.Equal(ApprovalStatus.Executed, request.Status);
    }

    [Theory]
    [InlineData(RiskLevel.Low, "Contributor", false, true)]
    [InlineData(RiskLevel.Low, "Viewer", false, false)]
    [InlineData(RiskLevel.Low, "Contributor", true, true)]
    [InlineData(RiskLevel.Medium, "Owner", true, false)]
    [InlineData(RiskLevel.Medium, "Contributor", false, false)]
    [InlineData(RiskLevel.Medium, "Admin", false, true)]
    public void MayDecide_ShouldFollowDefaultPolicy(RiskLevel risk, string role, bool isRequester, bool expected)
    {
        var request = Pending(risk);
        var member = new WorkspaceMembership(request.TenantId, request.WorkspaceId, isRequester ? Requester : Guid.NewGuid(), "x", role);

        Assert.Equal(expected, ApprovalPolicyManager.MayDecide(request, member));
    }
}
