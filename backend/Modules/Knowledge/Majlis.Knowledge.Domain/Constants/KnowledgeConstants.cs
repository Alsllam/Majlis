namespace Majlis.Knowledge.Domain.Constants;

/// <summary>Permission names; identical strings are used by the web and mobile apps.</summary>
public static class KnowledgePermissions
{
    public const string ViewDocument = "Permissions.Knowledge.ViewDocument";
    public const string UploadDocument = "Permissions.Knowledge.UploadDocument";
    public const string DeleteDocument = "Permissions.Knowledge.DeleteDocument";
}

/// <summary>Localization keys for business errors in this module.</summary>
public static class KnowledgeErrors
{
    public const string NotWorkspaceMember = "Knowledge:Document:NotWorkspaceMember";
    public const string FileTooLarge = "Knowledge:Document:FileTooLarge";
    public const string TypeNotSupported = "Knowledge:Document:TypeNotSupported";
    public const string FileNotUploaded = "Knowledge:Document:FileNotUploaded";
    public const string VersionNotPending = "Knowledge:Document:VersionNotPending";
    public const string Deleted = "Knowledge:Document:Deleted";
}

public static class KnowledgeFieldDefinitions
{
    public const int MaxTitleLength = Framework.Domain.Localization.FieldDefinitions.MaxNameLength;
    public const int MaxFileNameLength = 255;
    public const int MaxContentTypeLength = 128;
    public const int MaxBlobPathLength = 512;
    public const int MaxTagsLength = 500;
    public const int MaxReasonLength = 256;
    public const int MaxDetailLength = 2000;
    public const int Sha256Length = 64;

    /// <summary>Blob container for documents; the path is <c>{tenant}/{workspace}/{document}/{version}/{file}</c>.</summary>
    public const string Container = "documents";

    /// <summary>FR-KNW-001 accepted types (content type → extension); configurable max size lives in <c>KnowledgeOptions</c>.</summary>
    public static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = ".pdf",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx",
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = ".pptx",
        ["text/plain"] = ".txt",
        ["text/markdown"] = ".md",
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
    };
}
