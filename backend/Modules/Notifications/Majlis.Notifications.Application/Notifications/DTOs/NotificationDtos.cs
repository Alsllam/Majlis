using System.Text.Json;
using Majlis.Framework.Application.Dtos;

namespace Majlis.Notifications.Application.Notifications.DTOs;

public sealed record NotificationDto(Guid Id, string Type, JsonElement Params, string? Link, DateTime CreatedAt, DateTime? ReadAt);

public sealed record FilterNotificationDto : BaseFilterRequestDto
{
    public bool UnreadOnly { get; init; }
}

public sealed record UnreadCountDto(int Count);

public sealed record MarkReadDto
{
    public List<Guid> Ids { get; init; } = [];
}

public sealed record NotificationSettingsDto(bool EmailEnabled, List<string> MutedTypes);

public sealed record UpdateNotificationSettingsDto
{
    public bool EmailEnabled { get; init; } = true;
    public List<string> MutedTypes { get; init; } = [];
}
