namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Complete resolved profile candidate; structural presence never conveys authority.</summary>
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
public sealed record ProductionProfile(
    int SchemaVersion,
    string Purpose,
    ProductionProfileTemplate Template,
    ProductionProfileInfrastructure Infrastructure,
    ProductionProfileRelease Release,
    ProductionProfileEnvironment Environment,
    IReadOnlyList<ProductionProfileApplication> Applications,
    IReadOnlyList<ProductionProfileComponent> Components,
    ProductionProfileCatalogs Catalogs,
    ProductionProfileProtection Protection,
    ProductionProfileIssuer Issuer,
    ProductionProfileFreshness Freshness,
    ProductionProfileRestore Restore,
    IReadOnlyList<ProductionProfileEvidence> Evidence,
    string InfrastructureDigest);
