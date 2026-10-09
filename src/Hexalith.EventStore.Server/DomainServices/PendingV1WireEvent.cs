namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>Retains bounded unauthoritative event fields until the complete response is admitted.</summary>
internal sealed class PendingV1WireEvent
{
    /// <summary>Gets or sets the exact legacy alias.</summary>
    internal string? TypeName { get; set; }
    /// <summary>Gets or sets the privately decoded payload owner.</summary>
    internal byte[]? Payload { get; set; }
    /// <summary>Gets or sets the historical format default.</summary>
    internal string Format { get; set; } = "json";
    /// <summary>Gets or sets the raw metadata discriminator.</summary>
    internal int? MetadataVersion { get; set; }
    /// <summary>Gets or sets canonical-type raw presence.</summary>
    internal bool ContractPresent { get; set; }
    /// <summary>Gets or sets payload-version raw presence.</summary>
    internal bool VersionPresent { get; set; }
    /// <summary>Gets or sets the standalone JSON payload version.</summary>
    internal int? PayloadVersion { get; set; }
}
