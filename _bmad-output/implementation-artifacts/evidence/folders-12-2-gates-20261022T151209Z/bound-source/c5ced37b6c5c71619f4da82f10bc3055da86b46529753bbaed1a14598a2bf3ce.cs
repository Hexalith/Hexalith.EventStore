namespace Hexalith.EventStore.DomainService;

/// <summary>Identity of the one catalog and translator pair in a process host.</summary>
internal sealed record ProcessRegistration(Type CatalogType, Type TranslatorType);
