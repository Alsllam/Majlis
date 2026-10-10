namespace Majlis.Tasks.Domain.Enums;

public enum TaskStatus
{
    ToDo = 0,
    InProgress = 1,
    Done = 2,
    Cancelled = 3,
}

public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
}

public enum TaskOrigin
{
    Manual = 0,
    Agent = 1,
}
