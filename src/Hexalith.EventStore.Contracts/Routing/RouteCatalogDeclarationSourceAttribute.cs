namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Enrolls an owner-authored compiled declaration source, whose public static Routes property supplies full declarations.</summary>
/// <param name="SourceType">The explicit owner source; arbitrary assembly scanning never invents declarations.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class RouteCatalogDeclarationSourceAttribute(Type SourceType) : Attribute
{
    /// <summary>Gets the explicit declaration source type.</summary>
    public Type SourceType { get; } = SourceType;
}
