namespace Hexalith.EventStore.Server.Tests.Extensions;

/// <summary>The single command payload handled by the probe extension adapters.</summary>
/// <param name="Value">An arbitrary probe value.</param>
public sealed record ExtensionProbeCommand(string Value);
