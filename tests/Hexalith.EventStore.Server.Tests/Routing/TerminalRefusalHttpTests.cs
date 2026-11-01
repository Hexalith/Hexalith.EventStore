using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Terminal refusals retain real transport, committed scope, bounded output and effect fences.</summary>
public sealed class TerminalRefusalHttpTests
{
    [Fact]
    public async Task ExactUnsupportedCommandReturnsOneBoundedRejectionWithoutExecuting()
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        await fixture.StartAsync();
        using var request = fixture.Request(TerminalRefusalHttpFixture.Command());
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        bytes.Length.ShouldBeLessThan(1024);
        using var json = JsonDocument.Parse(bytes);
        json.RootElement.GetProperty("isRejection").GetBoolean().ShouldBeTrue();
        var item = json.RootElement.GetProperty("events").EnumerateArray().ShouldHaveSingleItem();
        item.GetProperty("eventTypeName").GetString().ShouldBe("terminal-refusal-rejected");
        using var rejection = JsonDocument.Parse(item.GetProperty("payload").GetBytesFromBase64());
        rejection.RootElement.GetProperty("Reason").GetString().ShouldBe("unsupported");
        json.RootElement.GetProperty("resultPayload").ValueKind.ShouldBe(JsonValueKind.Null);
        json.RootElement.TryGetProperty("postCommandStateJson", out _).ShouldBeFalse();
        await fixture.AssertNoEffectsAsync(1);
    }

    [Fact]
    public async Task KnownCommittedImplicitV1CommandCanBeRejectedWithoutAnyAdmissionOrStateEffects()
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        await fixture.StartAsync(knownRule: true);
        using var request = fixture.Request(TerminalRefusalHttpFixture.Command("increment-counter"));
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("isRejection").GetBoolean().ShouldBeTrue();
        json.RootElement.GetProperty("events").GetArrayLength().ShouldBe(1);
        await fixture.AssertNoEffectsAsync(1);
    }

    [Theory]
    [InlineData("anonymous", 401)]
    [InlineData("invalid-channel", 401)]
    [InlineData("wrong-caller", 401)]
    [InlineData("wrong-operation", 403)]
    [InlineData("missing-surface", 503)]
    [InlineData("non-service", 401)]
    [InlineData("duplicate-surface", 401)]
    [InlineData("missing-root", 503)]
    [InlineData("duplicate-root", 503)]
    [InlineData("wrong-root", 503)]
    [InlineData("wrong-epoch", 503)]
    [InlineData("missing-binding", 503)]
    [InlineData("duplicate-binding", 503)]
    [InlineData("wrong-binding", 503)]
    [InlineData("pending", 503)]
    [InlineData("no-generation", 503)]
    [InlineData("wrong-host", 503)]
    [InlineData("wrong-domain", 503)]
    [InlineData("wrong-tenant", 503)]
    public async Task InvalidAuthorityNeverInvokesRefusal(string scenario, int status)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        await using var fixture = new TerminalRefusalHttpFixture();
        await fixture.StartAsync(activate: scenario != "no-generation", appId: scenario == "wrong-host" ? "eventstore" : "sample", wrongIdentity: scenario == "wrong-host");
        if (scenario == "anonymous") fixture.Client.DefaultRequestHeaders.Clear();
        if (scenario == "invalid-channel")
        {
            fixture.Client.DefaultRequestHeaders.Remove(DaprAppChannelToken.HeaderName);
            fixture.Client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)));
        }
        if (scenario == "wrong-caller") fixture.Cohort.Workload(fixture.Client, "domain-service:process", caller: "untrusted");
        if (scenario == "wrong-operation") fixture.Cohort.Workload(fixture.Client, "domain-service:query");
        if (scenario is "missing-surface" or "non-service" or "duplicate-surface")
            fixture.Cohort.Workload(fixture.Client, "domain-service:process", scenario == "missing-surface" ? null : scenario == "non-service" ? "ui" : "service",
                duplicate: scenario == "duplicate-surface");
        if (scenario == "pending")
            await fixture.Catalog.Authorities[0].PrepareAsync(fixture.Successor());
        using var request = fixture.Request(TerminalRefusalHttpFixture.Command(
            tenant: scenario == "wrong-tenant" ? "tenant-b" : "tenant-a", domain: scenario == "wrong-domain" ? "unowned" : "counter"));
        string header = scenario.Contains("root", StringComparison.Ordinal) ? RouteCatalogInvocationHandler.RootHeader
            : scenario.Contains("epoch", StringComparison.Ordinal) ? RouteCatalogInvocationHandler.EpochHeader : RouteCatalogInvocationHandler.BindingHeader;
        if (scenario.StartsWith("missing-", StringComparison.Ordinal) && scenario != "missing-surface") request.Headers.Remove(header);
        if (scenario.StartsWith("duplicate-", StringComparison.Ordinal) && scenario != "duplicate-surface")
            request.Headers.TryAddWithoutValidation(header, request.Headers.GetValues(header).Single());
        if (scenario is "wrong-root" or "wrong-epoch" or "wrong-binding")
        {
            request.Headers.Remove(header);
            request.Headers.TryAddWithoutValidation(header, scenario == "wrong-epoch" ? "999" : new string('0', 64));
        }
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        ((int)response.StatusCode).ShouldBe(status);
        await fixture.AssertNoEffectsAsync(0);
    }

    [Theory]
    [InlineData("unregistered", "unsupported", 503, 0)]
    [InlineData("no-opinion", "unsupported", 503, 1)]
    [InlineData("different-case", "Unsupported", 503, 0)]
    [InlineData("normal-success", "increment-counter", 200, 0)]
    [InlineData("known-no-opinion", "increment-counter", 200, 1)]
    public async Task MissingOpinionRetainsExactNormalResolution(string mode, string command, int status, int callbacks)
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        fixture.Rule = (_, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion);
        await fixture.StartAsync(registerRule: mode != "unregistered", knownRule: mode == "known-no-opinion");
        using var request = fixture.Request(TerminalRefusalHttpFixture.Command(command));
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        ((int)response.StatusCode).ShouldBe(status);
        if (mode is "normal-success" or "known-no-opinion")
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            json.RootElement.GetProperty("isRejection").GetBoolean().ShouldBeFalse();
            var item = json.RootElement.GetProperty("events").EnumerateArray().ShouldHaveSingleItem();
            item.GetProperty("eventTypeName").GetString().ShouldBe(typeof(TerminalRefusalOrdinarySucceeded).FullName);
            using var payload = JsonDocument.Parse(item.GetProperty("payload").GetBytesFromBase64());
            payload.RootElement.GetProperty("Value").GetInt32().ShouldBe(7);
            fixture.Callbacks.ShouldBe(callbacks); fixture.Admissions.ShouldBe(1); fixture.Processors.ShouldBe(1);
            fixture.State.ByteStoreContains("reservation").ShouldBeTrue();
            fixture.State.ByteStoreContains("successful-write").ShouldBeTrue();
            fixture.ProtectedReads.ShouldBe(1);
            (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
        }
        else await fixture.AssertNoEffectsAsync(callbacks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingRuleAndNoOpinionPreserveOrdinaryAdmissionRejectionWithoutItsSerializer(bool noOpinion)
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        fixture.Rule = (_, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion);
        await fixture.StartAsync(knownRule: noOpinion, rejectAdmission: true);
        using var request = fixture.Request(TerminalRefusalHttpFixture.Command("increment-counter"));
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("isRejection").GetBoolean().ShouldBeTrue();
        var item = json.RootElement.GetProperty("events").EnumerateArray().ShouldHaveSingleItem();
        item.GetProperty("eventTypeName").GetString().ShouldBe(typeof(TerminalRefusalOrdinaryRejected).FullName);
        using var payload = JsonDocument.Parse(item.GetProperty("payload").GetBytesFromBase64());
        payload.RootElement.GetProperty("Reason").GetString().ShouldBe("ordinary-denial");
        fixture.Callbacks.ShouldBe(noOpinion ? 1 : 0); fixture.Admissions.ShouldBe(1); fixture.Processors.ShouldBe(0);
        fixture.ProtectedReads.ShouldBe(0);
        fixture.State.ByteStoreContains("reservation").ShouldBeFalse();
        fixture.State.ByteStoreContains("successful-write").ShouldBeFalse();
        fixture.State.TrySaveByteOperations.ShouldBeEmpty();
        System.Text.Encoding.UTF8.GetString(fixture.State.ByteStoreValue("untouched")).ShouldBe("{\"value\":3}");
        (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("cancel")]
    [InlineData("null")]
    [InlineData("null-event")]
    [InlineData("input-mutation")]
    [InlineData("empty")]
    [InlineData("oversized")]
    [InlineData("malformed")]
    [InlineData("json-null")]
    [InlineData("missing-serializer")]
    [InlineData("oversized-input")]
    [InlineData("oversized-metadata")]
    public async Task BadRuleOrBoundedDataFailsClosedAndReleasesAuthority(string mode)
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        if (mode == "throw") fixture.Rule = (_, _) => throw new InvalidOperationException("unavailable");
        if (mode == "cancel") fixture.Rule = (_, token) => throw new OperationCanceledException(token);
        if (mode == "null") fixture.Rule = (_, _) => Task.FromResult<DomainServiceTerminalRefusal>(null!);
        if (mode == "null-event") fixture.Rule = (_, _) => Task.FromResult(DomainServiceTerminalRefusal.Reject(null!));
        if (mode == "input-mutation") fixture.Rule = (context, _) =>
        {
            ((IList<byte>)context.Payload)[0] = 0;
            return Task.FromResult(DomainServiceTerminalRefusal.NoOpinion);
        };
        if (mode is "empty" or "oversized" or "malformed" or "json-null") fixture.Serializer = (_, stream, token) =>
            stream.WriteAsync(mode switch { "empty" => [], "oversized" => new byte[129], "json-null" => "null"u8.ToArray(), _ => "{"u8.ToArray() }, token).AsTask();
        await fixture.StartAsync(serializer: mode != "missing-serializer");
        DomainServiceRequest body = TerminalRefusalHttpFixture.Command();
        if (mode == "oversized-input") body = body with { Command = body.Command with { Payload = new byte[256 * 1024 + 1] } };
        if (mode == "oversized-metadata") body = body with { Command = body.Command with { UserId = new string('x', 256 * 1024) } };
        using var request = fixture.Request(body);
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await fixture.AssertNoEffectsAsync(mode is "oversized-input" or "oversized-metadata" ? 0 : 1);
    }

    [Theory]
    [InlineData("writer")]
    [InlineData("registry")]
    [InlineData("state-proof")]
    [InlineData("effective-events")]
    [InlineData("admin")]
    [InlineData("explicit-version")]
    [InlineData("required-source")]
    [InlineData("complete-source")]
    [InlineData("human-receipt")]
    [InlineData("snapshot")]
    [InlineData("trusted-origin")]
    [InlineData("snapshot-profile")]
    public async Task NonordinaryModesKeepOriginalCatalogPrecedence(string mode)
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        await fixture.StartAsync(configure: services =>
        {
            if (mode == "required-source") services.AddSingleton(new CompleteCommandSourceVerificationOptions("eventstore", ["counter"]));
        });
        DomainServiceRequest body = Nonordinary(mode, TerminalRefusalHttpFixture.Command());
        using var request = fixture.Request(body);
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await fixture.AssertNoEffectsAsync(0);
    }

    [Theory]
    [InlineData("unknown-local", false, true, "tenant-a", "unknown", 404)]
    [InlineData("owned-elsewhere", false, true, "tenant-a", "counter", 404)]
    [InlineData("known-named", true, true, "tenant-a", "counter", 404)]
    [InlineData("known-tenant", true, true, "tenant-b", "counter", 503)]
    [InlineData("known-metadata", true, false, "tenant-a", "counter", 503)]
    public async Task LegacyProjectionOnlyReturnsEarly404ForUnknownLocalOwnership(string mode, bool namedOnly,
        bool auxiliary, string tenant, string domain, int status)
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        await fixture.StartAsync(namedOnly: namedOnly, legacyAuxiliary: auxiliary, appId: mode == "owned-elsewhere" ? "sample-api" : "sample");
        fixture.Cohort.Workload(fixture.Client, "domain-service:project");
        using var request = fixture.Request(new ProjectionRequest(tenant, domain, "aggregate", []), "project");
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        ((int)response.StatusCode).ShouldBe(status);
        await fixture.AssertNoEffectsAsync(0);
    }

    [Fact]
    public void RegistrationIsFiniteExactAndRejectsDuplicateRules()
    {
        var services = new ServiceCollection();
        services.AddEventStoreDomainServiceTerminalRefusal("counter", "unsupported", (_, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), _ => { });
        Should.Throw<ArgumentException>(() => services.AddEventStoreDomainServiceTerminalRefusal("counter", "unsupported", (_, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), _ => { }));
        Should.Throw<ArgumentException>(() => services.AddEventStoreDomainServiceTerminalRefusal("counter", new string('x', 4097), (_, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), _ => { }));
        services.AddEventStoreDomainServiceTerminalRefusal("counter", "Unsupported", (_, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), _ => { });
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(BoundedV1DomainResultProducer));
        using var provider = services.BuildServiceProvider();
        DomainServiceRequestRouter.HasBoundedV1Producer(provider, "counter").ShouldBeFalse();

        var pairs = new ServiceCollection();
        for (int index = 0; index < 1024; index++)
            pairs.AddEventStoreDomainServiceTerminalRefusal("counter", "operation-" + index,
                (_, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), _ => { });
        pairs.Count(descriptor => descriptor.ServiceType == typeof(DomainServiceTerminalRefusalRule)).ShouldBe(1024);
        Should.Throw<ArgumentException>(() => pairs.AddEventStoreDomainServiceTerminalRefusal("counter", "operation-1024",
            (_, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), _ => { }));
        var names = new ServiceCollection();
        string domainAtLimit = new('é', 512);
        System.Text.Encoding.UTF8.GetByteCount(domainAtLimit).ShouldBe(1024);
        names.AddEventStoreDomainServiceTerminalRefusal(domainAtLimit, new string('x', 4096),
            (_, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), _ => { });
        Should.Throw<ArgumentException>(() => names.AddEventStoreDomainServiceTerminalRefusal(domainAtLimit + "a", "unsupported",
            (_, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), _ => { }));
    }

    [Fact]
    public async Task RefusalProfileCoexistsWithAnExistingOrdinaryProfileWithoutReplacingIt()
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        await fixture.StartAsync(configure: services => services.AddEventStoreBoundedV1DomainSerialization("counter", profile =>
            profile.Add<TerminalRefusalOrdinarySucceeded>("ordinary-success", "json", 128,
                (value, stream, token) => JsonSerializer.SerializeAsync(stream, value, cancellationToken: token))));
        using var ordinaryRequest = fixture.Request(TerminalRefusalHttpFixture.Command("increment-counter"));
        using var ordinaryResponse = await fixture.Client.SendAsync(ordinaryRequest, TestContext.Current.CancellationToken);
        ordinaryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var ordinaryJson = JsonDocument.Parse(await ordinaryResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        ordinaryJson.RootElement.GetProperty("isRejection").GetBoolean().ShouldBeFalse();
        ordinaryJson.RootElement.GetProperty("events").EnumerateArray().ShouldHaveSingleItem()
            .GetProperty("eventTypeName").GetString().ShouldBe("ordinary-success");
        fixture.Callbacks.ShouldBe(0); fixture.Admissions.ShouldBe(1); fixture.Processors.ShouldBe(1);
        int writes = fixture.State.TrySaveByteOperations.Count;
        writes.ShouldBe(2);

        using var refusalRequest = fixture.Request(TerminalRefusalHttpFixture.Command());
        using var refusalResponse = await fixture.Client.SendAsync(refusalRequest, TestContext.Current.CancellationToken);
        refusalResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var refusalJson = JsonDocument.Parse(await refusalResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        refusalJson.RootElement.GetProperty("isRejection").GetBoolean().ShouldBeTrue();
        refusalJson.RootElement.GetProperty("events").EnumerateArray().ShouldHaveSingleItem()
            .GetProperty("eventTypeName").GetString().ShouldBe("terminal-refusal-rejected");
        fixture.Callbacks.ShouldBe(1); fixture.Admissions.ShouldBe(1); fixture.Processors.ShouldBe(1);
        fixture.State.TrySaveByteOperations.Count.ShouldBe(writes);
        (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task SchemaTwoCannotAcquireTheNewRefusalCapability()
    {
        await using var fixture = new TerminalRefusalHttpFixture(schemaThree: false);
        await fixture.StartAsync();
        using var request = fixture.Request(TerminalRefusalHttpFixture.Command());
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await fixture.AssertNoEffectsAsync(0);
    }

    [Fact]
    public async Task SchemaThreeWithoutCompleteActivationBindingCannotInvokeRefusal()
    {
        await using var fixture = new TerminalRefusalHttpFixture(fullBinding: false);
        await fixture.StartAsync();
        using var request = fixture.Request(TerminalRefusalHttpFixture.Command());
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await fixture.AssertNoEffectsAsync(0);
    }

    [Fact]
    public async Task TerminalRefusalNeverInvokesTheEndpointDelegate()
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        await fixture.StartAsync(probeDelegate: true);
        using var request = fixture.Request(TerminalRefusalHttpFixture.Command());
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await fixture.AssertNoEffectsAsync(1);
    }

    [Fact]
    public async Task CallbackCancellationPropagatesAndReleasesAuthority()
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        using var cancellation = new CancellationTokenSource();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Rule = (_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(DomainServiceTerminalRefusal.NoOpinion);
        };
        await fixture.StartAsync(middleware: app => app.Use(async (context, next) =>
        {
            context.RequestAborted = cancellation.Token;
            try { await next(context); }
            finally { finished.TrySetResult(); }
        }));
        using var request = fixture.Request(TerminalRefusalHttpFixture.Command());
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe((HttpStatusCode)499);
        await finished.Task.WaitAsync(TestContext.Current.CancellationToken);
        await fixture.AssertNoEffectsAsync(1);
    }

    [Fact]
    public async Task InstalledUncommittedLegacyReceiverCannotTurnMissingOwnershipInto404OrExecution()
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        int effects = 0;
        var handler = NSubstitute.Substitute.For<IDomainProjectionHandler>();
        NSubstitute.SubstituteExtensions.Returns(handler.Domain, "unowned");
        NSubstitute.SubstituteExtensions.Returns(handler.Project(NSubstitute.Arg.Any<ProjectionRequest>()), _ =>
        { effects++; throw new InvalidOperationException("Uncommitted projection executed."); });
        await fixture.StartAsync(configure: services => services.AddSingleton(handler));
        fixture.Cohort.Workload(fixture.Client, "domain-service:project");
        using var request = fixture.Request(new ProjectionRequest("tenant-a", "unowned", "aggregate", []), "project");
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        effects.ShouldBe(0);
        await fixture.AssertNoEffectsAsync(0);
    }

    [Fact]
    public void NewFixtureSourcesPassTheUnchangedSdkSecretScanner()
    {
        string[] paths = ["tests/Hexalith.EventStore.Server.Tests/Routing/TerminalRefusalHttpTests.cs",
            "tests/Hexalith.EventStore.Server.Tests/Routing/TerminalRefusalHttpFixture.cs",
            "tests/Hexalith.EventStore.Server.Tests/Routing/TerminalRefusalRejected.cs",
            "tests/Hexalith.EventStore.Server.Tests/Routing/TerminalRefusalOrdinarySucceeded.cs",
            "tests/Hexalith.EventStore.Server.Tests/Routing/TerminalRefusalOrdinaryRejected.cs",
            "tests/Hexalith.EventStore.Server.Tests/Routing/TerminalRefusalOrdinaryHttpTests.cs",
            "tests/Hexalith.EventStore.Server.Tests/Routing/TerminalRefusalScopedService.cs",
            "tests/Hexalith.EventStore.DomainService.Tests/DomainServiceTerminalRefusalContextTests.cs",
            "tests/Hexalith.EventStore.DomainService.Tests/DomainServiceTerminalRefusalOptionsTests.cs",
            "tests/Hexalith.EventStore.Server.Tests/DomainServices/DomainServiceWorkloadAssertionHandlerTests.Diagnostics.cs",
            "tests/Hexalith.EventStore.Server.Tests/Routing/TerminalRefusalResponseStream.cs"];
        var scanner = typeof(Security.SecretsProtectionTests).GetMethod("ScanTrackedFiles",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        ((string[])scanner.Invoke(null, [paths, null])!).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("success")]
    [InlineData("write-error")]
    [InlineData("response-cancel")]
    public async Task TerminalResponseRetainsLeaseThroughExecutionAndDrainsOnEveryExit(string mode)
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        using var cancellation = new CancellationTokenSource();
        TerminalRefusalResponseStream? held = null;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await fixture.StartAsync(middleware: app => app.Use(async (context, next) =>
        {
            Stream original = context.Response.Body;
            context.RequestAborted = cancellation.Token;
            held = new TerminalRefusalResponseStream(original, mode == "write-error");
            context.Response.Body = held;
            ready.TrySetResult();
            try { await next(context); }
            finally { context.Response.Body = original; finished.TrySetResult(); }
        }));
        using var request = fixture.Request(TerminalRefusalHttpFixture.Command());
        Task<HttpResponseMessage> send = fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            await held!.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
            (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldHaveSingleItem();
            var nextActivation = fixture.Successor();
            await fixture.Catalog.Authorities[0].PrepareAsync(nextActivation);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Catalog.Authorities[0].ReadyAsync(nextActivation.AttemptId));
            if (mode == "response-cancel") cancellation.Cancel();
        }
        finally { held!.Release.TrySetResult(); }
        using var response = await send;
        response.StatusCode.ShouldBe(mode == "success" ? HttpStatusCode.OK : mode == "response-cancel" ? (HttpStatusCode)499 : HttpStatusCode.InternalServerError);
        await finished.Task.WaitAsync(TestContext.Current.CancellationToken);
        await fixture.AssertNoEffectsAsync(1);
    }

    [Fact]
    public async Task MultiWindowRejectionWriterFailureAfterSuccessfulFlushAbortsPartialResponseAndDrainsLease()
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        fixture.Rule = (_, _) => Task.FromResult(DomainServiceTerminalRefusal.Reject(new TerminalRefusalRejected(new string('x', 100 * 1024))));
        TerminalRefusalResponseStream? failed = null;
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await fixture.StartAsync(rejectionPayloadByteBudget: 128 * 1024, middleware: app => app.Use(async (context, next) =>
        {
            Stream original = context.Response.Body;
            failed = new TerminalRefusalResponseStream(original, fail: true, successfulWritesBeforeFailure: 1);
            failed.Release.TrySetResult();
            context.Response.Body = failed;
            try { await next(context); }
            finally { context.Response.Body = original; finished.TrySetResult(); }
        }));
        using var request = fixture.Request(TerminalRefusalHttpFixture.Command());
        using var response = await fixture.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var input = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        byte[] window = new byte[8192];
        long received = 0;
        await Should.ThrowAsync<IOException>(async () =>
        {
            int count;
            while ((count = await input.ReadAsync(window, TestContext.Current.CancellationToken)) > 0) received += count;
        });
        await finished.Task.WaitAsync(TestContext.Current.CancellationToken);
        received.ShouldBe(64 * 1024);
        failed!.SuccessfulWrites.ShouldBe(1);
        failed.WrittenBytes.ShouldBe(received);
        await fixture.AssertNoEffectsAsync(1);
    }

    [Theory]
    [InlineData("known-v2", 200)]
    [InlineData("version-conflict", 503)]
    [InlineData("target", 503)]
    [InlineData("tenant", 503)]
    [InlineData("step-up", 503)]
    public async Task KnownSelectionsRetainCommittedVersionTargetTenantAndStepUpBeforeMatchingRule(string scenario, int status)
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        await fixture.StartAsync(knownRule: true, knownV2: scenario is "known-v2" or "version-conflict",
            stepUp: scenario == "step-up", knownTarget: scenario == "target" ? "sample-api" : null);
        if (scenario is "known-v2" or "version-conflict")
        {
            var committed = (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed!;
            var envelope = RouteCatalogCodec.Decode(committed.CanonicalCatalog);
            envelope.Declarations.Single(declaration => declaration.RouteId == "counter-increment-counter").ContractVersion.ShouldBe("v2");
            envelope.Routes.ShouldContain(route => route.AppId == "sample" && route.ContractVersion == "v1"
                && envelope.Declarations.Any(declaration => declaration.RouteId == route.RouteId && declaration.Domain == "counter"));
        }
        var body = TerminalRefusalHttpFixture.Command("increment-counter", tenant: scenario == "tenant" ? "tenant-b" : "tenant-a");
        if (scenario == "version-conflict") body = body with { Command = body.Command with { Extensions = new() { [DomainServiceVersion.ExtensionKey] = "v1" } } };
        using var request = fixture.Request(body);
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        ((int)response.StatusCode).ShouldBe(status);
        if (status == 200) await AssertOrdinarySuccessAsync(fixture, response, callbacks: 0);
        else await fixture.AssertNoEffectsAsync(0);
    }

    [Theory]
    [InlineData("increment-counter", 200)]
    [InlineData("unsupported", 503)]
    public async Task LegacyActivationSkipsMatchingRulesAndRetainsOrdinaryGuards(string command, int status)
    {
        await using var fixture = new TerminalRefusalHttpFixture(fullBinding: false);
        await fixture.StartAsync(knownRule: true);
        using var request = fixture.Request(TerminalRefusalHttpFixture.Command(command));
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        ((int)response.StatusCode).ShouldBe(status);
        if (status == 200) await AssertOrdinarySuccessAsync(fixture, response, callbacks: 0);
        else await fixture.AssertNoEffectsAsync(0);
    }

    [Fact]
    public async Task DirectGuardConsumersKeepLeaseSignatureAndNormalAdmissionWithoutRefusal()
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        await fixture.StartAsync(knownRule: true);
        var http = new DefaultHttpContext { RequestServices = fixture.App.Services,
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim("eventstore:surface", "service")], "verified-workload")) };
        http.Request.Path = "/process";
        http.Request.Headers[RouteCatalogInvocationHandler.RootHeader] = fixture.Activation.RootDigest;
        http.Request.Headers[RouteCatalogInvocationHandler.EpochHeader] = "1";
        http.Request.Headers[RouteCatalogInvocationHandler.BindingHeader] = RouteCatalogActivationBinding.ComputeDigest(fixture.Activation);
        IDomainRouteCatalogGuard guard = fixture.App.Services.GetRequiredService<IDomainRouteCatalogGuard>();
        Task<RouteCatalogDispatchLease?> pending = guard.AcquireAsync(new DefaultEndpointFilterInvocationContext(http,
            TerminalRefusalHttpFixture.Command("increment-counter")));
        var lease = (await pending).ShouldNotBeNull();
        lease.Snapshot.RootDigest.ShouldBe(fixture.Activation.RootDigest);
        await lease.DisposeAsync();
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => guard.AcquireAsync(new DefaultEndpointFilterInvocationContext(http,
            TerminalRefusalHttpFixture.Command())));
        await fixture.AssertNoEffectsAsync(0);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public async Task ConfigurationOnlyBudgetBoundsCombinedPayloadAndUtf8MetadataAtExactByte(bool unicode, int excess)
    {
        const int budget = 128;
        await using var fixture = new TerminalRefusalHttpFixture();
        await fixture.StartAsync(commandByteBudget: budget);
        fixture.App.Services.GetRequiredService<IOptions<EventStoreProcessOptions>>().Value.MaxCommandBytesPerEnvelope.ShouldBe(budget);
        fixture.App.Services.GetService<CheckpointedProcessRunner>().ShouldBeNull();
        var command = TerminalRefusalHttpFixture.Command().Command with { UserId = unicode ? "é😀" : "user" };
        string?[] metadata = [command.MessageId, command.TenantId, command.Domain, command.AggregateId, command.CommandType,
            command.CorrelationId, command.CausationId, command.UserId];
        int metadataBytes = metadata.Sum(value => value is null ? 0 : System.Text.Encoding.UTF8.GetByteCount(value));
        command = command with { Payload = new byte[budget - metadataBytes + excess] };
        (command.Payload.Length + metadataBytes).ShouldBe(budget + excess);
        command.Payload.Length.ShouldBeLessThan(budget);
        if (unicode) (command.Payload.Length + metadata.Sum(value => value?.Length ?? 0)).ShouldBeLessThan(budget);
        using var request = fixture.Request(new DomainServiceRequest(command, null));
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(excess == 0 ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        if (excess == 0)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            json.RootElement.GetProperty("isRejection").GetBoolean().ShouldBeTrue();
            json.RootElement.GetProperty("events").GetArrayLength().ShouldBe(1);
        }
        await fixture.AssertNoEffectsAsync(excess == 0 ? 1 : 0);
    }

    [Theory]
    [InlineData("callback", false)]
    [InlineData("callback", true)]
    [InlineData("serializer", false)]
    [InlineData("serializer", true)]
    public async Task DenialAuditRemoteScopeDrainsCompletedCallsAndRetainsDemonstratedAmbiguity(string phase, bool ambiguous)
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        int auditCalls = 0;
        async Task AuditAsync()
        {
            try
            {
                await RouteCatalogRemoteExecution.InvokeAsync(async () =>
                {
                    RouteCatalogRemoteExecution.Current.ShouldNotBeNull();
                    auditCalls++;
                    await Task.Yield();
                    if (ambiguous) throw new TimeoutException("Denial audit completion is ambiguous.");
                });
            }
            catch (TimeoutException) when (ambiguous) { }
        }
        if (phase == "callback") fixture.Rule = async (_, _) =>
        {
            await AuditAsync();
            return DomainServiceTerminalRefusal.Reject(new TerminalRefusalRejected("unsupported"));
        };
        else fixture.Serializer = async (value, stream, token) =>
        {
            await AuditAsync();
            await JsonSerializer.SerializeAsync(stream, value, cancellationToken: token);
        };
        await fixture.StartAsync();
        using var request = fixture.Request(TerminalRefusalHttpFixture.Command());
        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        auditCalls.ShouldBe(1);
        await fixture.AssertNoEffectsAsync(1, retainedLease: ambiguous);
        if (ambiguous)
        {
            var next = fixture.Successor();
            await fixture.Catalog.Authorities[0].PrepareAsync(next);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Catalog.Authorities[0].ReadyAsync(next.AttemptId));
        }
    }

    [Theory]
    [InlineData("complete", 200)]
    [InlineData("cancel", 499)]
    [InlineData("fault", 503)]
    public async Task AsyncSerializerLifetimeHoldsAuthorityAndNeverEscapesFailedPayload(string mode, int status)
    {
        await using var fixture = new TerminalRefusalHttpFixture();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Serializer = async (value, stream, token) =>
        {
            RouteCatalogRemoteExecution.Current.ShouldNotBeNull();
            await JsonSerializer.SerializeAsync(stream, value, cancellationToken: token);
            started.TrySetResult();
            try
            {
                await release.Task.WaitAsync(token);
                RouteCatalogRemoteExecution.Current.ShouldNotBeNull();
                if (mode == "fault") throw new InvalidOperationException("Asynchronous serializer failed.");
            }
            finally { finished.TrySetResult(); }
        };
        await fixture.StartAsync(middleware: app => app.Use((context, next) =>
        { context.RequestAborted = cancellation.Token; return next(context); }));
        using var request = fixture.Request(TerminalRefusalHttpFixture.Command());
        Task<HttpResponseMessage> send = fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        try
        {
            await started.Task.WaitAsync(TestContext.Current.CancellationToken);
            fixture.Callbacks.ShouldBe(1); fixture.Admissions.ShouldBe(0); fixture.Processors.ShouldBe(0);
            (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldHaveSingleItem();
            if (mode == "cancel") cancellation.Cancel();
        }
        finally { release.TrySetResult(); }
        using var response = await send;
        ((int)response.StatusCode).ShouldBe(status);
        if (status != 200) (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
        await finished.Task.WaitAsync(TestContext.Current.CancellationToken);
        await fixture.AssertNoEffectsAsync(1);
    }

    [Theory]
    [InlineData("writer", 500, false)]
    [InlineData("registry", 500, false)]
    [InlineData("state-proof", 500, false)]
    [InlineData("effective-events", 500, false)]
    [InlineData("admin", 200, true)]
    [InlineData("explicit-version", 200, true)]
    [InlineData("required-source", 403, false)]
    [InlineData("complete-source", 403, false)]
    [InlineData("human-receipt", 200, true)]
    [InlineData("snapshot", 200, true)]
    [InlineData("snapshot-profile", 200, true)]
    [InlineData("trusted-origin", 503, false)]
    public async Task DeclaredNonordinaryMatchingRulesRetainBaselineValidationAndEffects(string mode, int status, bool executes)
    {
        string? baseline = null;
        foreach (bool registered in new[] { false, true })
        {
            await using var fixture = new TerminalRefusalHttpFixture();
            await fixture.StartAsync(registerRule: registered, knownRule: registered, configure: services =>
            {
                if (mode == "required-source") services.AddSingleton(new CompleteCommandSourceVerificationOptions("eventstore", ["counter"]));
            });
            using var request = fixture.Request(Nonordinary(mode, TerminalRefusalHttpFixture.Command("increment-counter")));
            using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
            ((int)response.StatusCode).ShouldBe(status);
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            if (executes)
            {
                if (baseline is null) baseline = body;
                else body.ShouldBe(baseline);
            }
            if (status == 500) body.Contains("CapabilityMismatch: this domain route has no authenticated versioned command-state intake.",
                StringComparison.Ordinal).ShouldBeTrue();
            if (executes) await AssertOrdinarySuccessAsync(fixture, response, callbacks: 0);
            else await fixture.AssertNoEffectsAsync(0);
        }
    }

    private static DomainServiceRequest Nonordinary(string mode, DomainServiceRequest body) => mode switch
    {
        "writer" => body with { WriterMode = "v1" },
        "registry" => body with { RegistryFingerprint = "registry" },
        "state-proof" => body with { CommandStateProof = [1] },
        "effective-events" => body with { VerifiedEffectiveEvents = [] },
        "admin" => body with { Command = body.Command with { Extensions = new() { [DomainServiceAdministratorAssertions.GlobalAdminExtensionKey] = "true" } } },
        "explicit-version" => body with { Command = body.Command with { Extensions = new() { [DomainServiceVersion.ExtensionKey] = "v1" } } },
        "complete-source" => body with { CompleteSource = new(body.Command.AggregateIdentity, "epoch", default, default, null!, []) },
        "human-receipt" => body with { HumanAuthorityReceipt = "unverified" },
        "snapshot" => body with { IncludePostCommandState = true },
        "snapshot-profile" => body with { SnapshotCaptureProfileId = "unqualified-profile" },
        "trusted-origin" => body with { TrustedEffectOrigin = new(new("tenant-a", "source", "aggregate", 1, EffectKindCatalog.CascadeExpire, "counter", "aggregate", 0),
            "foreign", "purpose", "cause", "increment-counter", string.Empty) },
        _ => body,
    };

    private static async Task AssertOrdinarySuccessAsync(TerminalRefusalHttpFixture fixture, HttpResponseMessage response, int callbacks)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("isRejection").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("events").GetArrayLength().ShouldBe(1);
        fixture.Callbacks.ShouldBe(callbacks); fixture.Admissions.ShouldBe(1); fixture.Processors.ShouldBe(1);
        fixture.ProtectedReads.ShouldBe(1);
        fixture.State.ByteStoreContains("reservation").ShouldBeTrue();
        fixture.State.ByteStoreContains("successful-write").ShouldBeTrue();
        System.Text.Encoding.UTF8.GetString(fixture.State.ByteStoreValue("untouched")).ShouldBe("{\"value\":3}");
        (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }
}
