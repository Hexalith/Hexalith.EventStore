using System.Reflection;

using Xunit.v3;

namespace Hexalith.EventStore.Testing.Integration;

/// <summary>
/// Serializes tests that share local DAPR/Aspire infrastructure and mutable fake publishers.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class DaprTestSerializationAttribute : BeforeAfterTestAttribute {
    private static readonly SemaphoreSlim s_daprTestGate = new(1, 1);

    /// <inheritdoc/>
    public override void Before(MethodInfo methodUnderTest, IXunitTest test) => s_daprTestGate.Wait();

    /// <inheritdoc/>
    public override void After(MethodInfo methodUnderTest, IXunitTest test) => _ = s_daprTestGate.Release();
}

