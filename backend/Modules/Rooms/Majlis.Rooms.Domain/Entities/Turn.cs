using Majlis.Framework.Domain.Entities;
using Majlis.Framework.Domain.Exceptions;
using Majlis.Rooms.Domain.Constants;
using Majlis.Rooms.Domain.Enums;

namespace Majlis.Rooms.Domain.Entities;

/// <summary>One instruction to the agent and its answer.</summary>
public class Turn : CreationAuditedEntity<Guid>, IMultiTenant
{
    protected Turn()
    {
    }

    public Turn(Guid id, Guid tenantId, Guid sessionId, SessionActor instructedBy, string instruction, string? language, Guid clientRequestId)
        : base(id)
    {
        TenantId = tenantId;
        SessionId = sessionId;
        InstructedByUserId = instructedBy.UserId;
        InstructedByDisplayName = instructedBy.DisplayName;
        Instruction = instruction;
        Language = language;
        ClientRequestId = clientRequestId;
        Status = TurnStatus.Streaming;
    }

    public Guid TenantId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid InstructedByUserId { get; private set; }
    public string InstructedByDisplayName { get; private set; } = string.Empty;
    public string Instruction { get; private set; } = string.Empty;
    public string? Language { get; private set; }

    /// <summary>Makes <c>instruct</c> idempotent: the same client request returns the same turn.</summary>
    public Guid ClientRequestId { get; private set; }

    public TurnStatus Status { get; private set; }
    public bool StopRequested { get; private set; }
    public string? AnswerText { get; private set; }

    /// <summary>Citations as JSON: <c>[{ label, documentId, versionId, title, page, passage }]</c>.</summary>
    public string? CitationsJson { get; private set; }

    public string? FailureReasonKey { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public int CachedTokens { get; private set; }
    public DateTime? FinishedAt { get; private set; }

    public bool IsRunning => Status == TurnStatus.Streaming;

    public void RequestStop()
    {
        if (!IsRunning)
        {
            throw new ConflictException(RoomsErrors.TurnNotRunning);
        }

        StopRequested = true;
    }

    /// <summary>Returns false when the turn already finished (duplicate message).</summary>
    public bool Complete(string text, string citationsJson, int inputTokens, int outputTokens, int cachedTokens, DateTime at)
    {
        if (!IsRunning)
        {
            return false;
        }

        Status = TurnStatus.Completed;
        AnswerText = text;
        CitationsJson = citationsJson;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CachedTokens = cachedTokens;
        FinishedAt = at;
        return true;
    }

    public bool Stop(string partialText, DateTime at)
    {
        if (!IsRunning)
        {
            return false;
        }

        Status = TurnStatus.Stopped;
        AnswerText = partialText;
        FinishedAt = at;
        return true;
    }

    public bool Fail(string reasonKey, string? partialText, DateTime at)
    {
        if (!IsRunning)
        {
            return false;
        }

        Status = TurnStatus.Failed;
        FailureReasonKey = reasonKey;
        AnswerText = partialText;
        FinishedAt = at;
        return true;
    }
}
