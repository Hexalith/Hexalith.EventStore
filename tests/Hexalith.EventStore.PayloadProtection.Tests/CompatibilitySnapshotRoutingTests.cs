using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Exercises Story 8.4 snapshot compatibility routing and the v2 snapshot type registry (normative sections 6.1 and
/// 12.3, vectors V117-V118). Vector numbers appear in method names only; the Story 8.3 <c>Vector</c> trait set stays
/// unchanged.
/// </summary>
public sealed class CompatibilitySnapshotRoutingTests
{
    /// <summary>A legacy or unprotected snapshot passes through and only it may use the corrupt-deletion exception.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V117_LegacySnapshot_PassesThroughAsync(bool explicitUnprotected)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        JsonElement state = CompatibilityTestData.Json("{\"Name\":\"Alice\",\"Version\":3}");
        EventStorePayloadProtectionMetadata? metadata = explicitUnprotected ? EventStorePayloadProtectionMetadata.Unprotected() : null;

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(CompatibilityTestData.Snapshot(state, metadata));

        result.IsReadable.ShouldBeTrue();
        result.Route.ShouldBe(explicitUnprotected ? CompatibilityReadRoute.Unprotected : CompatibilityReadRoute.LegacyUnprotected);
        ((JsonElement)result.State!).GetRawText().ShouldBe(state.GetRawText());
        result.Metadata.ShouldBe(explicitUnprotected
            ? EventStorePayloadProtectionMetadata.Unprotected()
            : EventStorePayloadProtectionMetadataCarrier.Legacy());
        result.AllowsCorruptLegacyDeletion.ShouldBeTrue();
        resolver.Calls.ShouldBe(0);
        reader.SnapshotCalls.ShouldBe(0);
    }

    /// <summary>
    /// A v2 snapshot, stored as a JSON element or carried in process, resolves its current identifier, authenticates,
    /// and only then deserializes through the registered metadata.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task V117_V2Snapshot_AuthenticatesThenDeserializesRegisteredTypeAsync(bool storedAsElement)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        ProtectedSnapshotPayloadV2 carrier = CompatibilityTestData.ProtectSnapshot(sequence: 9);
        object state = storedAsElement ? CompatibilityTestData.ToElement(carrier) : carrier;

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(
            CompatibilityTestData.Snapshot(state, CompatibilityTestData.V2Metadata(), sequence: 9));

        result.IsReadable.ShouldBeTrue();
        result.Route.ShouldBe(CompatibilityReadRoute.SharedV2);
        result.State.ShouldBe(new PartySnapshotState("Alice", 3));
        result.Metadata.ShouldBe(EventStorePayloadProtectionMetadata.Unprotected());
        result.AllowsCorruptLegacyDeletion.ShouldBeFalse();
        resolver.Calls.ShouldBe(1);
        reader.SnapshotCalls.ShouldBe(0);
    }

    /// <summary>A v2 carrier element with camelCase member names, as Dapr actor state stores it, routes to the core.</summary>
    [Theory]
    [InlineData("v2-element")]
    [InlineData("camel-case-v2")]
    public async Task V117_V2SnapshotElementMemberCase_RoutesToSharedV2Async(string shape)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, _) = CreateRouter();

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(
            CompatibilityTestData.Snapshot(ShapeFor(shape), CompatibilityTestData.V2Metadata()));

        result.IsReadable.ShouldBeTrue();
        result.Route.ShouldBe(CompatibilityReadRoute.SharedV2);
        result.State.ShouldBe(new PartySnapshotState("Alice", 3));
        resolver.Calls.ShouldBe(1);
    }

    /// <summary>A v2 carrier written and read back with web serializer options is a readable shared v2 snapshot.</summary>
    [Fact]
    public async Task V117_V2SnapshotRoundTrippedWithWebOptions_IsReadableAsync()
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, _) = CreateRouter();
        ProtectedSnapshotPayloadV2 carrier = CompatibilityTestData.ProtectSnapshot(sequence: 6);
        string serialized = JsonSerializer.Serialize(carrier, JsonSerializerOptions.Web);
        JsonElement stored = JsonSerializer.Deserialize<JsonElement>(serialized, JsonSerializerOptions.Web);
        stored.TryGetProperty("snapshotTypeId", out _).ShouldBeTrue();

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(
            CompatibilityTestData.Snapshot(stored, CompatibilityTestData.V2Metadata(), sequence: 6));

        result.IsReadable.ShouldBeTrue();
        result.Route.ShouldBe(CompatibilityReadRoute.SharedV2);
        result.State.ShouldBe(new PartySnapshotState("Alice", 3));
        result.Metadata.ShouldBe(EventStorePayloadProtectionMetadata.Unprotected());
        resolver.Calls.ShouldBe(1);
    }

    /// <summary>A snapshot written under a historical alias is read through that alias without rewriting it.</summary>
    [Fact]
    public async Task V117_V2SnapshotHistoricalAlias_IsAcceptedWithoutRewriteAsync()
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, _) = CreateRouter();
        ProtectedSnapshotPayloadV2 carrier = CompatibilityTestData.ProtectSnapshot(CompatibilityTestData.SnapshotAlias);
        JsonElement stored = CompatibilityTestData.ToElement(carrier);

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(
            CompatibilityTestData.Snapshot(stored, CompatibilityTestData.V2Metadata()));

        result.State.ShouldBe(new PartySnapshotState("Alice", 3));
        resolver.Calls.ShouldBe(1);
        stored.GetProperty("SnapshotTypeId").GetString().ShouldBe(CompatibilityTestData.SnapshotAlias);
    }

    /// <summary>An unknown v2 snapshot type identifier is a consistency mismatch before any key resolution.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task V117_UnknownSnapshotTypeId_IsConsistencyMismatchWithoutResolverAsync(bool withRegistry)
    {
        var resolver = new CountingKeyResolver();
        var router = new PayloadCompatibilityRouter(
            resolver.ResolveAsync,
            snapshotTypes: withRegistry ? CompatibilityTestData.Registry() : null);
        ProtectedSnapshotPayloadV2 carrier = CompatibilityTestData.ProtectSnapshot("hx-snapshot-v1:unregistered-state");
        JsonElement stored = CompatibilityTestData.ToElement(carrier);

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(
            CompatibilityTestData.Snapshot(stored, CompatibilityTestData.V2Metadata()));

        result.Route.ShouldBe(CompatibilityReadRoute.Rejected);
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        result.State.ShouldBeNull();
        result.AllowsCorruptLegacyDeletion.ShouldBeFalse();
        resolver.Calls.ShouldBe(0);
    }

    /// <summary>Authenticated plaintext that does not deserialize to the registered type is a consistency mismatch.</summary>
    [Theory]
    [InlineData("[1,2]")]
    [InlineData("null")]
    [InlineData("{\"Name\":42,\"Version\":3}")]
    public async Task V117_DeserializationFailureAfterAuthentication_IsConsistencyMismatchAsync(string plaintext)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, _) = CreateRouter();
        ProtectedSnapshotPayloadV2 carrier = CompatibilityTestData.ProtectSnapshot(plaintext: Encoding.UTF8.GetBytes(plaintext));

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(
            CompatibilityTestData.Snapshot(carrier, CompatibilityTestData.V2Metadata()));

        result.Route.ShouldBe(CompatibilityReadRoute.SharedV2);
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        result.State.ShouldBeNull();
        result.AllowsCorruptLegacyDeletion.ShouldBeFalse();
        resolver.Calls.ShouldBe(1);
    }

    /// <summary>Every v2 snapshot outcome clears the authenticated plaintext before returning or propagating cancellation.</summary>
    [Theory]
    [InlineData("readable")]
    [InlineData("deserialization-failure")]
    [InlineData("cancellation")]
    [InlineData("out-of-memory")]
    [InlineData("foreign-cancellation")]
    public async Task V118_V2SnapshotPlaintext_IsZeroedForEveryOutcomeAsync(string outcome)
    {
        using var source = new CancellationTokenSource();
        using var foreign = new CancellationTokenSource();
        var observer = new RecordingBufferObserver();
        var resolver = new CountingKeyResolver();
        SnapshotTypeRegistry registry = CompatibilityTestData.Registry();
        if (outcome is "cancellation" or "out-of-memory" or "foreign-cancellation")
        {
            var options = new JsonSerializerOptions { TypeInfoResolver = CompatibilityTestJsonContext.Default };
            options.Converters.Add(outcome switch
            {
                "cancellation" => new CancellingSnapshotConverter(source),
                "out-of-memory" => new ThrowingSnapshotConverter(new OutOfMemoryException()),
                _ => new ThrowingSnapshotConverter(new OperationCanceledException(foreign.Token)),
            });
            registry = new SnapshotTypeRegistry(
                [new SnapshotTypeRegistration(CompatibilityTestData.SnapshotTypeId, options.GetTypeInfo(typeof(PartySnapshotState)))]);
        }

        var router = new PayloadCompatibilityRouter(
            resolver.ResolveAsync,
            snapshotTypes: registry,
            bufferObserver: observer);
        ProtectedSnapshotPayloadV2 carrier = outcome == "deserialization-failure"
            ? CompatibilityTestData.ProtectSnapshot(plaintext: "[1,2]"u8.ToArray())
            : CompatibilityTestData.ProtectSnapshot();
        CompatibilitySnapshotRecord record = CompatibilityTestData.Snapshot(carrier, CompatibilityTestData.V2Metadata());
        if (outcome == "foreign-cancellation")
        {
            foreign.Cancel();
        }

        if (outcome == "cancellation")
        {
            await Should.ThrowAsync<OperationCanceledException>(async () => await router.ReadSnapshotAsync(record, source.Token));
        }
        else if (outcome == "out-of-memory")
        {
            await Should.ThrowAsync<OutOfMemoryException>(async () => await router.ReadSnapshotAsync(record));
        }
        else
        {
            CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(record, source.Token);
            if (outcome == "readable")
            {
                result.State.ShouldBe(new PartySnapshotState("Alice", 3));
            }
            else
            {
                result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
                result.State.ShouldBeNull();
                result.AllowsCorruptLegacyDeletion.ShouldBeFalse();
            }
        }

        resolver.Calls.ShouldBe(1);
        observer.Observed.ShouldBe([SensitiveBufferKind.DecryptedPlaintext]);
    }

    /// <summary>Caller cancellation after a failed core read wins over the unreadable snapshot result.</summary>
    [Fact]
    public async Task V118_CancellationAfterUnreadableV2CoreCompletion_PropagatesAsync()
    {
        using var source = new CancellationTokenSource();
        var coreObserver = new RecordingBufferObserver(kind =>
        {
            if (kind == SensitiveBufferKind.DataEncryptionKey)
            {
                source.Cancel();
            }
        });
        var resolver = new CountingKeyResolver(static _ => new byte[32]);
        var router = new PayloadCompatibilityRouter(
            resolver.ResolveAsync,
            snapshotTypes: CompatibilityTestData.Registry(),
            core: new PayloadProtectionCore(coreObserver));

        await Should.ThrowAsync<OperationCanceledException>(async () => await router.ReadSnapshotAsync(
            CompatibilityTestData.Snapshot(CompatibilityTestData.ProtectSnapshot(), CompatibilityTestData.V2Metadata()),
            source.Token));

        coreObserver.Observed.ShouldContain(SensitiveBufferKind.DataEncryptionKey);
        resolver.Calls.ShouldBe(1);
    }

    /// <summary>The registry resolves exact current identifiers and aliases only.</summary>
    [Fact]
    public void V117_Registry_ResolvesCurrentIdentifierAndAliasOnly()
    {
        SnapshotTypeRegistry registry = CompatibilityTestData.Registry();

        registry.TryResolve(CompatibilityTestData.SnapshotTypeId, out SnapshotTypeRegistration? current).ShouldBeTrue();
        registry.TryResolve(CompatibilityTestData.SnapshotAlias, out SnapshotTypeRegistration? alias).ShouldBeTrue();
        alias.ShouldBeSameAs(current);
        current!.TypeInfo.Type.ShouldBe(typeof(PartySnapshotState));
        registry.TryResolve("HX-SNAPSHOT-V1:PARTY-STATE", out _).ShouldBeFalse();
        registry.TryResolve("hx-snapshot-v1:party-state ", out _).ShouldBeFalse();
        registry.TryResolve(null, out _).ShouldBeFalse();
        registry.Registrations.Count.ShouldBe(1);
    }

    /// <summary>Lookup keys and exposed aliases stay frozen after the caller mutates its alias list.</summary>
    [Fact]
    public void V117_Registry_CopiesCallerAliases()
    {
        var aliases = new List<string> { CompatibilityTestData.SnapshotAlias };
        var registry = new SnapshotTypeRegistry(
            [new SnapshotTypeRegistration(
                CompatibilityTestData.SnapshotTypeId,
                CompatibilityTestJsonContext.Default.PartySnapshotState,
                aliases)]);

        aliases[0] = "hx-snapshot-v1:replacement";
        aliases.Add("hx-snapshot-v1:added");

        registry.TryResolve(CompatibilityTestData.SnapshotAlias, out SnapshotTypeRegistration? registration).ShouldBeTrue();
        registration!.Aliases.ShouldBe([CompatibilityTestData.SnapshotAlias]);
        registry.TryResolve("hx-snapshot-v1:replacement", out _).ShouldBeFalse();
        registry.TryResolve("hx-snapshot-v1:added", out _).ShouldBeFalse();
        Should.Throw<NotSupportedException>(() => ((IList<string>)registration.Aliases)[0] = "hx-snapshot-v1:changed");
    }

    /// <summary>Colliding identifiers or aliases, invalid identifiers, and shared CLR types fail construction.</summary>
    [Theory]
    [InlineData("duplicate-id")]
    [InlineData("alias-equals-own-id")]
    [InlineData("alias-equals-other-id")]
    [InlineData("duplicate-alias")]
    [InlineData("alias-twice-in-one")]
    [InlineData("shared-clr-type")]
    [InlineData("missing-prefix")]
    [InlineData("uppercase")]
    [InlineData("trailing-hyphen")]
    [InlineData("invalid-alias")]
    [InlineData("null-registration")]
    public void V117_Registry_RejectsCollisionsAndInvalidIdentifiers(string defect)
    {
        var state = CompatibilityTestJsonContext.Default.PartySnapshotState;
        var text = CompatibilityTestJsonContext.Default.String;
        const string Current = "hx-snapshot-v1:party-state";
        const string Other = "hx-snapshot-v1:audit-note";
        SnapshotTypeRegistration?[] registrations = defect switch
        {
            "duplicate-id" => [new(Current, state), new(Current, text)],
            "alias-equals-own-id" => [new(Current, state, [Current])],
            "alias-equals-other-id" => [new(Current, state, [Other]), new(Other, text)],
            "duplicate-alias" => [new(Current, state, ["hx-snapshot-v1:old"]), new(Other, text, ["hx-snapshot-v1:old"])],
            "alias-twice-in-one" => [new(Current, state, ["hx-snapshot-v1:old", "hx-snapshot-v1:old"])],
            "shared-clr-type" => [new(Current, state), new(Other, state)],
            "missing-prefix" => [new("party-state-registration", state)],
            "uppercase" => [new("hx-snapshot-v1:Party-State", state)],
            "trailing-hyphen" => [new("hx-snapshot-v1:party-state-", state)],
            "invalid-alias" => [new(Current, state, ["hx-snapshot-v2:party-state"])],
            _ => [new(Current, state), null],
        };

        Should.Throw<ArgumentException>(() => new SnapshotTypeRegistry(registrations!));
    }

    /// <summary>A protected snapshot that cannot be read returns no state and must be retained.</summary>
    [Theory]
    [InlineData("tampered-tag", UnreadableProtectedDataReason.BytesMetadataMismatch, 1)]
    [InlineData("missing-key", UnreadableProtectedDataReason.MissingKey, 1)]
    [InlineData("outage", UnreadableProtectedDataReason.ProviderUnavailable, 1)]
    [InlineData("other-sequence", UnreadableProtectedDataReason.BytesMetadataMismatch, 1)]
    [InlineData("truncated-envelope", UnreadableProtectedDataReason.BytesMetadataMismatch, 0)]
    public async Task V118_UnreadableProtectedSnapshot_ReturnsNoStateAndIsRetainedAsync(
        string failure,
        UnreadableProtectedDataReason expected,
        int expectedResolverCalls)
    {
        var resolver = new CountingKeyResolver(failure switch
        {
            "missing-key" => static _ => null,
            "outage" => static _ => throw new HttpRequestException(CompatibilityTestData.Sentinel),
            _ => null,
        });
        var router = new PayloadCompatibilityRouter(resolver.ResolveAsync, snapshotTypes: CompatibilityTestData.Registry());
        ProtectedSnapshotPayloadV2 carrier = CompatibilityTestData.ProtectSnapshot(sequence: 4);
        ProtectedSnapshotPayloadV2 stored = failure switch
        {
            "tampered-tag" => carrier with { Envelope = FlipLastCharacter(carrier.Envelope) },
            "truncated-envelope" => carrier with { Envelope = carrier.Envelope[..20] },
            _ => carrier,
        };
        JsonElement element = CompatibilityTestData.ToElement(stored);
        string before = element.GetRawText();
        ulong sequence = failure == "other-sequence" ? 5UL : 4UL;

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(
            CompatibilityTestData.Snapshot(element, CompatibilityTestData.V2Metadata(), sequence));

        result.IsReadable.ShouldBeFalse();
        result.UnreadableReason.ShouldBe(expected);
        result.State.ShouldBeNull();
        result.Metadata.ShouldBeNull();
        result.AllowsCorruptLegacyDeletion.ShouldBeFalse();
        resolver.Calls.ShouldBe(expectedResolverCalls);
        element.GetRawText().ShouldBe(before);
        CompatibilityTestData.ShouldNotLeak(result);
    }

    /// <summary>Invalid v2 type IDs are malformed stored bytes, not missing registry entries.</summary>
    [Theory]
    [InlineData("uppercase")]
    [InlineData("oversized")]
    [InlineData("wrong-prefix")]
    public async Task V118_InvalidV2SnapshotTypeId_IsBytesMetadataMismatchBeforeLookupAsync(string defect)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        ProtectedSnapshotPayloadV2 original = CompatibilityTestData.ProtectSnapshot();
        string invalidId = defect switch
        {
            "uppercase" => "hx-snapshot-v1:Party-state",
            "oversized" => "hx-snapshot-v1:" + new string('a', 128),
            _ => "other-snapshot-v1:party-state",
        };

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(CompatibilityTestData.Snapshot(
            original with { SnapshotTypeId = invalidId },
            CompatibilityTestData.V2Metadata()));

        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        result.State.ShouldBeNull();
        resolver.Calls.ShouldBe(0);
        reader.SnapshotCalls.ShouldBe(0);
    }

    /// <summary>Missing or unprotected metadata over any protected snapshot shape is a local mismatch.</summary>
    [Theory]
    [InlineData("v2-element", false)]
    [InlineData("v2-element", true)]
    [InlineData("v2-instance", false)]
    [InlineData("v2-instance", true)]
    [InlineData("v1-wrapper", false)]
    [InlineData("v1-wrapper", true)]
    [InlineData("camel-case-v2", false)]
    [InlineData("reserved-format-member", true)]
    [InlineData("nested-pdenc", false)]
    [InlineData("nested-enc", true)]
    [InlineData("v2-missing-format", false)]
    [InlineData("v2-only-type-id", true)]
    [InlineData("duplicate-members", false)]
    [InlineData("beyond-depth", false)]
    public async Task V118_ProtectedShapeWithLegacyMetadata_IsBytesMetadataMismatchAsync(string shape, bool explicitUnprotected)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        object state = ShapeFor(shape);

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(CompatibilityTestData.Snapshot(
            state,
            explicitUnprotected ? EventStorePayloadProtectionMetadata.Unprotected() : null));

        result.Route.ShouldBe(CompatibilityReadRoute.Rejected);
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        result.State.ShouldBeNull();
        result.AllowsCorruptLegacyDeletion.ShouldBeFalse();
        resolver.Calls.ShouldBe(0);
        reader.SnapshotCalls.ShouldBe(0);
    }

    /// <summary>Protected metadata that disagrees with the stored snapshot shape is a local decision with no calls.</summary>
    [Theory]
    [InlineData("v2", "plain", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v2", "v1-wrapper", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v2", "v2-extra-member", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v2", "v2-missing-format", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v2", "v2-only-type-id", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v2", "v2-v1-format", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v2", "v2-v3-format", UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation)]
    [InlineData("v1", "v2-element", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v1", "v2-instance", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v1", "plain", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v1", "v1-wrong-format", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v1", "v1-missing-marker", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v1", "v1-missing-format", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v1", "v1-missing-type", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v1", "v1-empty-type", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v1", "v1-missing-payload", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v1", "v1-null-payload", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("v1", "v1-wrapper-with-pdenc", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    public async Task V118_ProtectedMetadataShapeDisagreement_IsLocalDecisionAsync(
        string metadata,
        string shape,
        UnreadableProtectedDataReason expected)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(CompatibilityTestData.Snapshot(
            ShapeFor(shape),
            metadata == "v2" ? CompatibilityTestData.V2Metadata() : CompatibilityTestData.PartiesV1Metadata()));

        result.Route.ShouldBe(CompatibilityReadRoute.Rejected);
        result.UnreadableReason.ShouldBe(expected);
        result.AllowsCorruptLegacyDeletion.ShouldBeFalse();
        resolver.Calls.ShouldBe(0);
        reader.SnapshotCalls.ShouldBe(0);
    }

    /// <summary>Malformed, over-version, opaque, or non-allowlisted snapshot metadata is rejected before shape checks.</summary>
    [Theory]
    [InlineData("undefined-state", UnreadableProtectedDataReason.MalformedMetadata)]
    [InlineData("undefined-state-over-version", UnreadableProtectedDataReason.MalformedMetadata)]
    [InlineData("version-zero", UnreadableProtectedDataReason.MalformedMetadata)]
    [InlineData("over-version", UnreadableProtectedDataReason.UnknownMetadataVersion)]
    [InlineData("opaque", UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation)]
    [InlineData("opaque-forbidden", UnreadableProtectedDataReason.MalformedMetadata)]
    [InlineData("v2-extra-flag", UnreadableProtectedDataReason.MalformedMetadata)]
    [InlineData("unknown-scheme", UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation)]
    public async Task V118_SnapshotMetadataFailure_IsRejectedWithoutCallsAsync(
        string defect,
        UnreadableProtectedDataReason expected)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        EventStorePayloadProtectionMetadata v2 = CompatibilityTestData.V2Metadata();
        EventStorePayloadProtectionMetadata metadata = defect switch
        {
            "undefined-state" => v2 with { State = (PayloadProtectionState)7 },
            "undefined-state-over-version" => v2 with { State = (PayloadProtectionState)7, MetadataVersion = 2 },
            "version-zero" => v2 with { MetadataVersion = 0 },
            "over-version" => v2 with { MetadataVersion = 2 },
            "opaque" => EventStorePayloadProtectionMetadata.ProviderOpaque(),
            "opaque-forbidden" => EventStorePayloadProtectionMetadata.ProviderOpaque("forbidden"),
            "v2-extra-flag" => v2 with
            {
                CompatibilityFlags = CompatibilityTestData.Flags(("format", "json+pdenc-v2"), ("envelope", "pdenc-v2"), ("id", "1")),
            },
            _ => v2 with { Scheme = "acme-envelope-v9" },
        };

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(CompatibilityTestData.Snapshot(
            CompatibilityTestData.ToElement(CompatibilityTestData.ProtectSnapshot()),
            metadata));

        result.Route.ShouldBe(CompatibilityReadRoute.Rejected);
        result.UnreadableReason.ShouldBe(expected);
        resolver.Calls.ShouldBe(0);
        reader.SnapshotCalls.ShouldBe(0);
    }

    /// <summary>Snapshot metadata is classified before the state shape.</summary>
    [Fact]
    public async Task V118_OverVersionMetadataOverUnverifiableState_IsUnknownMetadataVersionAsync()
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, _) = CreateRouter();

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(CompatibilityTestData.Snapshot(
            ShapeFor("duplicate-members"),
            CompatibilityTestData.V2Metadata() with { MetadataVersion = 2 }));

        result.Route.ShouldBe(CompatibilityReadRoute.Rejected);
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.UnknownMetadataVersion);
        resolver.Calls.ShouldBe(0);
    }

    /// <summary>Exact Parties v1 metadata over its snapshot wrapper is decided by the registered reader.</summary>
    [Theory]
    [InlineData("v1-wrapper")]
    [InlineData("v1-object-payload")]
    public async Task V117_V1Snapshot_RoutesToRegisteredReaderAsync(string shape)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        byte[] plaintext = CompatibilityTestData.SnapshotPlaintext();
        reader.OnSnapshot = (_, _) => CoreUnprotectionResult.Readable(plaintext);

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(
            CompatibilityTestData.Snapshot(ShapeFor(shape), CompatibilityTestData.PartiesV1Metadata()));

        result.IsReadable.ShouldBeTrue();
        result.Route.ShouldBe(CompatibilityReadRoute.RegisteredV1);
        ((JsonElement)result.State!).GetRawText().ShouldBe("{\"Name\":\"Alice\",\"Version\":3}");
        result.Metadata.ShouldBe(EventStorePayloadProtectionMetadata.Unprotected());
        result.AllowsCorruptLegacyDeletion.ShouldBeFalse();
        reader.SnapshotCalls.ShouldBe(1);
        reader.LastMetadata.ShouldBe(CompatibilityTestData.PartiesV1Metadata());
        resolver.Calls.ShouldBe(0);
        plaintext.ShouldAllBe(static value => value == 0);
    }

    /// <summary>A v1 snapshot is opaque without a reader and keeps the reader's typed reason or fault mapping.</summary>
    [Theory]
    [InlineData("no-reader", UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation)]
    [InlineData("missing-key", UnreadableProtectedDataReason.MissingKey)]
    [InlineData("fault", UnreadableProtectedDataReason.ProviderUnavailable)]
    [InlineData("partial", UnreadableProtectedDataReason.ConsistencyMismatch)]
    [InlineData("wrapper-no-enc", UnreadableProtectedDataReason.ConsistencyMismatch)]
    [InlineData("json-null", UnreadableProtectedDataReason.ConsistencyMismatch)]
    public async Task V118_UnreadableV1Snapshot_ReturnsNoStateAsync(string failure, UnreadableProtectedDataReason expected)
    {
        var resolver = new CountingKeyResolver();
        byte[]? readerPayload = failure switch
        {
            "wrapper-no-enc" => "{\"marker\":\"$protectedSnapshot\",\"typeName\":\"PartyState\",\"payload\":\"plain\",\"serializationFormat\":\"json+pdenc-v1\"}"u8.ToArray(),
            "json-null" => "null"u8.ToArray(),
            _ => null,
        };
        var reader = new FakeLegacyPayloadReader
        {
            OnSnapshot = failure switch
            {
                "missing-key" => static (_, _) => CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.MissingKey),
                "fault" => static (_, _) => throw new InvalidOperationException(CompatibilityTestData.Sentinel),
                "wrapper-no-enc" or "json-null" => (_, _) => CoreUnprotectionResult.Readable(readerPayload!),
                _ => static (_, _) => CoreUnprotectionResult.Readable(
                    Encoding.UTF8.GetBytes("{\"marker\":\"$protectedSnapshot\",\"payload\":{\"$enc\":1}}")),
            },
        };
        var router = new PayloadCompatibilityRouter(
            resolver.ResolveAsync,
            failure == "no-reader" ? null : [reader],
            CompatibilityTestData.Registry());

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(
            CompatibilityTestData.Snapshot(ShapeFor("v1-wrapper"), CompatibilityTestData.PartiesV1Metadata()));

        result.IsReadable.ShouldBeFalse();
        result.UnreadableReason.ShouldBe(expected);
        result.State.ShouldBeNull();
        result.AllowsCorruptLegacyDeletion.ShouldBeFalse();
        CompatibilityTestData.ShouldNotLeak(result);
        readerPayload?.ShouldAllBe(static value => value == 0);
    }

    /// <summary>A reader cannot escape the bounded reason taxonomy, even when its result is unreadable.</summary>
    [Fact]
    public async Task V118_UndefinedLegacyReaderReason_IsConsistencyMismatchAsync()
    {
        var resolver = new CountingKeyResolver();
        var reader = new FakeLegacyPayloadReader
        {
            OnSnapshot = static (_, _) => CoreUnprotectionResult.Unreadable((UnreadableProtectedDataReason)999),
        };
        var router = new PayloadCompatibilityRouter(resolver.ResolveAsync, [reader]);

        CompatibilitySnapshotReadResult result = await router.ReadSnapshotAsync(
            CompatibilityTestData.Snapshot(ShapeFor("v1-wrapper"), CompatibilityTestData.PartiesV1Metadata()));

        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        result.State.ShouldBeNull();
    }

    /// <summary>Cancellation wins when a legacy reader returns an unreadable result after cancelling the caller.</summary>
    [Fact]
    public async Task V118_LegacyReaderUnreadableAfterCancellation_PropagatesAsync()
    {
        using var source = new CancellationTokenSource();
        var reader = new FakeLegacyPayloadReader
        {
            OnSnapshot = (_, _) =>
            {
                source.Cancel();
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.MissingKey);
            },
        };
        var router = new PayloadCompatibilityRouter(new CountingKeyResolver().ResolveAsync, [reader]);

        await Should.ThrowAsync<OperationCanceledException>(async () => await router.ReadSnapshotAsync(
            CompatibilityTestData.Snapshot(ShapeFor("v1-wrapper"), CompatibilityTestData.PartiesV1Metadata()),
            source.Token));
    }

    /// <summary>Caller cancellation during registered v2 deserialization propagates after materialization.</summary>
    [Fact]
    public async Task V118_V2SnapshotCancellationDuringMaterialization_PropagatesAsync()
    {
        using var source = new CancellationTokenSource();
        var options = new JsonSerializerOptions { TypeInfoResolver = CompatibilityTestJsonContext.Default };
        options.Converters.Add(new CancellingSnapshotConverter(source));
        var registry = new SnapshotTypeRegistry(
            [new SnapshotTypeRegistration(CompatibilityTestData.SnapshotTypeId, options.GetTypeInfo(typeof(PartySnapshotState)))]);
        var resolver = new CountingKeyResolver();
        var router = new PayloadCompatibilityRouter(resolver.ResolveAsync, snapshotTypes: registry);

        await Should.ThrowAsync<OperationCanceledException>(async () => await router.ReadSnapshotAsync(
            CompatibilityTestData.Snapshot(CompatibilityTestData.ProtectSnapshot(), CompatibilityTestData.V2Metadata()),
            source.Token));

        resolver.Calls.ShouldBe(1);
    }

    /// <summary>A snapshot state that is neither a stored JSON element nor a v2 carrier is an invalid argument.</summary>
    [Fact]
    public async Task V117_UnsupportedStateType_IsRejectedAsArgumentAsync()
    {
        (PayloadCompatibilityRouter router, _, _) = CreateRouter();

        await Should.ThrowAsync<ArgumentException>(async () => await router.ReadSnapshotAsync(
            CompatibilityTestData.Snapshot(new PartySnapshotState("Alice", 3), null)));
    }

    /// <summary>Caller cancellation propagates from every snapshot route.</summary>
    [Theory]
    [InlineData("legacy", false)]
    [InlineData("v1", false)]
    [InlineData("v2", false)]
    [InlineData("v1", true)]
    [InlineData("v2", true)]
    public async Task V118_Cancellation_PropagatesFromEverySnapshotRouteAsync(string route, bool duringCall)
    {
        using var source = new CancellationTokenSource();
        var resolver = new CountingKeyResolver(_ =>
        {
            source.Cancel();
            throw new OperationCanceledException(source.Token);
        });
        var reader = new FakeLegacyPayloadReader
        {
            OnSnapshot = (_, _) =>
            {
                source.Cancel();
                throw new OperationCanceledException(source.Token);
            },
        };
        var router = new PayloadCompatibilityRouter(resolver.ResolveAsync, [reader], CompatibilityTestData.Registry());
        CompatibilitySnapshotRecord record = route switch
        {
            "legacy" => CompatibilityTestData.Snapshot(CompatibilityTestData.Json("{\"Name\":\"Alice\"}"), null),
            "v1" => CompatibilityTestData.Snapshot(ShapeFor("v1-wrapper"), CompatibilityTestData.PartiesV1Metadata()),
            _ => CompatibilityTestData.Snapshot(CompatibilityTestData.ProtectSnapshot(), CompatibilityTestData.V2Metadata()),
        };
        if (!duringCall)
        {
            await source.CancelAsync();
        }

        await Should.ThrowAsync<OperationCanceledException>(async () => await router.ReadSnapshotAsync(record, source.Token));
    }

    private static object ShapeFor(string shape)
    {
        ProtectedSnapshotPayloadV2 carrier = CompatibilityTestData.ProtectSnapshot();
        return shape switch
        {
            "plain" => CompatibilityTestData.Json("{\"Name\":\"Alice\",\"Version\":3}"),
            "v2-element" => CompatibilityTestData.ToElement(carrier),
            "v2-instance" => carrier,
            "v1-wrapper" => CompatibilityTestData.Json(
                "{\"marker\":\"$protectedSnapshot\",\"typeName\":\"PartyState\",\"payload\":\"bm9wZQ\",\"serializationFormat\":\"json+pdenc-v1\"}"),
            "v1-object-payload" => CompatibilityTestData.Json(
                "{\"marker\":\"$protectedSnapshot\",\"typeName\":\"PartyState\",\"payload\":{\"email\":{\"$enc\":{}}},\"serializationFormat\":\"json+pdenc-v1\"}"),
            "v1-wrapper-with-pdenc" => CompatibilityTestData.Json(
                "{\"marker\":\"$protectedSnapshot\",\"typeName\":\"PartyState\",\"payload\":\"bm9wZQ\",\"serializationFormat\":\"json+pdenc-v1\",\"$pdenc\":\"AAAA\"}"),
            "v1-wrong-format" => CompatibilityTestData.Json(
                "{\"marker\":\"$protectedSnapshot\",\"typeName\":\"PartyState\",\"payload\":\"bm9wZQ\",\"serializationFormat\":\"json+pdenc-v2\"}"),
            "v1-missing-marker" => CompatibilityTestData.Json(
                "{\"typeName\":\"PartyState\",\"payload\":\"bm9wZQ\",\"serializationFormat\":\"json+pdenc-v1\"}"),
            "v1-missing-format" => CompatibilityTestData.Json(
                "{\"marker\":\"$protectedSnapshot\",\"typeName\":\"PartyState\",\"payload\":\"bm9wZQ\"}"),
            "v1-missing-type" => CompatibilityTestData.Json(
                "{\"marker\":\"$protectedSnapshot\",\"payload\":\"bm9wZQ\",\"serializationFormat\":\"json+pdenc-v1\"}"),
            "v1-empty-type" => CompatibilityTestData.Json(
                "{\"marker\":\"$protectedSnapshot\",\"typeName\":\"  \",\"payload\":\"bm9wZQ\",\"serializationFormat\":\"json+pdenc-v1\"}"),
            "v1-missing-payload" => CompatibilityTestData.Json(
                "{\"marker\":\"$protectedSnapshot\",\"typeName\":\"PartyState\",\"serializationFormat\":\"json+pdenc-v1\"}"),
            "v1-null-payload" => CompatibilityTestData.Json(
                "{\"marker\":\"$protectedSnapshot\",\"typeName\":\"PartyState\",\"payload\":null,\"serializationFormat\":\"json+pdenc-v1\"}"),
            "camel-case-v2" => CompatibilityTestData.Json(
                "{\"format\":\"json+pdenc-v2\",\"snapshotTypeId\":\"" + carrier.SnapshotTypeId + "\",\"envelope\":\"" + carrier.Envelope + "\"}"),
            "v2-extra-member" => CompatibilityTestData.Json(
                "{\"Format\":\"json+pdenc-v2\",\"SnapshotTypeId\":\"" + carrier.SnapshotTypeId + "\",\"Envelope\":\"" + carrier.Envelope + "\",\"Note\":\"x\"}"),
            "v2-v1-format" => CompatibilityTestData.ToElement(carrier with { Format = "json+pdenc-v1" }),
            "v2-v3-format" => CompatibilityTestData.ToElement(carrier with { Format = "json+pdenc-v3" }),
            "v2-missing-format" => CompatibilityTestData.Json(
                "{\"SnapshotTypeId\":\"" + carrier.SnapshotTypeId + "\",\"Envelope\":\"" + carrier.Envelope + "\"}"),
            "v2-only-type-id" => CompatibilityTestData.Json("{\"snapshotTypeId\":\"" + carrier.SnapshotTypeId + "\"}"),
            "reserved-format-member" => CompatibilityTestData.Json("{\"Name\":\"Alice\",\"serializationFormat\":\"JSON+PDENC-V9\"}"),
            "nested-pdenc" => CompatibilityTestData.Json("{\"Name\":{\"$pdenc\":\"AAAA\"}}"),
            "nested-enc" => CompatibilityTestData.Json("{\"Items\":[{\"Name\":{\"$enc\":{}}}]}"),
            "duplicate-members" => CompatibilityTestData.Json("{\"Name\":\"Alice\",\"Name\":\"Bob\"}"),
            _ => CompatibilityTestData.Json(new string('[', 64) + "{\"Name\":1}" + new string(']', 64)),
        };
    }

    private static string FlipLastCharacter(string envelope)
        => envelope[..^1] + (envelope[^1] == 'A' ? 'Q' : 'A');

    private static (PayloadCompatibilityRouter Router, CountingKeyResolver Resolver, FakeLegacyPayloadReader Reader) CreateRouter()
    {
        var resolver = new CountingKeyResolver();
        var reader = new FakeLegacyPayloadReader();
        return (
            new PayloadCompatibilityRouter(resolver.ResolveAsync, [reader], CompatibilityTestData.Registry()),
            resolver,
            reader);
    }

}
