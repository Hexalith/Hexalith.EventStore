namespace Hexalith.EventStore.Client.Gateway;

/// <summary>
/// Options controlling the absolute command-status <c>Location</c> header emitted by generated command controllers.
/// </summary>
public sealed class CommandStatusLocationOptions {
    /// <summary>
    /// Gets or sets the browser-facing EventStore gateway base authority used to compose the absolute
    /// command-status <c>Location</c> URL. When <see langword="null"/> the generated command controller emits
    /// no <c>Location</c> header (fail-closed per architecture invariant AD-17).
    /// </summary>
    public Uri? GatewayStatusBase { get; set; }
}
