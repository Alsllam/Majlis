namespace Majlis.Approvals.Domain.Constants;

/// <summary>Permission names; identical strings are used by the web and mobile apps.</summary>
public static class ApprovalsPermissions
{
    public const string ViewApproval = "Permissions.Approvals.ViewApproval";

    /// <summary>Held by drivers: ai-service creates requests with the driver's token.</summary>
    public const string RequestAction = "Permissions.Approvals.RequestAction";

    public const string ApproveAction = "Permissions.Approvals.ApproveAction";
}

/// <summary>Localization keys for business errors in this module.</summary>
public static class ApprovalsErrors
{
    public const string NotWorkspaceMember = "Approvals:Request:NotWorkspaceMember";
    public const string NotPending = "Approvals:Request:NotPending";
    public const string SelfApprovalNotAllowed = "Approvals:Request:SelfApprovalNotAllowed";
    public const string ApproverRoleRequired = "Approvals:Request:ApproverRoleRequired";
    public const string UnknownTool = "Approvals:Request:UnknownTool";
    public const string InvalidArgs = "Approvals:Request:InvalidArgs";
    public const string ReasonRequired = "Approvals:Request:ReasonRequired";
}

public static class ApprovalsFieldDefinitions
{
    public const int MaxToolLength = 64;
    public const int MaxSummaryLength = Framework.Domain.Localization.FieldDefinitions.MaxDescriptionLength;
    public const int MaxReasonLength = Framework.Domain.Localization.FieldDefinitions.MaxDescriptionLength;
    public const int MaxNoteLength = Framework.Domain.Localization.FieldDefinitions.MaxNoteLength;
    public const int MaxArgsLength = 16000;
    public const int MaxDisplayNameLength = Framework.Domain.Localization.FieldDefinitions.MaxNameLength;
    public const int MaxResultLength = Framework.Domain.Localization.FieldDefinitions.MaxDescriptionLength;
}

/// <summary>Tools the agent may propose (AI-AGT-006) with their risk; the owning module runs them after approval.</summary>
public static class KnownTools
{
    public const string CreateTask = "create_task";
    public const string UpdateTask = "update_task";
    public const string DraftDocument = "draft_document";

    public static readonly IReadOnlyDictionary<string, Enums.RiskLevel> Risk = new Dictionary<string, Enums.RiskLevel>
    {
        [CreateTask] = Enums.RiskLevel.Low,
        [DraftDocument] = Enums.RiskLevel.Low,
        [UpdateTask] = Enums.RiskLevel.Medium,
    };
}
