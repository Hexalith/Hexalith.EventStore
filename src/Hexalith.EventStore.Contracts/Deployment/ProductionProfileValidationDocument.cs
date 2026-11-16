namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Shared nonserialized validation view of common profile sections.</summary>
/// <param name="SchemaVersion">The required schema version.</param>
/// <param name="Purpose">The required purpose.</param>
/// <param name="Template">The required template.</param>
/// <param name="Infrastructure">The required infrastructure.</param>
/// <param name="Release">The required release.</param>
/// <param name="Environment">The required environment.</param>
/// <param name="Applications">The required applications.</param>
/// <param name="Components">The required components.</param>
/// <param name="Catalogs">The required catalogs.</param>
/// <param name="Protection">The required protection.</param>
/// <param name="Issuer">The required issuer.</param>
/// <param name="Freshness">The required freshness.</param>
/// <param name="Restore">The required restore.</param>
/// <param name="Evidence">The required evidence.</param>
/// <param name="InfrastructureDigest">The required infrastructure digest.</param>
internal sealed record ProductionProfileValidationDocument(
    int SchemaVersion,
    string Purpose,
    ProductionProfileTemplate Template,
    ProductionProfileInfrastructure Infrastructure,
    ProductionProfileRelease Release,
    ProductionProfileEnvironment Environment,
    IReadOnlyList<ProductionProfileApplication> Applications,
    IReadOnlyList<ProductionProfileValidationComponent> Components,
    ProductionProfileCatalogs Catalogs,
    ProductionProfileProtection Protection,
    ProductionProfileIssuer Issuer,
    ProductionProfileFreshness Freshness,
    ProductionProfileRestore Restore,
    IReadOnlyList<ProductionProfileEvidence> Evidence,
    string InfrastructureDigest)
{
    /// <summary>Creates a view after closed shape validation has succeeded.</summary>
    internal static ProductionProfileValidationDocument From(object candidate) => candidate switch
    {
        ProductionProfile v1 => new(
            v1.SchemaVersion, v1.Purpose, v1.Template, v1.Infrastructure, v1.Release, v1.Environment,
            v1.Applications, v1.Components.Select(ProductionProfileValidationComponent.From).ToArray(),
            v1.Catalogs, v1.Protection, v1.Issuer, v1.Freshness, v1.Restore, v1.Evidence, v1.InfrastructureDigest),
        ProductionProfileV2 v2 => new(
            v2.SchemaVersion, v2.Purpose, v2.Template, v2.Infrastructure, v2.Release, v2.Environment,
            v2.Applications, v2.Components.Select(ProductionProfileValidationComponent.From).ToArray(),
            v2.Catalogs, v2.Protection, v2.Issuer, v2.Freshness, v2.Restore, v2.Evidence, v2.InfrastructureDigest),
        _ => throw new InvalidOperationException(),
    };
}

