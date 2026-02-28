
namespace Hexalith.EventStore.Client.Configuration;

/// <summary>
/// Global options for the Event Store client SDK.
/// Configured via <c>AddEventStore(options =&gt; ...)</c> and available through <c>IOptions&lt;EventStoreOptions&gt;</c>.
/// </summary>
/// <remarks>
/// This class uses the standard .NET Options pattern (POCO with parameterless constructor and settable properties)
/// for compatibility with <c>IOptions&lt;T&gt;</c>, <c>IOptionsSnapshot&lt;T&gt;</c>, and <c>IOptionsMonitor&lt;T&gt;</c>.
/// Story 16-6 will extend this class with cascading configuration properties.
/// </remarks>
public class EventStoreOptions {
	/// <summary>
	/// Gets or sets a value indicating whether registration diagnostics are enabled.
	/// This minimal flag exists to validate global options configuration flow and can be expanded in later stories.
	/// </summary>
	public bool EnableRegistrationDiagnostics { get; set; }
}
