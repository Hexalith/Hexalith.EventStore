using System.Runtime.CompilerServices;

using Xunit;

namespace Hexalith.EventStore.Testing.Integration;

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

