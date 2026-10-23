namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Immutable body-free preparation and its resolved outcome.</summary>
internal sealed record ContentStoredPublication(ContentPublicationRequest Request, ContentPublicationOutcome Outcome);
