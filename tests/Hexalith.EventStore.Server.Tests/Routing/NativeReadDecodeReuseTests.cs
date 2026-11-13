using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Checks owner-scoped decoding at each fresh validation of a still-open native read lease.</summary>
public sealed class NativeReadDecodeReuseTests(ITestOutputHelper output)
{
#if DEBUG
    private static bool PrivateExpected => true;
#else
    private static bool PrivateExpected => false;
#endif

    /// <summary>Every native validation owns a closing scope, preserves an unrelated ambient scope, and freshly observes the owner.</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task NativeValidationsOwnTheirDecodeScopeAndKeepFreshObservations(bool enabled, bool retain)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var observations = new List<(RouteCatalogDecodeScope? Scope, int Hits, bool HostMemo)>();
        int trustCalls = 0;
        using var authority = Create(fixture, token =>
        {
            token.ThrowIfCancellationRequested();
            var current = RouteCatalogDecodeScope.Current;
            observations.Add((current, current?.CacheHits ?? 0, current?.HasHostMemo == true));
            return Task.FromResult(fixture.Observations[0]);
        }, () => trustCalls++, enabled, retain);
        await using var lease = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        var before = await fixture.Store.ReadAsync(default);
        observations.Clear();
        trustCalls = 0;
        using var unrelated = RouteCatalogDecodeScope.Enter();
        RouteCatalogCodec.Decode(lease.Activation!.CanonicalCatalog, lease.Activation.RootDigest);
        int unrelatedCaptures = unrelated.CaptureCount;

        await authority.ValidateNativeReadLeaseAsync(lease, TestContext.Current.CancellationToken);
        RouteCatalogDecodeScope.Current.ShouldBeSameAs(unrelated);
        await authority.ValidateNativeReadLeaseAsync(lease, TestContext.Current.CancellationToken);

        observations.Count.ShouldBe(2);
        trustCalls.ShouldBe(2);
        if (PrivateExpected && enabled)
        {
            observations[0].Scope.ShouldNotBeNull().ShouldNotBeSameAs(unrelated);
            observations[1].Scope.ShouldNotBeNull().ShouldNotBeSameAs(observations[0].Scope);
            observations.ShouldAllBe(item => item.Hits > 0 && item.HostMemo == retain);
            observations.ShouldAllBe(item => item.Scope!.Count == 0 && item.Scope.RetainedBytes == 0);
        }
        else observations.ShouldAllBe(item => item.Scope == null);
        unrelated.CaptureCount.ShouldBe(unrelatedCaptures);
        var after = await fixture.Store.ReadAsync(default);
        after.ETag.ShouldBe(before.ETag);
        after.State.Leases.ShouldBe(before.State.Leases);
    }

    /// <summary>A warm native read still checks current host, trust, roots, coordinates, cancellation and lease closure.</summary>
    [Theory]
    [InlineData("host")]
    [InlineData("trust")]
    [InlineData("catalog")]
    [InlineData("coordinates")]
    [InlineData("cancellation")]
    [InlineData("closed")]
    public async Task WarmNativeValidationCannotRetainAuthority(string changed)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        bool changedHost = false;
        var observedScopes = new List<RouteCatalogDecodeScope?>();
        using var authority = Create(fixture, token =>
        {
            observedScopes.Add(RouteCatalogDecodeScope.Current);
            token.ThrowIfCancellationRequested();
            return Task.FromResult(changedHost ? fixture.Observations[0] with { AuthenticationProfile = "changed-native-host"u8.ToArray() }
                : fixture.Observations[0]);
        });
        await using var lease = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        await authority.ValidateNativeReadLeaseAsync(lease, TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        RouteCatalogDispatchLease current = lease;
        if (changed == "host") changedHost = true;
        if (changed == "trust") current = new(lease.Snapshot, () => Task.CompletedTask)
        { Activation = lease.Activation! with { TrustProof = "changed-native-proof" } };
        if (changed == "catalog")
        {
            byte[] bytes = lease.Activation!.CanonicalCatalog.ToArray();
            bytes[0] ^= 1;
            current = new(lease.Snapshot, () => Task.CompletedTask) { Activation = lease.Activation with { CanonicalCatalog = bytes } };
        }
        if (changed == "coordinates") fixture.Store.Coordinates = fixture.Store.Coordinates with { StateKey = "changed-native-authority" };
        if (changed == "cancellation") cancellation.Cancel();
        if (changed == "closed") await lease.DisposeAsync();

        if (changed == "catalog") await Should.ThrowAsync<RouteCatalogValidationException>(() => authority.ValidateNativeReadLeaseAsync(current, cancellation.Token));
        else if (changed == "cancellation") await Should.ThrowAsync<OperationCanceledException>(() => authority.ValidateNativeReadLeaseAsync(current, cancellation.Token));
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.ValidateNativeReadLeaseAsync(current, cancellation.Token));

        RouteCatalogDecodeScope.Current.ShouldBeNull();
        observedScopes.ShouldAllBe(scope => scope == null || scope.Count == 0 && scope.RetainedBytes == 0);
        (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(changed == "closed" ? 0 : 1);
    }

    /// <summary>Measures the complete scope/capture/parse cost against suppressed decoding on a representative structured 219KB catalog.</summary>
    [Fact]
    public void MeasureRepresentativeNativeValidationDecodeCost()
    {
        byte[] bytes = RepresentativeCatalog();
        bytes.Length.ShouldBe(219012);
        string root = Convert.ToHexStringLower(SHA256.HashData(bytes));
        using var memo = new RouteCatalogValidatedBytesMemo();
        long firstStarted = Stopwatch.GetTimestamp();
        using (var first = RouteCatalogDecodeScope.EnterWithMemo(memo)) RouteCatalogCodec.Decode(bytes, root);
        double firstScopedMs = Stopwatch.GetElapsedTime(firstStarted).TotalMilliseconds;
        for (int warmup = 0; warmup < 12; warmup++)
        {
            RouteCatalogCodec.Decode(bytes, root);
            using var scope = RouteCatalogDecodeScope.EnterWithMemo(memo);
            RouteCatalogCodec.Decode(bytes, root);
        }
        var measurements = new List<object>();
        for (int sample = 0; sample < 5; sample++)
        {
            (double elapsed, long allocated) Measure(bool scoped)
            {
                long before = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
                for (int iteration = 0; iteration < 40; iteration++)
                {
                    using var boundary = RouteCatalogDecodeScope.Suppress();
                    using var scope = scoped ? RouteCatalogDecodeScope.EnterWithMemo(memo) : null;
                    // A native validation decodes current profile and trust independently.
                    RouteCatalogCodec.Decode(bytes, root);
                    RouteCatalogCodec.Decode(bytes, root);
                }
                return (Stopwatch.GetElapsedTime(started).TotalMilliseconds / 40,
                    (GC.GetAllocatedBytesForCurrentThread() - before) / 40);
            }
            var suppressed = Measure(false);
            var owned = Measure(true);
            measurements.Add(new { sample, suppressedMs = suppressed.elapsed, ownedWarmMs = owned.elapsed,
                suppressedAllocatedBytes = suppressed.allocated, ownedWarmAllocatedBytes = owned.allocated });
        }
        output.WriteLine(JsonSerializer.Serialize(new { representativeOnly = true, catalogBytes = bytes.Length,
            structuredSchemaProperties = 1000, decodesPerValidation = 2, firstScopedMs, measurements }));
    }

    /// <summary>Builds the representative structured 219KB canonical document shared by focused computation benchmarks.</summary>
    internal static byte[] RepresentativeCatalog()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        var properties = Enumerable.Range(0, 1000).ToDictionary(index => "field" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture),
            _ => new { type = "string", description = "representative-owner-schema-field" });
        byte[] Build(int padding)
        {
            using var schema = JsonDocument.Parse(JsonSerializer.Serialize(new { properties, description = new string('x', padding) }));
            RouteOperationContract operation = fixture.Envelope.Operations![0] with { RequestSchema = schema.RootElement.Clone() };
            operation = operation with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(operation) };
            var envelope = fixture.Envelope with
            {
                Operations = fixture.Envelope.Operations.Select(item => item.RouteId == operation.RouteId ? operation : item).ToArray(),
                Routes = fixture.Envelope.Routes.Select(item => item.RouteId == operation.RouteId ? item with { OperationContractDigest = operation.Digest } : item).ToArray(),
            };
            return RouteCatalogCodec.Encode(RouteCatalogCodec.Seal(envelope));
        }
        return Build(219012 - Build(0).Length);
    }

    private static RouteCatalogAuthority Create(RouteCatalogTestFixture fixture,
        Func<CancellationToken, Task<RouteCatalogHostObservation>> capture, Action? trusted = null, bool enabled = true, bool retain = true)
    {
        var options = Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development",
            DevelopmentFixture = true, RequireTechnicalHttpBinding = true, ScopedDecodeReuse = enabled, RetainValidatedCatalogBytes = retain });
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        var actualTrust = new DevelopmentRouteCatalogTrustValidator(options, environment);
        var trust = Substitute.For<IRouteCatalogTrustValidator>();
        trust.ValidateAsync(Arg.Any<RouteCatalogActivation>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            trusted?.Invoke();
            return actualTrust.ValidateAsync(call.Arg<RouteCatalogActivation>(), call.Arg<string>(), call.Arg<CancellationToken>());
        });
        return new(fixture.Store, trust, options, new ObservedRouteCatalogHost(capture), environment: environment);
    }
}
