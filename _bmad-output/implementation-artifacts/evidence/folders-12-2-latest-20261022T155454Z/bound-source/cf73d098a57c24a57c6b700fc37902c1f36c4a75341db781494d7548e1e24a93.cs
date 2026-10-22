namespace Hexalith.EventStore.Server.Security;

/// <summary>Installed history of one guarded cell. Restored absence is not a fresh empty cell.</summary>
public enum GuardedCellHistory
{
    /// <summary>The installation cannot classify the cell.</summary>
    Unknown = 0,

    /// <summary>The cell has never been written.</summary>
    NeverWritten = 1,

    /// <summary>The cell is currently occupied by installed bytes.</summary>
    Occupied = 2,

    /// <summary>A restore removed bytes that the installation still remembers.</summary>
    RestoredAbsent = 3,
}
