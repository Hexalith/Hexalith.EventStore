namespace Hexalith.EventStore.Testing.Integration;

/// <summary>An exact HTTP resource binding required by a descriptor fixture.</summary>
/// <param name="ResourceId">The declared resource identity.</param>
/// <param name="ModuleId">The declaring module identity.</param>
/// <param name="EndpointName">The endpoint name.</param>
/// <param name="Timeout">The finite request timeout.</param>
/// <param name="Authenticated">Whether requests carry the private runner bearer credential.</param>
public sealed record TestEnvironmentEndpoint(string ResourceId, string ModuleId, string EndpointName, TimeSpan Timeout, bool Authenticated = true)
{
    /// <summary>Gets whether this binding explicitly requires the separate private Dapr invocation credential.</summary>
    public bool DaprInvocation { get; init; }
}
