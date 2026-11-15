namespace Hexalith.EventStore.Testing.OriginalSource;

/// <summary>Owner append disposition; conflict leaves original bytes intact and never substitutes callback success.</summary>
internal sealed record SyntheticOriginalSourceAppendResult(string Id, bool Conflict, string? Failure);
