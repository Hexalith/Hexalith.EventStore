namespace Hexalith.EventStore.Testing.OriginalSource;

/// <summary>Primary-source page address, retained atomically with the committed head for bounded direct page reads.</summary>
internal sealed record SyntheticOriginalSourcePageAddress(long FirstSequence, string PageId);
