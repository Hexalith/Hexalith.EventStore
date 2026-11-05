using Hexalith.EventStore.Client.Streams;

namespace Hexalith.EventStore.DomainService;

/// <summary>Qualifies the SDK's explicitly enrolled native workload reader independently of ordinary readers.</summary>
/// <param name="Reader">The independently authenticated reader requiring native source proof.</param>
internal sealed record DomainProjectionNativeSourceReader(IAuthoritativeEventStreamReader Reader);
