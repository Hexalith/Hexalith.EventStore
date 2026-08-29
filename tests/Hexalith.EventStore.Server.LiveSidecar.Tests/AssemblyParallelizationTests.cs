using System.Reflection;

using Shouldly;

using Xunit.Sdk;
using Xunit.v3;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests;

/// <summary>
/// Tests assembly-level xUnit parallelization metadata.
/// </summary>
public sealed class AssemblyParallelizationTests
{
    /// <summary>
    /// Verifies live-sidecar tests remain serialized under xUnit 4.
    /// </summary>
    [Fact]
    public void AssemblyDisablesTestParallelization()
    {
        ParallelizationAttribute attribute = typeof(AssemblyParallelizationTests).Assembly
            .GetCustomAttribute<ParallelizationAttribute>()
            .ShouldNotBeNull();

        attribute.GetMode().ShouldBe(ParallelMode.None);
    }
}
