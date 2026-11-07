using System.Security.Cryptography;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Approved supplement sections 1-7: neutral transport, authority selection and gated modes; mocks prove classification only.</summary>
public sealed class DaprPayloadProtectionBackendTests
{
    private const string Operation = "01J00000000000000000000001";
    private static readonly PlatformCustodyPayloadProtectionOptions Options = new("payload-keys", "fixture-installation", new('a', 64));

    [Fact]
    public async Task WrapSelectsFreshProfileThenSendsOnlyItsExplicitVersionAndClearsEveryOwnedBuffer()
    {
        var authority = new PayloadBackendTestAuthority();
        var operations = new List<PayloadKeyBackendOperation>();
        var responseBuffers = new List<byte[]>();
        using DaprClient client = Client(request =>
        {
            using var decoded = PayloadKeyBackendCodec.DecodeRequest(request.Data.Span);
            operations.Add(decoded.Operation);
            request.Operation.ShouldBe("payload-key-v1"); request.BindingName.ShouldBe(Options.BindingName);
            if (decoded.Operation == PayloadKeyBackendOperation.Wrap) { decoded.ProviderKeyVersionId.ShouldBe("fixture:key:v2"); decoded.Bytes!.Length.ShouldBe(32); }
            byte[] bytes = Respond(decoded, request.Data.Span);
            responseBuffers.Add(bytes); return new(request, bytes, new Dictionary<string, string>());
        });
        var backend = new DaprPayloadProtectionBackend(client, Options, authority);
        byte[] caller = Enumerable.Repeat((byte)0x83, 32).ToArray();
        using var result = await backend.WrapAsync(Operation, caller, default);
        result.Failure.ShouldBe(PayloadBackendFailure.None);
        result.ProviderKeyVersionId.ShouldBe("fixture:key:v2");
        byte[] wrapped = result.TakeBytes(); wrapped.Length.ShouldBe(384); wrapped.ShouldAllBe(b => b == 0x59);
        operations.ShouldBe([PayloadKeyBackendOperation.Profile, PayloadKeyBackendOperation.Wrap]);
        caller.ShouldAllBe(b => b == 0x83);
        authority.Inputs.ShouldAllBe(bytes => bytes.All(b => b == 0));
        authority.Encoded.ShouldAllBe(bytes => bytes.ToArray().All(b => b == 0));
        responseBuffers.ShouldAllBe(bytes => bytes.All(b => b == 0));
        CryptographicOperations.ZeroMemory(caller); CryptographicOperations.ZeroMemory(wrapped);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactRewrapAndHistoricalUnwrapNeverRefreshOrRedirectVersions(bool unwrap)
    {
        int calls = 0;
        using DaprClient client = Client(request =>
        {
            calls++;
            using var decoded = PayloadKeyBackendCodec.DecodeRequest(request.Data.Span);
            decoded.Operation.ShouldBe(unwrap ? PayloadKeyBackendOperation.Unwrap : PayloadKeyBackendOperation.WrapExact);
            decoded.ProviderKeyVersionId.ShouldBe("fixture:key:historical");
            return new(request, Respond(decoded, request.Data.Span), new Dictionary<string, string>());
        });
        var backend = new DaprPayloadProtectionBackend(client, Options, new PayloadBackendTestAuthority());
        using var result = unwrap ? await backend.UnwrapAsync(Operation, "fixture:key:historical", new byte[384], default)
            : await backend.WrapExactAsync(Operation, "fixture:key:historical", new byte[32], default);
        result.Failure.ShouldBe(PayloadBackendFailure.None); calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(PayloadKeyBackendOutcome.Unavailable, (int)PayloadBackendFailure.Unavailable)]
    [InlineData(PayloadKeyBackendOutcome.Denied, (int)PayloadBackendFailure.Denied)]
    [InlineData(PayloadKeyBackendOutcome.Missing, (int)PayloadBackendFailure.Missing)]
    [InlineData(PayloadKeyBackendOutcome.Invalidated, (int)PayloadBackendFailure.Invalidated)]
    [InlineData(PayloadKeyBackendOutcome.ConsistencyMismatch, (int)PayloadBackendFailure.ConsistencyMismatch)]
    [InlineData(PayloadKeyBackendOutcome.Ambiguous, (int)PayloadBackendFailure.Ambiguous)]
    public async Task AuthenticatedClosedOutcomesMapWithoutProviderText(PayloadKeyBackendOutcome outcome, int failure)
    {
        using DaprClient client = Client(request =>
        {
            using var decoded = PayloadKeyBackendCodec.DecodeRequest(request.Data.Span);
            return new(request, Respond(decoded, request.Data.Span, outcome), new Dictionary<string, string>());
        });
        var backend = new DaprPayloadProtectionBackend(client, Options, new PayloadBackendTestAuthority());
        using var result = await backend.WrapExactAsync(Operation, "fixture:key:v1", new byte[32], default);
        result.Failure.ShouldBe((PayloadBackendFailure)failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResponseLossStaysAmbiguousForMutationsAndUnavailableForReads(bool unwrap)
    {
        using DaprClient client = Client(_ => throw new InvalidOperationException("PRIVATE_PROVIDER_ERROR_CANARY"));
        var backend = new DaprPayloadProtectionBackend(client, Options, new PayloadBackendTestAuthority());
        using var result = unwrap ? await backend.UnwrapAsync(Operation, "fixture:key:v1", new byte[384], default)
            : await backend.WrapExactAsync(Operation, "fixture:key:v1", new byte[32], default);
        result.Failure.ShouldBe(unwrap ? PayloadBackendFailure.Unavailable : PayloadBackendFailure.Ambiguous);
        result.ToString().ShouldBe(nameof(PayloadBackendResult));
    }

    [Fact]
    public async Task RotationOrResponseIdentityDisagreementFailsWithoutAlternativeVersion()
    {
        using DaprClient client = Client(request =>
        {
            using var decoded = PayloadKeyBackendCodec.DecodeRequest(request.Data.Span);
            using var response = Response(decoded, request.Data.Span) with { ProviderKeyVersionId = "fixture:key:other" };
            return new(request, PayloadKeyBackendCodec.EncodeResponse(response), new Dictionary<string, string>());
        });
        var backend = new DaprPayloadProtectionBackend(client, Options, new PayloadBackendTestAuthority());
        using var result = await backend.WrapExactAsync(Operation, "fixture:key:v1", new byte[32], default);
        result.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch);
    }

    [Fact]
    public async Task MissingKeyLookupCannotResolveAnUncertainOriginalMutation()
    {
        int calls = 0;
        using DaprClient client = Client(request =>
        {
            calls++;
            using var decoded = PayloadKeyBackendCodec.DecodeRequest(request.Data.Span);
            decoded.Operation.ShouldBe(PayloadKeyBackendOperation.Lookup); decoded.Bytes.ShouldBeNull();
            return new(request, Respond(decoded, request.Data.Span, PayloadKeyBackendOutcome.Missing), new Dictionary<string, string>());
        });
        var authority = new PayloadBackendTestAuthority();
        var backend = new DaprPayloadProtectionBackend(client, Options, authority);
        using var uncertain = await backend.ReconcileAsync(Operation, default);
        uncertain.Failure.ShouldBe(PayloadBackendFailure.Ambiguous);
        authority.OriginalAbsent = true;
        using var proven = await backend.ReconcileAsync(Operation, default);
        proven.Failure.ShouldBe(PayloadBackendFailure.Missing); calls.ShouldBe(2);
    }

    [Fact]
    public async Task UnauthenticatedResponseCannotReturnKeyMaterial()
    {
        var authority = new PayloadBackendTestAuthority { Authenticate = false };
        using DaprClient client = Client(request =>
        {
            using var decoded = PayloadKeyBackendCodec.DecodeRequest(request.Data.Span);
            return new(request, Respond(decoded, request.Data.Span), new Dictionary<string, string>());
        });
        var backend = new DaprPayloadProtectionBackend(client, Options, authority);
        using var result = await backend.UnwrapAsync(Operation, "fixture:key:v1", new byte[384], default);
        result.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch);
        Should.Throw<PayloadProtectionFormatException>(() => result.TakeBytes());
        authority.Encoded.ShouldAllBe(bytes => bytes.ToArray().All(b => b == 0));
    }

    [Fact]
    public async Task ProfileAndWrapShareOneTotalBudgetAndClearALateSuccessfulResponse()
    {
        var clock = new LifecycleTestClock();
        var authority = new PayloadBackendTestAuthority();
        int calls = 0;
        using DaprClient client = Client(request =>
        {
            calls++;
            using var decoded = PayloadKeyBackendCodec.DecodeRequest(request.Data.Span);
            clock.Advance(TimeSpan.FromSeconds(6));
            return new(request, Respond(decoded, request.Data.Span), new Dictionary<string, string>());
        });
        var backend = new DaprPayloadProtectionBackend(client, Options, authority, timeProvider: clock);
        using var result = await backend.WrapAsync(Operation, new byte[32], default);
        calls.ShouldBe(2); result.Failure.ShouldBe(PayloadBackendFailure.Ambiguous);
        Should.Throw<PayloadProtectionFormatException>(() => result.TakeBytes());
        authority.Inputs.ShouldAllBe(bytes => bytes.All(b => b == 0));
        authority.Encoded.ShouldAllBe(bytes => bytes.ToArray().All(b => b == 0));
    }

    [Fact]
    public async Task PrivateOriginalAbsenceProofFailureRemainsBoundedAndAmbiguous()
    {
        using DaprClient client = Client(request =>
        {
            using var decoded = PayloadKeyBackendCodec.DecodeRequest(request.Data.Span);
            return new(request, Respond(decoded, request.Data.Span, PayloadKeyBackendOutcome.Missing), new Dictionary<string, string>());
        });
        var authority = new PayloadBackendTestAuthority { Confirming = _ => ValueTask.FromException<bool>(new InvalidOperationException("PRIVATE_PROOF_CANARY")) };
        var backend = new DaprPayloadProtectionBackend(client, Options, authority);
        using var result = await backend.ReconcileAsync(Operation, default);
        result.Failure.ShouldBe(PayloadBackendFailure.Ambiguous);
    }

    [Fact]
    public async Task MissingInstalledAuthorityAndInvalidInputContactNoBinding()
    {
        using DaprClient client = Substitute.For<DaprClient>();
        var authority = new PayloadBackendTestAuthority { Admit = false };
        var backend = new DaprPayloadProtectionBackend(client, Options, authority);
        using var refused = await backend.WrapExactAsync(Operation, "fixture:key:v1", new byte[32], default);
        refused.Failure.ShouldBe(PayloadBackendFailure.Unavailable);
        using var malformed = await backend.WrapAsync(Operation, new byte[33], default);
        malformed.Failure.ShouldBe(PayloadBackendFailure.ConsistencyMismatch);
        await client.DidNotReceive().InvokeBindingAsync(Arg.Any<BindingRequest>(), Arg.Any<CancellationToken>());
        authority.Encoded.ShouldAllBe(bytes => bytes.ToArray().All(b => b == 0));
    }

    [Fact]
    public async Task CallerCancellationWaitsForActualIoCompletionBeforeClearingOwners()
    {
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var authority = new PayloadBackendTestAuthority();
        using DaprClient client = Substitute.For<DaprClient>();
        client.InvokeBindingAsync(Arg.Any<BindingRequest>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            BindingRequest request = call.ArgAt<BindingRequest>(0);
            entered.SetResult();
            await release.Task;
            using var decoded = PayloadKeyBackendCodec.DecodeRequest(request.Data.Span);
            return new BindingResponse(request, Respond(decoded, request.Data.Span), new Dictionary<string, string>());
        });
        var backend = new DaprPayloadProtectionBackend(client, Options, authority);
        Task<PayloadBackendResult> pending = backend.WrapExactAsync(Operation, "fixture:key:v1", new byte[32], cancellation.Token).AsTask();
        await entered.Task; cancellation.Cancel();
        pending.IsCompleted.ShouldBeFalse(); authority.Encoded[0].Span.ContainsAnyExcept((byte)0).ShouldBeTrue();
        release.SetResult();
        await Should.ThrowAsync<OperationCanceledException>(() => pending);
        authority.Inputs.ShouldAllBe(bytes => bytes.All(b => b == 0)); authority.Encoded.ShouldAllBe(bytes => bytes.ToArray().All(b => b == 0));
    }

    [Fact]
    public async Task ExplicitAuthorityProfileSupportsFullReservationAndCompletionWithoutMigratingPredecessor()
    {
        var profile = PayloadProtectionAuthorityProfile.PlatformCustody;
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync(authorityProfile: profile);
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.AcquireCompletionAsync(write.Completion!);
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted);
        WrappedDekV1 key = (await fixture.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion!.KeyReference, 1)))!.Value;
        key.State.ShouldBe("Active");
        FenceV1 fence = (await fixture.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(fixture.Root)))!.Value;
        fence.ApprovedSpecDigest.ShouldBe(profile.Digest); fence.HighestWrittenFormat.ShouldBe("V2"); fence.ApprovalEpoch.ShouldBe(7ul);
        byte[] encoded = PayloadProtectionStateCodec.Encode(fence, profile);
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Decode<FenceV1>(encoded));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(fence with { ApprovedSpecDigest = PayloadProtectionStateKeys.AuthorityDigest }, profile));
        int before = fixture.Store.Commits;
        var incompatible = new PayloadProtectionLeaseCoordinator(fixture.Store, new([new("tenant-a", "parties")]),
            new(new('b', 40), new('c', 64), fixture.Backend.BackendId, ["json", "json+pdenc-v2"], ["V2"]), fixture.Clock);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => incompatible.RegisterAsync(fixture.Identity).AsTask());
        fixture.Store.Commits.ShouldBe(before);
        CryptographicOperations.ZeroMemory(write.Result.PayloadBytes);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Dev")]
    [InlineData("Test")]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("")]
    public void EnabledRemainsBlockedBeforeAnyHostedServiceOrProviderResolution(string environment)
    {
        var services = new ServiceCollection();
        bool resolved = false;
        services.AddSingleton<DaprClient>(_ => { resolved = true; throw new InvalidOperationException(); });
        var config = Configuration("Enabled", "PlatformCustody");
        PayloadProtectionOptions.Load(config).AuthorityProfile.ShouldBeSameAs(PayloadProtectionAuthorityProfile.PlatformCustody);
        services.AddSingleton<IHostEnvironment>(new PayloadProtectionTestEnvironment(environment));
        Should.Throw<InvalidOperationException>(() => services.AddEventStorePayloadProtection(config)).Message.ShouldBe("Payload protection startup admission is unavailable.");
        services.ShouldNotContain(d => d.ServiceType == typeof(IHostedService)); resolved.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Mode", "enabled")]
    [InlineData("Backend", "OpenBao")]
    [InlineData("WriteMode", "fallback")]
    [InlineData("MaxProtectedValueBytes", "1048577")]
    [InlineData("KeyCacheEntries", "4097")]
    [InlineData("KeyCacheTtl", "00:05:01")]
    [InlineData("OperationTimeout", "00:00:31")]
    [InlineData("PlatformCustody:BindingName", "https://private-provider")]
    [InlineData("PlatformCustody:InstallationId", "private/provider")]
    [InlineData("PlatformCustody:ProfileSha256", "INVALID")]
    [InlineData("PlatformCustody:Credentials", "PRIVATE_CANARY")]
    [InlineData("AuthorityDigest", "arbitrary")]
    public void ClosedOptionsRejectWeakUnknownAndProviderPrivateSettings(string field, string value)
    {
        var config = Configuration("Enabled", "PlatformCustody", field, value);
        Should.Throw<InvalidOperationException>(() => PayloadProtectionOptions.Load(config)).Message.ShouldBe("Payload protection options are invalid.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task DisabledDuplicateOrFactoryServiceIsRefusedWithoutInvokingIt(int count)
    {
        var services = new ServiceCollection();
        bool resolved = false;
        for (int index = 0; index < count; index++)
        { services.AddSingleton<IEventPayloadProtectionService>(_ => { resolved = true; throw new InvalidOperationException(); }); }
        services.AddEventStorePayloadProtection(Configuration("Disabled", "stale-private-provider"));
        await using var provider = services.BuildServiceProvider();
        IHostedService guard = provider.GetServices<IHostedService>().Single();
        await Should.ThrowAsync<InvalidOperationException>(() => guard.StartAsync(default)); resolved.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => services.AddEventStorePayloadProtection(Configuration("Disabled", null)));
    }

    [Fact]
    public void ParsedLimitsAreRetainedAndDisabledUnknownProviderFieldsAreRefused()
    {
        var values = new Dictionary<string, string?>
        {
            ["Mode"] = "Enabled", ["Backend"] = "LocalDevelopment", ["MaxProtectedValueBytes"] = "512",
            ["KeyCacheEntries"] = "12", ["KeyCacheTtl"] = "00:00:20", ["OperationTimeout"] = "00:00:03",
            ["WriteMode"] = "CompatibilityLegacy", ["LegacyWriterId"] = "fixture-writer",
        };
        PayloadProtectionOptions options = PayloadProtectionOptions.Load(new PayloadProtectionTestConfiguration(values));
        options.MaxProtectedValueBytes.ShouldBe(512); options.KeyCacheEntries.ShouldBe(12);
        options.KeyCacheTtl.ShouldBe(TimeSpan.FromSeconds(20)); options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(3));
        options.LegacyWriterId.ShouldBe("fixture-writer"); options.AuthorityProfile.ShouldBeSameAs(PayloadProtectionAuthorityProfile.Predecessor);
        Should.Throw<InvalidOperationException>(() => PayloadProtectionOptions.Load(Configuration("Disabled", "PlatformCustody", "PlatformCustody:Credentials", "PRIVATE_CANARY")));
    }

    private static DaprClient Client(Func<BindingRequest, BindingResponse> respond)
    {
        DaprClient client = Substitute.For<DaprClient>();
        client.InvokeBindingAsync(Arg.Any<BindingRequest>(), Arg.Any<CancellationToken>()).Returns(call => Task.FromResult(respond(call.ArgAt<BindingRequest>(0))));
        return client;
    }
    private static byte[] Respond(PayloadKeyBackendRequest request, ReadOnlySpan<byte> encoded, PayloadKeyBackendOutcome outcome = PayloadKeyBackendOutcome.Succeeded)
    {
        using var response = Response(request, encoded) with
        {
            Outcome = outcome,
            Bytes = outcome == PayloadKeyBackendOutcome.Succeeded ? request.Operation is PayloadKeyBackendOperation.Profile or PayloadKeyBackendOperation.Lookup ? null : Enumerable.Repeat((byte)0x59, request.Operation == PayloadKeyBackendOperation.Unwrap ? 32 : 384).ToArray() : null,
            Profile = outcome == PayloadKeyBackendOutcome.Succeeded && request.Operation == PayloadKeyBackendOperation.Profile ? Profile() : null,
            ProviderKeyVersionId = outcome == PayloadKeyBackendOutcome.Succeeded ? request.ProviderKeyVersionId : null,
        };
        return PayloadKeyBackendCodec.EncodeResponse(response);
    }
    private static PayloadKeyBackendResponse Response(PayloadKeyBackendRequest request, ReadOnlySpan<byte> encoded)
        => new(1, request.Operation, request.OperationId, request.RequestId, request.InstallationId, request.ProfileSha256,
            Convert.ToHexStringLower(SHA256.HashData(encoded)), PayloadKeyBackendOutcome.Succeeded, request.ProviderKeyVersionId,
            request.Operation is PayloadKeyBackendOperation.Profile or PayloadKeyBackendOperation.Lookup ? null : new byte[request.Operation == PayloadKeyBackendOperation.Unwrap ? 32 : 384],
            request.Operation == PayloadKeyBackendOperation.Profile ? Profile() : null);
    private static PayloadKeyBackendProfile Profile() => new("rsa-3072", "RSA-OAEP-256", false, false, false, false, false, "fixture:key:v2", Enum.GetValues<PayloadKeyBackendOperation>());
    private static PayloadProtectionTestConfiguration Configuration(string mode, string? backend, string? replace = null, string? value = null)
    {
        var values = new Dictionary<string, string?> { ["Mode"] = mode, ["Backend"] = backend, ["PlatformCustody:BindingName"] = Options.BindingName,
            ["PlatformCustody:InstallationId"] = Options.InstallationId, ["PlatformCustody:ProfileSha256"] = Options.ProfileSha256 };
        if (replace is not null) { values[replace] = value; }
        return new(values);
    }
}
