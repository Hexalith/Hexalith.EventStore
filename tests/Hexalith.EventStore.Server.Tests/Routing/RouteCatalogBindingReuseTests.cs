using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exercises complete deterministic input capture without retaining independent trust or admission decisions.</summary>
public sealed class RouteCatalogBindingReuseTests(ITestOutputHelper output)
{
    /// <summary>Both versioned preimages retain their exact original bytes under cold and warm reuse.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalV1AndV2DigestBytesAreUnchanged(bool technical)
    {
        RouteCatalogActivation activation = Candidate(technical);
        string expected = RouteCatalogActivationBinding.ComputeDigest(activation);
        using var scope = RouteCatalogBindingScope.Enter();
        RouteCatalogActivationBinding.ComputeDigest(activation).ShouldBe(expected);
        RouteCatalogActivationBinding.ComputeDigest(activation with { TrustProof = "independent-proof-never-captured" }).ShouldBe(expected);
        scope.CacheHits.ShouldBe(1);
    }

    /// <summary>Every captured scalar or nested list participates in exact matching, including malformed current values.</summary>
    [Theory]
    [InlineData("attempt")]
    [InlineData("catalog")]
    [InlineData("root")]
    [InlineData("predecessor")]
    [InlineData("owner")]
    [InlineData("host")]
    [InlineData("host-app")]
    [InlineData("host-inventory")]
    [InlineData("host-topology")]
    [InlineData("host-authentication")]
    [InlineData("host-executable")]
    [InlineData("aux-app")]
    [InlineData("aux-method")]
    [InlineData("aux-operation")]
    [InlineData("aux-domain")]
    [InlineData("http-owner")]
    [InlineData("http-verbs")]
    [InlineData("http-code")]
    [InlineData("http-grants")]
    [InlineData("native")]
    [InlineData("callback")]
    [InlineData("domain-auxiliary")]
    [InlineData("sidecar")]
    [InlineData("cap-app")]
    [InlineData("cap-domain")]
    [InlineData("cap-version")]
    [InlineData("cap-fingerprint")]
    [InlineData("cap-types")]
    [InlineData("receiver-app")]
    [InlineData("receiver-fence")]
    [InlineData("endpoint-pattern")]
    [InlineData("endpoint-methods")]
    [InlineData("endpoint-action")]
    [InlineData("endpoint-request")]
    [InlineData("endpoint-response")]
    [InlineData("endpoint-policies")]
    [InlineData("endpoint-policy-digest")]
    [InlineData("endpoint-filter")]
    [InlineData("endpoint-credential")]
    [InlineData("invocation-caller")]
    [InlineData("invocation-method")]
    [InlineData("invocation-verbs")]
    [InlineData("null-host")]
    [InlineData("null-auxiliary")]
    [InlineData("null-capability-types")]
    [InlineData("null-endpoint-methods")]
    [InlineData("null-invocation-verbs")]
    public void CurrentChangedInputsRetainOriginalDigestOrFailure(string changed)
    {
        RouteCatalogActivation activation = Candidate();
        string original = RouteCatalogActivationBinding.ComputeDigest(activation);
        using var memo = new RouteCatalogBindingMemo();
        using var scope = RouteCatalogBindingScope.Enter(memo);
        RouteCatalogActivationBinding.ComputeDigest(activation).ShouldBe(original);
        RouteCatalogActivationBinding.ComputeDigest(activation).ShouldBe(original);
        scope.CacheHits.ShouldBe(1);
        RouteCatalogActivation current = Change(activation, changed);
        string? expected = null;
        Exception? expectedFailure = Record.Exception(() => expected = RouteCatalogActivationBinding.ComputeDigestCore(current));
        if (expectedFailure is null)
        {
            RouteCatalogActivationBinding.ComputeDigest(current).ShouldBe(expected);
            expected.ShouldNotBe(original);
        }
        else
        {
            Exception actual = Record.Exception(() => RouteCatalogActivationBinding.ComputeDigest(current)).ShouldNotBeNull();
            actual.GetType().ShouldBe(expectedFailure.GetType());
            actual.Message.ShouldBe(expectedFailure.Message);
            if (actual is RouteCatalogValidationException validation)
                validation.Diagnostics.ShouldBe(((RouteCatalogValidationException)expectedFailure).Diagnostics);
            memo.Count.ShouldBe(1);
        }
        scope.CacheHits.ShouldBe(1);
    }

    /// <summary>Caller mutation cannot rewrite a successful memo entry or its nested private collection buffers.</summary>
    [Fact]
    public void RestoredMutableCollectionsMatchOnlyOriginalPrivateCopies()
    {
        RouteCatalogActivation activation = Candidate();
        string original = RouteCatalogActivationBinding.ComputeDigest(activation);
        using var scope = RouteCatalogBindingScope.Enter();
        RouteCatalogActivationBinding.ComputeDigest(activation).ShouldBe(original);
        var verbs = (string[])activation.TechnicalHttpReceivers![0].Invocations![0].HttpMethods;
        verbs[0] = "DELETE";
        RouteCatalogActivationBinding.ComputeDigest(activation).ShouldNotBe(original);
        scope.CacheHits.ShouldBe(0);
        verbs[0] = "GET";
        RouteCatalogActivationBinding.ComputeDigest(activation).ShouldBe(original);
        scope.CacheHits.ShouldBe(1);
    }

    /// <summary>Owner proofs never enter private retained inputs; they remain independent authority observations.</summary>
    [Fact]
    public void CaptureOwnsAllCollectionsAndExcludesTrustProof()
    {
        RouteCatalogActivation activation = Candidate();
        RouteCatalogBindingInput.TryCapture(activation, out var captured).ShouldBeTrue();
        RouteCatalogActivation owned = captured!.Activation;
        owned.TrustProof.ShouldBe(string.Empty);
        owned.CanonicalCatalog.ShouldNotBeSameAs(activation.CanonicalCatalog);
        owned.RequiredHosts.ShouldNotBeSameAs(activation.RequiredHosts);
        owned.AuxiliaryOperations![0].Http!.Grants.ShouldNotBeSameAs(activation.AuxiliaryOperations![0].Http!.Grants);
        owned.ProjectionCapabilities![0].ProjectionTypes.ShouldNotBeSameAs(activation.ProjectionCapabilities![0].ProjectionTypes);
        owned.TechnicalHttpReceivers![0].Endpoints.ShouldNotBeSameAs(activation.TechnicalHttpReceivers![0].Endpoints);
        owned.TechnicalHttpReceivers[0].Endpoints[0].Methods.ShouldNotBeSameAs(activation.TechnicalHttpReceivers[0].Endpoints[0].Methods);
        owned.TechnicalHttpReceivers[0].Endpoints[0].Policies.ShouldNotBeSameAs(activation.TechnicalHttpReceivers[0].Endpoints[0].Policies);
        owned.TechnicalHttpReceivers[0].Invocations![0].HttpMethods.ShouldNotBeSameAs(activation.TechnicalHttpReceivers[0].Invocations![0].HttpMethods);
    }

    /// <summary>Finite entry and byte bounds fall back without altering successful results or errors.</summary>
    [Fact]
    public void BoundsAndDisposalRetainOrdinaryFallback()
    {
        RouteCatalogActivation activation = Candidate();
        using var memo = new RouteCatalogBindingMemo();
        using var scope = RouteCatalogBindingScope.Enter(memo);
        for (int index = 0; index < 5; index++)
        {
            var current = activation with { AttemptId = "bounded-" + index };
            RouteCatalogActivationBinding.ComputeDigest(current).ShouldBe(RouteCatalogActivationBinding.ComputeDigestCore(current));
        }
        memo.Count.ShouldBe(3);
        memo.RetainedBytes.ShouldBeInRange(1, 8 * 1024 * 1024);
        var oversized = activation with { Owner = new string('x', 2 * 1024 * 1024) };
        RouteCatalogBindingInput.TryCapture(oversized, out _).ShouldBeFalse();
        RouteCatalogActivationBinding.ComputeDigest(oversized).ShouldBe(RouteCatalogActivationBinding.ComputeDigestCore(oversized));
        memo.Count.ShouldBe(3);
        memo.Dispose();
        memo.Count.ShouldBe(0);
        memo.RetainedBytes.ShouldBe(0);
        RouteCatalogActivationBinding.ComputeDigest(activation).ShouldBe(RouteCatalogActivationBinding.ComputeDigestCore(activation));
        memo.Count.ShouldBe(0);
    }

    /// <summary>The cumulative byte ceiling can refuse a third valid graph before reaching the entry ceiling.</summary>
    [Fact]
    public void TotalByteLimitBoundsRetainedLargeInputs()
    {
        RouteCatalogActivation activation = Candidate(false) with { Owner = new string('x', 1500 * 1024) };
        using var memo = new RouteCatalogBindingMemo();
        using var scope = RouteCatalogBindingScope.Enter(memo);
        for (int index = 0; index < 3; index++)
        {
            var current = activation with { AttemptId = "large-" + index };
            RouteCatalogActivationBinding.ComputeDigest(current).ShouldBe(RouteCatalogActivationBinding.ComputeDigestCore(current));
        }
        memo.Count.ShouldBe(2);
        memo.RetainedBytes.ShouldBeInRange(5 * 1024 * 1024, 8 * 1024 * 1024);
    }

    /// <summary>A valid collection outside the closed capture set retains the original calculation without retaining it.</summary>
    [Fact]
    public void UnsupportedCollectionUsesUnchangedComputation()
    {
        RouteCatalogActivation activation = Candidate();
        activation = activation with { RequiredHosts = activation.RequiredHosts.ToImmutableArray() };
        using var memo = new RouteCatalogBindingMemo();
        using var scope = RouteCatalogBindingScope.Enter(memo);
        RouteCatalogBindingInput.TryCapture(activation, out _).ShouldBeFalse();
        RouteCatalogActivationBinding.ComputeDigest(activation).ShouldBe(RouteCatalogActivationBinding.ComputeDigestCore(activation));
        memo.Count.ShouldBe(0);
    }

    /// <summary>Closing a flowed scope disables its child context even while the child still holds its AsyncLocal reference.</summary>
    [Fact]
    public async Task ClosedScopeAndForeignOwnerNeverBorrowRetainedResults()
    {
        RouteCatalogActivation activation = Candidate();
        using var first = new RouteCatalogBindingMemo();
        using var second = new RouteCatalogBindingMemo();
        var outer = RouteCatalogBindingScope.Enter(first);
        RouteCatalogActivationBinding.ComputeDigest(activation);
        using (RouteCatalogBindingScope.Suppress())
        {
            RouteCatalogBindingScope.Current.ShouldBeNull();
            using var inner = RouteCatalogBindingScope.Enter(second);
            RouteCatalogActivationBinding.ComputeDigest(activation);
            inner.CacheHits.ShouldBe(0);
        }
        RouteCatalogBindingScope.Current.ShouldBeSameAs(outer);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task child = Task.Run(async () =>
        {
            await resume.Task;
            RouteCatalogBindingScope.Current.ShouldBeSameAs(outer);
            RouteCatalogActivationBinding.ComputeDigest(activation).ShouldBe(RouteCatalogActivationBinding.ComputeDigestCore(activation));
            outer.CacheHits.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
        outer.Dispose();
        resume.SetResult();
        await child;
        RouteCatalogBindingScope.Current.ShouldBeNull();
        first.Count.ShouldBe(1);
        second.Count.ShouldBe(1);
    }

    /// <summary>Concurrent independent scopes share only successful immutable computations on the explicitly supplied owner memo.</summary>
    [Fact]
    public async Task ConcurrentScopesKeepIndependentLifetimesAndExactDigests()
    {
        RouteCatalogActivation activation = Candidate();
        string expected = RouteCatalogActivationBinding.ComputeDigestCore(activation);
        using var memo = new RouteCatalogBindingMemo();
        await Task.WhenAll(Enumerable.Range(0, 24).Select(index => Task.Run(() =>
        {
            using var scope = RouteCatalogBindingScope.Enter(memo);
            RouteCatalogActivationBinding.ComputeDigest(activation).ShouldBe(expected);
            RouteCatalogActivationBinding.ComputeDigest(activation with { TrustProof = "proof-" + index }).ShouldBe(expected);
            scope.CacheHits.ShouldBeGreaterThanOrEqualTo(1);
        }, TestContext.Current.CancellationToken)));
        memo.Count.ShouldBe(1);
        RouteCatalogBindingScope.Current.ShouldBeNull();
    }

    /// <summary>Records full capture/comparison cost against the same warm decoder baseline; timing is evidence, not a flaky assertion.</summary>
    [Fact]
    public void MeasureCompleteWarmReuseIncludingCaptureAndComparison()
    {
        foreach (bool technical in new[] { false, true })
        {
            RouteCatalogActivation activation = Candidate(technical);
            using var decoding = RouteCatalogDecodeScope.Enter();
            using var scope = RouteCatalogBindingScope.Enter();
            for (int warmup = 0; warmup < 12; warmup++)
            {
                RouteCatalogActivationBinding.ComputeDigestCore(activation);
                RouteCatalogActivationBinding.ComputeDigest(activation);
            }
            var measurements = new List<object>();
            for (int sample = 0; sample < 5; sample++)
            {
                (double elapsed, long bytes) Measure(Action action)
                {
                    long allocated = GC.GetAllocatedBytesForCurrentThread();
                    long started = Stopwatch.GetTimestamp();
                    for (int iteration = 0; iteration < 40; iteration++) action();
                    return (Stopwatch.GetElapsedTime(started).TotalMilliseconds / 40,
                        (GC.GetAllocatedBytesForCurrentThread() - allocated) / 40);
                }
                var plain = Measure(() => RouteCatalogActivationBinding.ComputeDigestCore(activation));
                var reused = Measure(() => RouteCatalogActivationBinding.ComputeDigest(activation));
                measurements.Add(new { sample, baselineMs = plain.elapsed, capturedReuseMs = reused.elapsed,
                    baselineAllocatedBytes = plain.bytes, capturedReuseAllocatedBytes = reused.bytes });
            }
            output.WriteLine(JsonSerializer.Serialize(new { technical, catalogBytes = activation.CanonicalCatalog.Length, measurements }));
        }
    }

    private static RouteCatalogActivation Candidate(bool technical = true)
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        RouteCatalogActivation candidate = fixture.Candidate();
        if (!technical) return candidate;
        string hash = new('a', 64), app = candidate.RequiredHosts[0].AppId;
        RouteContractDeclaration declaration = fixture.Envelope.Declarations.First(item => item.Kind == "projection");
        RouteCatalogEnvelope envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        { Routes = fixture.Envelope.Routes.Select(item => item.RouteId == declaration.RouteId ? item with { Method = "project/v2" } : item).ToArray() });
        RouteCatalogEntry target = envelope.Routes.First(item => item.RouteId == declaration.RouteId);
        candidate = candidate with
        {
            CanonicalCatalog = RouteCatalogCodec.Encode(envelope), RootDigest = RouteCatalogCodec.ComputeRootDigest(envelope),
            AuxiliaryOperations = new RouteCatalogAuxiliaryOperation[] { new(app, "api/owned", "owned-http", "sample")
            { Http = new("Owner", "GET", hash) { Grants = new RouteCatalogHttpGrant[] { new("api/owned", "GET") } } },
                new(app, "native", "sdk-native-actor:health", "sample") { NativeActor = new("health", "GET", hash, hash, hash) },
                new(app, "dapr/subscribe", "sdk-dapr-callback:subscribe", "sample") { DaprCallback = new("subscribe", "GET", hash, hash, hash) },
                new(app, "owner/read", "owner-read", "sample") { DomainAuxiliary = new("Owner", "Request", "Scope", hash) },
                new(app, "owner/events", "sdk-sidecar:domain-events", "sample") { Sidecar = new("Owner", hash, "pubsub", "events") } },
            ProjectionCapabilities = new RouteCatalogProjectionCapability[] { new(target.AppId, declaration.Domain, target.ContractVersion, "owned-capability", new[] { declaration.ProjectionType! }) },
            TechnicalHttpReceivers = new RouteCatalogTechnicalHttpReceiver[] { new(app, RouteCatalogTechnicalHttpPolicy.FenceVersion,
                new RouteCatalogTechnicalHttpEndpoint[] { new("/api/owned", new[] { "GET" }, "Owned.Action", hash, hash, new[] { "EventStoreHuman" }, hash, hash) })
                { Invocations = new RouteCatalogDeclaredInvocation[] { new("caller", "api/owned", new[] { "GET" }) } } },
        };
        _ = RouteCatalogActivationBinding.ComputeDigestCore(candidate);
        return candidate;
    }

    private static RouteCatalogActivation Change(RouteCatalogActivation activation, string changed)
    {
        string hash = new('b', 64);
        var hosts = (RouteCatalogHostIdentity[])activation.RequiredHosts;
        var auxiliary = (RouteCatalogAuxiliaryOperation[])activation.AuxiliaryOperations!;
        var capabilities = (RouteCatalogProjectionCapability[])activation.ProjectionCapabilities!;
        var receivers = (RouteCatalogTechnicalHttpReceiver[])activation.TechnicalHttpReceivers!;
        var endpoints = (RouteCatalogTechnicalHttpEndpoint[])receivers[0].Endpoints;
        var invocations = (RouteCatalogDeclaredInvocation[])receivers[0].Invocations!;
        switch (changed)
        {
            case "attempt": return activation with { AttemptId = "changed-attempt" };
            case "catalog": activation.CanonicalCatalog[0] ^= 1; break;
            case "root": return activation with { RootDigest = hash };
            case "predecessor": return activation with { PredecessorRootDigest = hash };
            case "owner": return activation with { Owner = "changed-owner" };
            case "host": hosts[0] = hosts[0] with { HostId = "changed-host" }; break;
            case "host-app": hosts[0] = hosts[0] with { AppId = "changed-app" }; break;
            case "host-inventory": hosts[0] = hosts[0] with { InventoryDigest = hash }; break;
            case "host-topology": hosts[0] = hosts[0] with { TopologyDigest = hash }; break;
            case "host-authentication": hosts[0] = hosts[0] with { AuthenticationFingerprint = "changed-authentication" }; break;
            case "host-executable": hosts[0] = hosts[0] with { ExecutableDigest = hash }; break;
            case "aux-app": auxiliary[0] = auxiliary[0] with { AppId = "changed-app" }; break;
            case "aux-method": auxiliary[0] = auxiliary[0] with { Method = "changed-method" }; break;
            case "aux-operation": auxiliary[0] = auxiliary[0] with { Operation = "changed-operation" }; break;
            case "aux-domain": auxiliary[0] = auxiliary[0] with { Domain = "changed-domain" }; break;
            case "http-owner": auxiliary[0] = auxiliary[0] with { Http = auxiliary[0].Http! with { Owner = "changed-owner" } }; break;
            case "http-verbs": auxiliary[0] = auxiliary[0] with { Http = auxiliary[0].Http! with { Verbs = "POST" } }; break;
            case "http-code": auxiliary[0] = auxiliary[0] with { Http = auxiliary[0].Http! with { CodeDigest = hash } }; break;
            case "http-grants": ((RouteCatalogHttpGrant[])auxiliary[0].Http!.Grants!)[0] = new("changed-method", "GET"); break;
            case "native": auxiliary[1] = auxiliary[1] with { NativeActor = auxiliary[1].NativeActor! with { InventoryDigest = hash } }; break;
            case "callback": auxiliary[2] = auxiliary[2] with { DaprCallback = auxiliary[2].DaprCallback! with { FrameworkDigest = hash } }; break;
            case "domain-auxiliary": auxiliary[3] = auxiliary[3] with { DomainAuxiliary = auxiliary[3].DomainAuxiliary! with { ScopeMethod = "changed-scope" } }; break;
            case "sidecar": auxiliary[4] = auxiliary[4] with { Sidecar = auxiliary[4].Sidecar! with { TopicName = "changed-topic" } }; break;
            case "cap-app": capabilities[0] = capabilities[0] with { AppId = "changed-app" }; break;
            case "cap-domain": capabilities[0] = capabilities[0] with { Domain = "changed-domain" }; break;
            case "cap-version": capabilities[0] = capabilities[0] with { ContractVersion = "V77" }; break;
            case "cap-fingerprint": capabilities[0] = capabilities[0] with { Fingerprint = "changed-fingerprint" }; break;
            case "cap-types": ((string[])capabilities[0].ProjectionTypes)[0] = "changed-projection"; break;
            case "receiver-app": receivers[0] = receivers[0] with { AppId = "changed-app" }; break;
            case "receiver-fence": receivers[0] = receivers[0] with { FenceVersion = "changed-fence" }; break;
            case "endpoint-pattern": endpoints[0] = endpoints[0] with { Pattern = "/changed" }; break;
            case "endpoint-methods": ((string[])endpoints[0].Methods)[0] = "POST"; break;
            case "endpoint-action": endpoints[0] = endpoints[0] with { ActionIdentity = "Changed.Action" }; break;
            case "endpoint-request": endpoints[0] = endpoints[0] with { RequestDigest = hash }; break;
            case "endpoint-response": endpoints[0] = endpoints[0] with { ResponseDigest = hash }; break;
            case "endpoint-policies": ((string[])endpoints[0].Policies)[0] = "AdminReadOnly"; break;
            case "endpoint-policy-digest": endpoints[0] = endpoints[0] with { PolicyDigest = hash }; break;
            case "endpoint-filter": endpoints[0] = endpoints[0] with { FilterDigest = hash }; break;
            case "endpoint-credential": endpoints[0] = endpoints[0] with { CredentialKind = "workload" }; break;
            case "invocation-caller": invocations[0] = invocations[0] with { CallerAppId = "changed-caller" }; break;
            case "invocation-method": invocations[0] = invocations[0] with { Method = "changed-method" }; break;
            case "invocation-verbs": ((string[])invocations[0].HttpMethods)[0] = "DELETE"; break;
            case "null-host": hosts[0] = null!; break;
            case "null-auxiliary": auxiliary[0] = null!; break;
            case "null-capability-types": capabilities[0] = capabilities[0] with { ProjectionTypes = null! }; break;
            case "null-endpoint-methods": endpoints[0] = endpoints[0] with { Methods = null! }; break;
            case "null-invocation-verbs": invocations[0] = invocations[0] with { HttpMethods = null! }; break;
            default: throw new ArgumentOutOfRangeException(nameof(changed));
        }
        return activation;
    }
}
