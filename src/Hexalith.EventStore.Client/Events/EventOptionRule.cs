using System.Text.Json;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Declares one implementation-owned compile-time option's type, requiredness and exact default.</summary>
/// <param name="Name">The exact property name.</param>
/// <param name="Kind">The JSON kind; True also admits False for Boolean rules.</param>
/// <param name="Required">Whether absence is forbidden.</param>
/// <param name="CanonicalDefault">Exact JSON default for an optional rule, or null for a required rule.</param>
/// <param name="IntegerOnly">Whether a numeric value must be an exact integer.</param>
internal sealed record EventOptionRule(string Name, JsonValueKind Kind, bool Required, string? CanonicalDefault = null, bool IntegerOnly = false);
