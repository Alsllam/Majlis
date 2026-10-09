using Refit;

namespace Majlis.Rooms.Application.Ai;

/// <summary>ai-service internal API (not routed by the BFF). Called with the driver's own access token.</summary>
public interface IAiTurnClient
{
    [Post("/internal/sessions/{sessionId}/turns")]
    Task StartTurnAsync(Guid sessionId, [Body] StartAiTurnRequest request, [Header("Authorization")] string authorization, CancellationToken cancellationToken = default);
}

public sealed record StartAiTurnRequest(
    Guid TurnId,
    Guid TenantId,
    Guid WorkspaceId,
    Guid RoomId,
    string Instruction,
    string Language,
    AiActor InstructedBy,
    IReadOnlyList<AiHistoryTurn> History);

public sealed record AiActor(Guid UserId, string DisplayName);

/// <summary>A previous completed turn, oldest first, for conversation context.</summary>
public sealed record AiHistoryTurn(string Instruction, string Answer, string InstructedBy);
