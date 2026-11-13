using System.Reflection;
using System.Runtime.ExceptionServices;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Supplies existing compiled-owner inputs and the unchanged uncached path for memo parity controls.</summary>
internal static class CanonicalHashTestInputs
{
    /// <summary>Returns a real schema-three operation from the existing owner fixture.</summary>
    internal static RouteOperationContract Operation() => new RouteCatalogTestFixture(compiledSchemaThree: true).Envelope.Operations![0];

    /// <summary>Returns the existing fixture's validated descriptor schema.</summary>
    internal static IdempotencyDescriptorSchema Descriptor() => new RouteCatalogTestFixture().Envelope.Idempotency[0].DescriptorSchema;

    /// <summary>Computes through the original path without inheriting a private owner.</summary>
    internal static string Uncached(Func<string> compute)
    {
        using var suppression = RouteCatalogDecodeScope.Suppress();
        return compute();
    }

    /// <summary>Exercises private preimages only in tests, without changing the production API.</summary>
    internal static string Hash(object value)
    {
        try
        {
            return (string)typeof(RouteCatalogCodec).GetMethod("Hash", BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(typeof(object)).Invoke(null, [value])!;
        }
        catch (TargetInvocationException failure) when (failure.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
            throw;
        }
    }
}
