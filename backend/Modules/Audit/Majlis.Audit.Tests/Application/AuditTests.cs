using System.Reflection;
using FakeItEasy;
using Majlis.Audit.Application;
using Majlis.Audit.Application.Entries;
using Majlis.Audit.Application.Entries.DTOs;
using Majlis.Audit.Application.EventHandlers;
using Majlis.Audit.Application.Recording;
using Majlis.Audit.Application.Verification;
using Majlis.Audit.Domain.Constants;
using Majlis.Audit.Domain.Entities;
using Majlis.Audit.EntityFrameworkCore;
using Majlis.Framework.Application.Security;
using Majlis.Framework.Domain.Events;
using Majlis.Framework.Domain.Security;
using Majlis.Framework.EntityFrameworkCore.Extensions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Majlis.Audit.Tests.Application;

public sealed class TestCurrentUser : ICurrentUser
{
    public Guid? Id { get; set; }
    public Guid? TenantId { get; set; }
    public string? DisplayName { get; set; }
    public bool IsAuthenticated => Id is not null;
    public IReadOnlyCollection<string> Roles { get; set; } = [];
    public Guid GetRequiredId() => Id ?? throw new InvalidOperationException();
    public Guid GetRequiredTenantId() => TenantId ?? throw new InvalidOperationException();
}

public class AuditTests : IDisposable
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OtherTenant = Guid.NewGuid();
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly (Guid Id, string Name) Sara = (Guid.NewGuid(), "سارة");
    private static readonly (Guid Id, string Name) Khalid = (Guid.NewGuid(), "خالد");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly ServiceProvider _provider;

    public AuditTests()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());
        services.AddDbContext<AuditDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddMajlisRepositories<AuditDbContext>();
        services.AddSingleton(A.Fake<IEventPublisher>());
        services.AddSingleton(Options.Create(new AuditOptions { ExportMaxRows = 100 }));
        services.AddScoped<AuditRecorder>();
        services.AddScoped<ChainVerifier>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScoped<AuditAppService>();
        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        GC.SuppressFinalize(this);
    }

    private IServiceScope Scope(Guid tenantId, (Guid Id, string Name) user)
    {
        var scope = _provider.CreateScope();
        var current = scope.ServiceProvider.GetRequiredService<TestCurrentUser>();
        current.Id = user.Id;
        current.TenantId = tenantId;
        current.DisplayName = user.Name;
        return scope;
    }

    private async Task RecordControlAsync(long seq, string type, Guid tenantId = default)
    {
        using var scope = Scope(tenantId == default ? TenantId : tenantId, Sara);
        var recorder = scope.ServiceProvider.GetRequiredService<AuditRecorder>();
        var m = new SessionEventAppended(tenantId == default ? TenantId : tenantId, WorkspaceId, Guid.NewGuid(), SessionId, seq, type, null, DateTime.UtcNow, "user", Sara.Id, Sara.Name, """{"kind":"claim","from":null,"to":{"userId":"x","displayName":"سارة"},"epoch":2}""");
        await SessionEventAppendedHandler.Handle(m, recorder, Ct);
    }

    [Fact]
    public void Endpoints_ShouldRequireAPermission_WhenExposedToUsers()
    {
        var endpoints = typeof(AuditAppService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any()).ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, m => Assert.NotNull(m.GetCustomAttribute<HasPermissionAttribute>()));
    }

    [Fact]
    public async Task Recorder_ShouldChainEntriesPerTenant_SkipDuplicates_AndIgnoreTurns()
    {
        await RecordControlAsync(1, "control.changed");
        await RecordControlAsync(1, "control.changed"); // redelivered
        await RecordControlAsync(2, "turn.started"); // content, not audit
        await RecordControlAsync(3, "handoff.offered");
        await RecordControlAsync(1, "session.started", OtherTenant);

        using var scope = Scope(TenantId, Sara);
        var service = scope.ServiceProvider.GetRequiredService<AuditAppService>();
        var page = await service.GetListAsync(new FilterAuditDto { SkipCount = 0, MaxResultCount = 10 }, Ct);
        Assert.Equal([2L, 1L], page.Items.Select(e => e.Seq));
        Assert.Equal(["handoff.offered", "control.changed"], page.Items.Select(e => e.Action));
        Assert.Equal(AuditCategories.Session, page.Items[0].Category);
        Assert.Equal("سارة", page.Items[1].Details.GetProperty("to").GetString());

        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var entries = await db.Entries.AsNoTracking().Where(e => e.TenantId == TenantId).OrderBy(e => e.Seq).ToListAsync(Ct);
        Assert.Equal(AuditChain.GenesisHash, entries[0].PreviousHash);
        Assert.Equal(entries[0].Hash, entries[1].PreviousHash);
        var verification = await service.VerifyAsync(Ct);
        Assert.True(verification.Valid);
        Assert.Equal(2, verification.Entries);
        Assert.Equal(entries[1].Hash, verification.HeadHash);
    }

    [Fact]
    public async Task Verify_ShouldReportTheFirstTamperedEntry()
    {
        await RecordControlAsync(1, "control.changed");
        await RecordControlAsync(2, "handoff.offered");
        await RecordControlAsync(3, "session.ended");

        using (var scope = Scope(TenantId, Sara))
        {
            // Someone edits a row behind the module's back.
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var second = await db.Entries.SingleAsync(e => e.Seq == 2, Ct);
            db.Entry(second).Property(e => e.Action).CurrentValue = "control.changed";
            await db.SaveChangesAsync(Ct);
        }

        using var check = Scope(TenantId, Sara);
        var verification = await check.ServiceProvider.GetRequiredService<AuditAppService>().VerifyAsync(Ct);
        Assert.False(verification.Valid);
        Assert.Equal(2, verification.BrokenAtSeq);
    }

    [Fact]
    public async Task NightlyVerification_ShouldCheckEveryTenant_AndRecordTheOutcomeOnTheChain()
    {
        await RecordControlAsync(1, "control.changed");
        await RecordControlAsync(2, "session.ended");
        await RecordControlAsync(1, "control.changed", OtherTenant);
        using (var scope = Scope(OtherTenant, Sara))
        {
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var entry = await db.Entries.IgnoreQueryFilters().SingleAsync(e => e.TenantId == OtherTenant, Ct);
            db.Entry(entry).Property(e => e.Action).CurrentValue = "handoff.offered";
            await db.SaveChangesAsync(Ct);
        }

        using var check = Scope(TenantId, Sara);
        var broken = await AuditChainVerificationRequestedHandler.Handle(new AuditChainVerificationRequested(DateTime.UtcNow), check.ServiceProvider.GetRequiredService<ChainVerifier>(), Ct).ContinueWith(_ => 0, Ct, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
        Assert.Equal(0, broken);
        var chains = await check.ServiceProvider.GetRequiredService<AuditDbContext>().Chains.IgnoreQueryFilters().ToListAsync(Ct);
        var good = chains.Single(c => c.TenantId == TenantId);
        var bad = chains.Single(c => c.TenantId == OtherTenant);
        Assert.Equal((true, (long?)null), (good.LastVerificationValid, good.LastBrokenSeq));
        Assert.Equal((false, (long?)1), (bad.LastVerificationValid, bad.LastBrokenSeq));
        Assert.NotNull(good.LastVerifiedAt);
    }

    [Fact]
    public async Task Handlers_ShouldRecordApprovalsToolsDocumentsAndMembers_ThenExportThemAsCsv()
    {
        var requestId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        using (var scope = Scope(TenantId, Sara))
        {
            var recorder = scope.ServiceProvider.GetRequiredService<AuditRecorder>();
            var clock = TimeProvider.System;
            await ApprovalRequestedHandler.Handle(new ApprovalRequested(TenantId, WorkspaceId, Guid.NewGuid(), SessionId, null, requestId, "create_task", "إنشاء مهمة", null, "Low", "{}", Khalid.Id, Khalid.Name, DateTime.UtcNow), recorder, clock, Ct);
            await ApprovalDecidedHandler.Handle(new ApprovalDecided(TenantId, SessionId, requestId, "create_task", "Approved", Sara.Id, Sara.Name, "ok", null), recorder, clock, Ct);
            await ApprovalExecutedHandler.Handle(new ApprovalExecuted(TenantId, SessionId, requestId, "create_task", true, "x", Guid.NewGuid(), null), recorder, clock, Ct);
            await DocumentAccessedHandler.Handle(new DocumentAccessed(TenantId, WorkspaceId, documentId, Guid.NewGuid(), Khalid.Id, Khalid.Name, "download", DateTime.UtcNow), recorder, Ct);
            await MemberAddedHandler.Handle(new Majlis.Audit.Domain.Events.MemberAdded(TenantId, WorkspaceId, Khalid.Id, Khalid.Name, "Contributor"), recorder, clock, Ct);
            await TaskAssignedHandler.Handle(new TaskAssigned(TenantId, WorkspaceId, null, Guid.NewGuid(), "مهمة, مع فاصلة", Khalid.Id, Sara.Id, Sara.Name), recorder, clock, Ct);
        }

        using var check = Scope(TenantId, Sara);
        var service = check.ServiceProvider.GetRequiredService<AuditAppService>();
        var all = await service.GetListAsync(new FilterAuditDto { SkipCount = 0, MaxResultCount = 20 }, Ct);
        Assert.Equal(["task.assigned", "member.added", "document.download", "tool.executed", "approval.approved", "approval.requested"], all.Items.Select(e => e.Action));
        var approvals = await service.GetListAsync(new FilterAuditDto { Category = AuditCategories.Approval, SkipCount = 0, MaxResultCount = 20 }, Ct);
        Assert.Equal(2, approvals.TotalCount);
        var byKhalid = await service.GetListAsync(new FilterAuditDto { ActorUserId = Khalid.Id, SkipCount = 0, MaxResultCount = 20 }, Ct);
        Assert.Equal(["document.download", "approval.requested"], byKhalid.Items.Select(e => e.Action));

        var csv = await service.ExportAsync(new FilterAuditDto { SkipCount = 0, MaxResultCount = 20 }, Ct);
        var text = System.Text.Encoding.UTF8.GetString(csv.FileContents);
        Assert.StartsWith("﻿seq,occurredAt,category", text, StringComparison.Ordinal);
        Assert.Contains("\"{\"\"title\"\":\"\"مهمة, مع فاصلة\"\"", text, StringComparison.Ordinal);
        Assert.Equal(7, text.TrimEnd().Split('\n').Length);
        Assert.True((await service.VerifyAsync(Ct)).Valid);
    }
}
