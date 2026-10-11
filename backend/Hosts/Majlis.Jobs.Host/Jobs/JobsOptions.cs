namespace Majlis.Jobs.Host.Jobs;

public sealed class JobsOptions
{
    /// <summary>Hangfire tables live in their own schema, apart from every module.</summary>
    public string Schema { get; set; } = "jobs";

    /// <summary>Per-job cron overrides keyed by job id (UTC).</summary>
    public Dictionary<string, string> Crons { get; set; } = [];

    /// <summary>The dashboard at <c>/hangfire</c> is served only when true (development: local requests only).</summary>
    public bool Dashboard { get; set; }
}
