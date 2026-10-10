namespace Majlis.Tasks.Domain.Constants;

/// <summary>Permission names; identical strings are used by the web and mobile apps.</summary>
public static class TasksPermissions
{
    public const string ViewTask = "Permissions.Tasks.ViewTask";
    public const string CreateTask = "Permissions.Tasks.CreateTask";
    public const string UpdateTask = "Permissions.Tasks.UpdateTask";
}

public static class TasksErrors
{
    public const string NotWorkspaceMember = "Tasks:Task:NotWorkspaceMember";
    public const string AssigneeNotMember = "Tasks:Task:AssigneeNotMember";
}

public static class TasksFieldDefinitions
{
    public const int MaxTitleLength = Framework.Domain.Localization.FieldDefinitions.MaxNameLength;
    public const int MaxDescriptionLength = Framework.Domain.Localization.FieldDefinitions.MaxDescriptionLength;
    public const int MaxDisplayNameLength = Framework.Domain.Localization.FieldDefinitions.MaxNameLength;
}
