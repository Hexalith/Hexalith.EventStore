using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

using Xunit;
using Xunit.v3;

namespace Hexalith.EventStore.Testing.Integration;

/// <summary>
/// Runs a test only when local DAPR infrastructure from <c>dapr init</c> is available.
/// </summary>
public sealed class DaprFactAttribute : FactAttribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="DaprFactAttribute"/> class.
    /// </summary>
    /// <param name="sourceFilePath">The captured source file path (supplied by the compiler).</param>
    /// <param name="sourceLineNumber">The captured source line number (supplied by the compiler).</param>
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
    /// <summary>
    /// Initializes a new instance of the <see cref="DaprPerformanceFactAttribute"/> class.
    /// </summary>
    /// <param name="sourceFilePath">The captured source file path (supplied by the compiler).</param>
    /// <param name="sourceLineNumber">The captured source line number (supplied by the compiler).</param>
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
/// Process-wide gate that serializes access to the shared local DAPR/Aspire infrastructure.
/// </summary>
public static class DaprTestExecutionGate {
    private static readonly SemaphoreSlim s_gate = new(1, 1);

    /// <summary>
    /// Acquires the gate, returning a disposable lease that releases it on disposal.
    /// </summary>
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

/// <summary>
/// Discovery-time prerequisite check for DAPR-backed integration tests.
/// </summary>
public static class DaprTestPrerequisites {
    private static readonly int PlacementPort = DaprLocalEndpoints.PlacementPort;
    private static readonly int SchedulerPort = DaprLocalEndpoints.SchedulerPort;
    private static readonly int RedisPort = DaprDiagnostics.ResolveRedisPort();
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);
    private static readonly Lazy<bool> s_isAvailable = new(CheckAvailability);

    /// <summary>
    /// Gets a value indicating whether local DAPR runtime dependencies are reachable.
    /// </summary>
    public static bool IsAvailable => s_isAvailable.Value;

    /// <summary>
    /// Gets the skip reason used when DAPR runtime dependencies are not reachable.
    /// </summary>
    public static string SkipReason
        => "DAPR integration prerequisites are unavailable. Run 'dapr init' and ensure Redis, placement, and scheduler are reachable.";

    private static bool CheckAvailability()
        => IsRedisResponsive()
            && IsPortReachable(PlacementPort)
            && IsPortReachable(SchedulerPort);

    private static bool IsRedisResponsive() {
        try {
            using var client = new TcpClient();
            Task connect = client.ConnectAsync("localhost", RedisPort);
            if (!connect.Wait(ProbeTimeout) || !client.Connected) {
                return false;
            }

            client.SendTimeout = (int)ProbeTimeout.TotalMilliseconds;
            client.ReceiveTimeout = (int)ProbeTimeout.TotalMilliseconds;

            using NetworkStream stream = client.GetStream();
            byte[] ping = Encoding.ASCII.GetBytes("*1\r\n$4\r\nPING\r\n");
            stream.Write(ping);

            Span<byte> buffer = stackalloc byte[16];
            int total = 0;
            while (total < 5) {
                int chunk = stream.Read(buffer[total..]);
                if (chunk <= 0) {
                    break;
                }

                total += chunk;
            }

            return total >= 5 && Encoding.ASCII.GetString(buffer[..total]).StartsWith("+PONG", StringComparison.Ordinal);
        }
        catch {
            return false;
        }
    }

    private static bool IsPortReachable(int port) {
        try {
            using var client = new TcpClient();
            Task connect = client.ConnectAsync("localhost", port);
            return connect.Wait(ProbeTimeout) && client.Connected;
        }
        catch {
            return false;
        }
    }
}

/// <summary>
/// Discovery-time prerequisite check for DAPR-backed performance tests.
/// </summary>
public static class DaprPerformanceTestPrerequisites {
    private const string EnablePerformanceTestsVariable = "HEXALITH_EVENTSTORE_RUN_PERFORMANCE_TESTS";
    private const string LegacyEnablePerformanceTestsVariable = "HEXALITH_TENANTS_RUN_PERFORMANCE_TESTS";

    /// <summary>
    /// Gets a value indicating whether DAPR-backed performance tests should run.
    /// </summary>
    public static bool IsAvailable
        => DaprTestEnvironment.GetVariable(EnablePerformanceTestsVariable, LegacyEnablePerformanceTestsVariable) == "1"
            && DaprTestPrerequisites.IsAvailable;

    /// <summary>
    /// Gets the skip reason used when performance tests are not enabled.
    /// </summary>
    public static string SkipReason
        => $"DAPR performance tests are disabled. Set {EnablePerformanceTestsVariable}=1 and ensure DAPR prerequisites are reachable to run them.";
}
