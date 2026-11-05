using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Testing.OwnerSourceRead;

/// <summary>The actual SDK page, or a sanitized failure without a fabricated successful page.</summary>
/// <param name="Id">The original one-shot request identity.</param>
/// <param name="Page">The actual bounded SDK result, absent on failure.</param>
/// <param name="Failure">A finite failure category, never exception details.</param>
public sealed record OwnerSourceReadResult(string Id, StreamReadPage? Page, string? Failure);
