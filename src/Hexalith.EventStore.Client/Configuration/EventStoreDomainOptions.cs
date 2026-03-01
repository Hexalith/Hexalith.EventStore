
namespace Hexalith.EventStore.Client.Configuration;

/// <summary>
/// Per-domain configuration options for event store resource naming.
/// Used in the five-layer cascading configuration system where each layer can override
/// individual properties. A <c>null</c> value means "not set at this layer — use value from previous layer."
/// </summary>
/// <remarks>
/// <para>
/// The cascade resolution applies layers in priority order (lowest to highest):
/// <list type="number">
///   <item><description>Layer 1 — Convention defaults (from <c>NamingConventionEngine</c>)</description></item>
///   <item><description>Layer 2 — Global code options (from <c>EventStoreOptions</c>)</description></item>
///   <item><description>Layer 3 — Domain self-config (<c>OnConfiguring()</c> override)</description></item>
///   <item><description>Layer 4 — External config (<c>appsettings.json</c>)</description></item>
///   <item><description>Layer 5 — Explicit override (<c>ConfigureDomain()</c> callback)</description></item>
/// </list>
/// </para>
/// </remarks>
public class EventStoreDomainOptions {
	/// <summary>
	/// Gets or sets the DAPR state store name for this domain.
	/// When <c>null</c>, the convention default is used (e.g., <c>{domain}-eventstore</c>).
	/// </summary>
	public string? StateStoreName { get; set; }

	/// <summary>
	/// Gets or sets the pub/sub topic pattern for this domain.
	/// When <c>null</c>, the convention default is used (e.g., <c>{domain}.events</c>).
	/// </summary>
	public string? TopicPattern { get; set; }

	/// <summary>
	/// Gets or sets the dead-letter topic pattern for this domain.
	/// When <c>null</c>, the convention default is used (e.g., <c>deadletter.{domain}.events</c>).
	/// </summary>
	public string? DeadLetterTopicPattern { get; set; }
}
