namespace Hexalith.EventStore.DomainService;

/// <summary>An owner request outside its committed domain or tenant authorization.</summary>
internal sealed class DomainRouteCatalogForbiddenAuxiliaryRequestException : Exception;
