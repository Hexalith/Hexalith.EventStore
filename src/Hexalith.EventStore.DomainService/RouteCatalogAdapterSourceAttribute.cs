namespace Hexalith.EventStore.DomainService;

/// <summary>Names a compiled owner source whose Create method returns its exact installed intent adapters.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class RouteCatalogAdapterSourceAttribute(Type sourceType) : Attribute
{
    /// <summary>Gets the owner factory type.</summary>
    public Type SourceType { get; } = sourceType;
}
