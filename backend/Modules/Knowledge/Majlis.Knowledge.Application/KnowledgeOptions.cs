namespace Majlis.Knowledge.Application;

public sealed class KnowledgeOptions
{
    /// <summary>FR-KNW-001: 100 MB by default.</summary>
    public int MaxFileSizeMb { get; set; } = 100;

    public int UploadUrlMinutes { get; set; } = 15;

    public int DownloadUrlMinutes { get; set; } = 10;
}
