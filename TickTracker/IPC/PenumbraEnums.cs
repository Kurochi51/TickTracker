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

public enum ApiCollectionType : byte
{
    Yourself = 0,

    MalePlayerCharacter,
    FemalePlayerCharacter,
    MaleNonPlayerCharacter,
    FemaleNonPlayerCharacter,
    NonPlayerChild,
    NonPlayerElderly,

    MaleMidlander,
    FemaleMidlander,
    MaleHighlander,
    FemaleHighlander,

    MaleWildwood,
    FemaleWildwood,
    MaleDuskwight,
    FemaleDuskwight,

    MalePlainsfolk,
    FemalePlainsfolk,
    MaleDunesfolk,
    FemaleDunesfolk,

    MaleSeekerOfTheSun,
    FemaleSeekerOfTheSun,
    MaleKeeperOfTheMoon,
    FemaleKeeperOfTheMoon,

    MaleSeawolf,
    FemaleSeawolf,
    MaleHellsguard,
    FemaleHellsguard,

    MaleRaen,
    FemaleRaen,
    MaleXaela,
    FemaleXaela,

    MaleHelion,
    FemaleHelion,
    MaleLost,
    FemaleLost,

    MaleRava,
    FemaleRava,
    MaleVeena,
    FemaleVeena,

    MaleMidlanderNpc,
    FemaleMidlanderNpc,
    MaleHighlanderNpc,
    FemaleHighlanderNpc,

    MaleWildwoodNpc,
    FemaleWildwoodNpc,
    MaleDuskwightNpc,
    FemaleDuskwightNpc,

    MalePlainsfolkNpc,
    FemalePlainsfolkNpc,
    MaleDunesfolkNpc,
    FemaleDunesfolkNpc,

    MaleSeekerOfTheSunNpc,
    FemaleSeekerOfTheSunNpc,
    MaleKeeperOfTheMoonNpc,
    FemaleKeeperOfTheMoonNpc,

    MaleSeawolfNpc,
    FemaleSeawolfNpc,
    MaleHellsguardNpc,
    FemaleHellsguardNpc,

    MaleRaenNpc,
    FemaleRaenNpc,
    MaleXaelaNpc,
    FemaleXaelaNpc,

    MaleHelionNpc,
    FemaleHelionNpc,
    MaleLostNpc,
    FemaleLostNpc,

    MaleRavaNpc,
    FemaleRavaNpc,
    MaleVeenaNpc,
    FemaleVeenaNpc,

    Default = 0xE0,
    Interface = 0xE1,
    Current = 0xE2,
}


