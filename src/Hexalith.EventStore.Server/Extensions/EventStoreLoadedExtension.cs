namespace Hexalith.EventStore.Server.Extensions;

/// <summary>One extension package whose assembly identity matched its declaration and whose adapters are registered.</summary>
/// <param name="PackageId">The loaded package and assembly name.</param>
/// <param name="ExtensionId">The verified extension identity.</param>
/// <param name="ApiMajor">The verified extension-API major.</param>
/// <param name="AdapterCount">The number of trusted intent adapters the package contributed.</param>
public sealed record EventStoreLoadedExtension(string PackageId, string ExtensionId, int ApiMajor, int AdapterCount);
