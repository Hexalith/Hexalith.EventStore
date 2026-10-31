using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Runs genuine published 3.129.0 deserialization, binding and trust code in a separate SDK load context.</summary>
public sealed class TechnicalHttpPreviousPeerTests
{
    [Fact]
    public async Task ActualPreviousPeerAcceptsLegacyActivationAndRefusesTechnicalV2Trust()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "legacy-sdk-3.129.0");
        // Exact DLLs from the NuGet.org repository-signed 3.129.0 archives, verified during qualification.
        Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(directory, "Hexalith.EventStore.ServiceDefaults.dll"), TestContext.Current.CancellationToken)))
            .ShouldBe("27eaf7c72322b13d33b98aa726535e1a392e95a67c99788e76c468fa2e16f124");
        Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(directory, "Hexalith.EventStore.Contracts.dll"), TestContext.Current.CancellationToken)))
            .ShouldBe("f6fabeba9600e44b6a3d5be56b8a7707438943e2b74bf6a496151e9bb4ee3942");
        var load = new PreviousRouteCatalogPeerLoadContext(directory);
        try
        {
            Assembly previous = load.LoadFromAssemblyPath(Path.Combine(directory, "Hexalith.EventStore.ServiceDefaults.dll"));
            Assembly contracts = load.LoadFromAssemblyPath(Path.Combine(directory, "Hexalith.EventStore.Contracts.dll"));
            previous.ShouldNotBeSameAs(typeof(RouteCatalogActivationBinding).Assembly);
            Type activationType = contracts.GetType(typeof(RouteCatalogActivation).FullName!, throwOnError: true)!;
            activationType.GetProperty("TechnicalHttpReceivers").ShouldBeNull();
            Type hostOptions = previous.GetType(typeof(RouteCatalogHostOptions).FullName!, throwOnError: true)!;
            object configured = Activator.CreateInstance(hostOptions)!;
            hostOptions.GetProperty("DevelopmentFixture")!.SetValue(configured, true);
            object options = typeof(Options).GetMethod(nameof(Options.Create))!.MakeGenericMethod(hostOptions).Invoke(null, [configured])!;
            var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns(Environments.Development);
            object trust = Activator.CreateInstance(previous.GetType(typeof(DevelopmentRouteCatalogTrustValidator).FullName!, throwOnError: true)!, options, environment)!;
            MethodInfo compute = previous.GetType(typeof(RouteCatalogActivationBinding).FullName!, throwOnError: true)!.GetMethod(nameof(RouteCatalogActivationBinding.ComputeDigest))!;
            MethodInfo validate = trust.GetType().GetMethod(nameof(DevelopmentRouteCatalogTrustValidator.ValidateAsync))!;
            var fixture = new RouteCatalogTestFixture();
            var legacy = fixture.Candidate();
            object parsedLegacy = JsonSerializer.Deserialize(JsonSerializer.SerializeToUtf8Bytes(legacy), activationType)!;
            string oldDigest = (string)compute.Invoke(null, [parsedLegacy])!;
            oldDigest.ShouldBe(legacy.TrustProof);
            await InvokeTrustAsync(validate, trust, parsedLegacy, oldDigest);
            var descriptor = new RouteCatalogTechnicalHttpEndpoint("/api/v1/admin/storage/snapshot-policy", ["PUT"], "actual-v2-wire-shape",
                RouteCatalogTestFixture.Hash("request"), RouteCatalogTestFixture.Hash("response"), ["EventStoreHuman"], RouteCatalogTestFixture.Hash("policy"), RouteCatalogTestFixture.Hash("filter"));
            var v2 = new RouteCatalogTestFixture(compiledSchemaThree: true).Candidate() with { TechnicalHttpReceivers = [new("eventstore", RouteCatalogTechnicalHttpPolicy.FenceVersion, [descriptor])] };
            v2 = v2 with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(v2) };
            object parsedV2 = JsonSerializer.Deserialize(JsonSerializer.SerializeToUtf8Bytes(v2), activationType)!;
            string oldV2Digest = (string)compute.Invoke(null, [parsedV2])!;
            oldV2Digest.ShouldNotBe(v2.TrustProof);
            Exception refusal = await Should.ThrowAsync<Exception>(() => InvokeTrustAsync(validate, trust, parsedV2, oldV2Digest));
            refusal.GetType().FullName.ShouldBe(typeof(RouteCatalogUnavailableException).FullName);
            refusal.GetType().Assembly.ShouldBeSameAs(previous);
            refusal.GetType().GetProperty(nameof(RouteCatalogUnavailableException.Category))!.GetValue(refusal)!.ToString().ShouldBe(nameof(RouteCatalogRefusalReason.Trust));
        }
        finally { load.Unload(); }
    }

    private static async Task InvokeTrustAsync(MethodInfo method, object trust, object activation, string digest)
    {
        Task pending;
        try { pending = (Task)method.Invoke(trust, [activation, digest, TestContext.Current.CancellationToken])!; }
        catch (TargetInvocationException failure) when (failure.InnerException is not null)
        { ExceptionDispatchInfo.Capture(failure.InnerException).Throw(); throw; }
        await pending;
    }
}
