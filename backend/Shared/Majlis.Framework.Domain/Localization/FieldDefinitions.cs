namespace Majlis.Framework.Domain.Localization;

/// <summary>Shared limits. Validators read these; never hard-code lengths.</summary>
public static class FieldDefinitions
{
    public const int MaxNameLength = 120;
    public const int MaxDescriptionLength = 1000;
    public const int MaxMessageLength = 8000;
    public const int MaxNoteLength = 500;
    public const int LanguageCodeLength = 8;
}
