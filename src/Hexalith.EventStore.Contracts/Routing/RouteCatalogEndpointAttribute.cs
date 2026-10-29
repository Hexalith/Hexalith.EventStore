namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Binds a generated ingress action to its actual compiled command or query contract.</summary>
/// <param name="ContractType">The compiled owner contract.</param>
/// <param name="Kind">Exactly command or query.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RouteCatalogEndpointAttribute(Type ContractType, string Kind) : Attribute
{
    /// <summary>Gets the compiled contract type.</summary>
    public Type ContractType { get; } = ContractType;
    /// <summary>Gets the message kind.</summary>
    public string Kind { get; } = Kind;
}
