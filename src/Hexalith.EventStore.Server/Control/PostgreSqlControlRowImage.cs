namespace Hexalith.EventStore.Server.Control;

/// <summary>One complete addressed SQL row image from an existing authenticated owner.</summary>
internal sealed record PostgreSqlControlRowImage(
    ulong Generation,
    string OwnerFence,
    PostgreSqlControlPayloadSource Payload,
    PostgreSqlRegistryIndex? RegistryIndex);
