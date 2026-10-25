using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Explicit separate installed role issuers. No role is synthesized from a credential string.</summary>
/// <param name="Producer">The primary writer issuer.</param><param name="Observer">The independent observation issuer.</param>
/// <param name="Registrar">The enrollment-only issuer.</param><param name="Reader">The authority read issuer.</param><param name="Recovery">The independent recovery issuer.</param>
public sealed record SourcePublicationAuthorityCredentials(IWorkloadAssertionIssuer Producer, IWorkloadAssertionIssuer Observer,
    IWorkloadAssertionIssuer Registrar, IWorkloadAssertionIssuer Reader, IWorkloadAssertionIssuer Recovery);
