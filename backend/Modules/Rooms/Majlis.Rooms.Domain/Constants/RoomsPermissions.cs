namespace Majlis.Rooms.Domain.Constants;

/// <summary>Permission names; identical strings are used by the web and mobile apps.</summary>
public static class RoomsPermissions
{
    public const string ViewRoom = "Permissions.Rooms.ViewRoom";
    public const string CreateRoom = "Permissions.Rooms.CreateRoom";
    public const string ManageParticipants = "Permissions.Rooms.ManageParticipants";
    public const string DriveSession = "Permissions.Rooms.DriveSession";
    public const string TakeOverSession = "Permissions.Rooms.TakeOverSession";
}

/// <summary>Localization keys for business errors in this module.</summary>
public static class RoomsErrors
{
    public const string NotParticipant = "Rooms:Room:NotParticipant";
    public const string ObserverCannotDrive = "Rooms:Room:ObserverCannotDrive";
    public const string ActiveSessionExists = "Rooms:Session:ActiveSessionExists";
    public const string SessionEnded = "Rooms:Session:SessionEnded";
    public const string NotDriver = "Rooms:Session:NotDriver";
    public const string ControlChanged = "Rooms:Session:ControlChanged";
    public const string ControlNotFree = "Rooms:Session:ControlNotFree";
    public const string AlreadyDriver = "Rooms:Session:AlreadyDriver";
    public const string NoPendingHandOff = "Rooms:Session:NoPendingHandOff";
    public const string RequestNotFound = "Rooms:Session:RequestNotFound";
    public const string TurnInProgress = "Rooms:Session:TurnInProgress";
    public const string TurnNotRunning = "Rooms:Session:TurnNotRunning";
    public const string HandOffToSelf = "Rooms:Session:HandOffToSelf";
    public const string NotWorkspaceMember = "Rooms:Room:NotWorkspaceMember";
    public const string UserNotWorkspaceMember = "Rooms:Room:UserNotWorkspaceMember";
}

public static class RoomsFieldDefinitions
{
    public const int MaxRoomNameLength = Framework.Domain.Localization.FieldDefinitions.MaxNameLength;
    public const int MaxPurposeLength = Framework.Domain.Localization.FieldDefinitions.MaxDescriptionLength;
    public const int MaxInstructionLength = Framework.Domain.Localization.FieldDefinitions.MaxMessageLength;
    public const int MaxNoteLength = Framework.Domain.Localization.FieldDefinitions.MaxNoteLength;
    public const int MaxDisplayNameLength = Framework.Domain.Localization.FieldDefinitions.MaxNameLength;
    public const int MaxEventTypeLength = 64;
}
