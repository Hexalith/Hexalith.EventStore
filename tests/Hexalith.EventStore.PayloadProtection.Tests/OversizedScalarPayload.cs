namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>A caller-owned oversized scalar, shared with the converter regression.</summary>
/// <param name="Value">The caller-owned scalar value.</param>
internal sealed record OversizedScalarPayload(string Value);
