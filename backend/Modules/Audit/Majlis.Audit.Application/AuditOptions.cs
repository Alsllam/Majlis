namespace Majlis.Audit.Application;

public sealed class AuditOptions
{
    /// <summary>Largest CSV export in one request; narrow the filter above that.</summary>
    public int ExportMaxRows { get; set; } = 10000;
}
