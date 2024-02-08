namespace TickTracker.IPC;

#pragma warning disable MA0048 // File name must match type name

public enum PenumbraApiEc
{
    Success = 0,
    NothingChanged = 1,
    CollectionMissing = 2,
    ModMissing = 3,
    OptionGroupMissing = 4,
    OptionMissing = 5,

    CharacterCollectionExists = 6,
    LowerPriority = 7,
    InvalidGamePath = 8,
    FileMissing = 9,
    InvalidManipulation = 10,
    InvalidArgument = 11,
    PathRenameFailed = 12,
    CollectionExists = 13,
    AssignmentCreationDisallowed = 14,
    AssignmentDeletionDisallowed = 15,
    InvalidIdentifier = 16,
    SystemDisposed = 17,
    AssignmentDeletionFailed = 18,
    UnknownError = 255,
}

public enum ModSettingChange
{
    Inheritance,
    EnableState,
    Priority,
    Setting,
    MultiInheritance,
    MultiEnableState,
}


