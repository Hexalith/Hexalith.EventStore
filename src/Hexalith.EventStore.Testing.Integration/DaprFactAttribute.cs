using System.Reflection;
using System.Runtime.CompilerServices;

using Xunit;
using Xunit.v3;

namespace Hexalith.EventStore.Testing.Integration;

/// <summary>
/// Runs a test only when local DAPR infrastructure from <c>dapr init</c> is available.
/// </summary>
public sealed class DaprFactAttribute : FactAttribute {
    /// <summary>Initializes a new instance of the <see cref="DaprFactAttribute"/> class.</summary>
    public DaprFactAttribute(
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber) {
        Skip = DaprTestPrerequisites.SkipReason;
        SkipUnless = nameof(DaprTestPrerequisites.IsAvailable);
        SkipType = typeof(DaprTestPrerequisites);
    }
}

/// <summary>
/// Runs a DAPR-backed performance test only when performance tests are explicitly enabled.
/// </summary>
public sealed class DaprPerformanceFactAttribute : FactAttribute {
    /// <summary>Initializes a new instance of the <see cref="DaprPerformanceFactAttribute"/> class.</summary>
    public DaprPerformanceFactAttribute(
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber) {
        Skip = DaprPerformanceTestPrerequisites.SkipReason;
        SkipUnless = nameof(DaprPerformanceTestPrerequisites.IsAvailable);
        SkipType = typeof(DaprPerformanceTestPrerequisites);
    }
}

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

/// <summary>
/// Process-wide gate that serializes code blocks sharing local DAPR/Aspire infrastructure.
/// </summary>
public static class DaprTestExecutionGate {
    private static readonly SemaphoreSlim s_gate = new(1, 1);

    /// <summary>Enters the gate, blocking until it is free, and returns a lease that releases on dispose.</summary>
    public static IDisposable Enter() {
        s_gate.Wait();
        return new Lease();
    }

    private sealed class Lease : IDisposable {
        private bool _disposed;

        public void Dispose() {
            if (_disposed) {
                return;
            }

            _disposed = true;
            _ = s_gate.Release();
        }
    }
}
