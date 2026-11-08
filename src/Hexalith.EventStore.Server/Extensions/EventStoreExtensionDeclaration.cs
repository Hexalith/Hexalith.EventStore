namespace Hexalith.EventStore.Server.Extensions;

/// <summary>One composer-declared extension package that the gateway must load and verify before it starts.</summary>
/// <param name="PackageId">The package and assembly name the composed host references.</param>
/// <param name="ExtensionId">The declared stable extension identity.</param>
/// <param name="ApiMajor">The declared extension-API major.</param>
public sealed record EventStoreExtensionDeclaration(string PackageId, string ExtensionId, int ApiMajor);
