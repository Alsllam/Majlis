namespace Majlis.Framework.Application.Dtos;

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int TotalCount);

public sealed record EntityIdDto<TKey>(TKey Id);

public sealed record LookupDto<TKey>(TKey Id, string DisplayName);

public enum ActiveFilter
{
    All,
    Active,
    InActive,
}

/// <summary>Base filter for list endpoints. Reads use POST with a body so filters never land in URLs.</summary>
public abstract record BaseFilterRequestDto
{
    public string? FilterText { get; init; }
    public int SkipCount { get; init; }
    public int MaxResultCount { get; init; } = 20;
    public string? Sorting { get; init; }
    public ActiveFilter? ActiveFilter { get; init; }
}
