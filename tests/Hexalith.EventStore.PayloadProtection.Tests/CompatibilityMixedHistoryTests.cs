using System.Text;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>
/// Exercises Story 8.4 mixed-history stream routing (normative section 12.1 step 4, vector V119).
/// Vector numbers appear in method names only; the Story 8.3 <c>Vector</c> trait set stays unchanged.
/// </summary>
public sealed class CompatibilityMixedHistoryTests
{
    /// <summary>Every record of a readable mixed stream follows its own route, in order.</summary>
    [Fact]
    public async Task V119_ReadableMixedStream_RoutesEveryRecordIndependentlyAsync()
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        CompatibilityEventRecord[] records =
        [
            CompatibilityTestData.V2Event(1),
            CompatibilityTestData.LegacyEvent(2),
            CompatibilityTestData.RedactedEvent(3),
            CompatibilityTestData.V1Event(4),
            CompatibilityTestData.UnprotectedEvent(5),
            CompatibilityTestData.CustomEvent(6),
            CompatibilityTestData.V1Event(7, withMetadata: false),
            CompatibilityTestData.V2Event(8),
        ];

        CompatibilityStreamReadResult result = await router.ReadStreamAsync(records);

        result.IsReadable.ShouldBeTrue();
        result.FirstUnreadable.ShouldBeNull();
        result.Events!.Select(static item => item.SequenceNumber).ShouldBe([1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL, 8UL]);
        result.Events!.Select(static item => item.Route).ShouldBe(
        [
            CompatibilityReadRoute.SharedV2,
            CompatibilityReadRoute.LegacyUnprotected,
            CompatibilityReadRoute.Redacted,
            CompatibilityReadRoute.RegisteredV1,
            CompatibilityReadRoute.Unprotected,
            CompatibilityReadRoute.LegacyUnprotected,
            CompatibilityReadRoute.RegisteredV1,
            CompatibilityReadRoute.SharedV2,
        ]);
        result.Events!.Select(static item => item.SerializationFormat).ShouldBe(
            ["json", "json", "json-redacted", "json", "json", "application/x-protobuf", "json", "json"]);
        result.Events![0].PayloadBytes.ShouldBe(CompatibilityTestData.PlainJson());
        result.Events![7].PayloadBytes.ShouldBe(CompatibilityTestData.PlainJson());
        resolver.Calls.ShouldBe(2);
        reader.EventCalls.ShouldBe(2);
    }

    /// <summary>An empty stream is readable and calls nothing.</summary>
    [Fact]
    public async Task V119_EmptyStream_IsReadableAsync()
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();

        CompatibilityStreamReadResult result = await router.ReadStreamAsync([]);

        result.IsReadable.ShouldBeTrue();
        result.Events.ShouldBeEmpty();
        resolver.Calls.ShouldBe(0);
        reader.EventCalls.ShouldBe(0);
    }

    /// <summary>The stream stops at the first unreadable sequence and no later record is examined.</summary>
    [Theory]
    [InlineData("missing-key", UnreadableProtectedDataReason.MissingKey)]
    [InlineData("misplaced-v2", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("stripped-v2", UnreadableProtectedDataReason.BytesMetadataMismatch)]
    [InlineData("reserved-v3", UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation)]
    [InlineData("malformed-carrier", UnreadableProtectedDataReason.MalformedMetadata)]
    [InlineData("v1-unreadable", UnreadableProtectedDataReason.KeyInvalidatedOrDeleted)]
    public async Task V119_StopsAtFirstUnreadableSequenceAsync(string failure, UnreadableProtectedDataReason expected)
    {
        int resolverCalls = 0;
        var resolver = new CountingKeyResolver(_ =>
        {
            resolverCalls++;
            return failure == "missing-key" && resolverCalls == 2 ? null : TestFixture.Dek();
        });
        var reader = new FakeLegacyPayloadReader();
        reader.OnEvent = (record, _) => failure == "v1-unreadable" && record.SequenceNumber == 4
            ? CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted)
            : CoreUnprotectionResult.Readable(CompatibilityTestData.PlainJson());
        var router = new PayloadCompatibilityRouter(resolver.ResolveAsync, [reader]);
        CompatibilityEventRecord failing = failure switch
        {
            "missing-key" => CompatibilityTestData.V2Event(4),
            "misplaced-v2" => CompatibilityTestData.MisplacedV2Event(4),
            "stripped-v2" => CompatibilityTestData.Event(4, CompatibilityTestData.PlainJson(), "json+pdenc-v2", CompatibilityTestData.V2Carrier()),
            "reserved-v3" => CompatibilityTestData.Event(4, CompatibilityTestData.V2Json(4), "json+pdenc-v3", CompatibilityTestData.V2Carrier()),
            "malformed-carrier" => CompatibilityTestData.Event(4, CompatibilityTestData.V2Json(4), "json+pdenc-v2", "{\"state\":\"9\",\"metadataVersion\":1}"),
            _ => CompatibilityTestData.V1Event(4),
        };
        CompatibilityEventRecord[] records =
        [
            CompatibilityTestData.LegacyEvent(1),
            CompatibilityTestData.V1Event(2),
            CompatibilityTestData.V2Event(3),
            failing,
            CompatibilityTestData.V2Event(5),
            CompatibilityTestData.V1Event(6),
            CompatibilityTestData.RedactedEvent(7),
        ];
        int callsThroughFailure = failure is "missing-key" or "misplaced-v2" ? 2 : 1;
        int readsThroughFailure = failure == "v1-unreadable" ? 2 : 1;

        CompatibilityStreamReadResult result = await router.ReadStreamAsync(records);

        result.IsReadable.ShouldBeFalse();
        result.Events.ShouldBeNull();
        result.FirstUnreadable.ShouldNotBeNull();
        result.FirstUnreadable.SequenceNumber.ShouldBe(4UL);
        result.FirstUnreadable.UnreadableReason.ShouldBe(expected);
        result.FirstUnreadable.PayloadBytes.ShouldBeNull();
        resolver.Calls.ShouldBe(callsThroughFailure);
        reader.EventCalls.ShouldBe(readsThroughFailure);
    }

    /// <summary>
    /// Every order of the four readable route kinds stops exactly at an inserted unreadable record, examining nothing
    /// after it.
    /// </summary>
    [Fact]
    public async Task V119_EveryPermutationStopsOnlyAtTheUnreadableRecordAsync()
    {
        string[] kinds = ["legacy", "redacted", "v1", "v2"];
        int cases = 0;
        foreach (string[] order in Permutations(kinds))
        {
            for (int position = 0; position <= order.Length; position++)
            {
                (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
                var records = new List<CompatibilityEventRecord>();
                ulong sequence = 1;
                for (int index = 0; index <= order.Length; index++)
                {
                    if (index == position)
                    {
                        records.Add(CompatibilityTestData.MisplacedV2Event(sequence++));
                    }

                    if (index < order.Length)
                    {
                        records.Add(Create(order[index], sequence++));
                    }
                }

                CompatibilityStreamReadResult result = await router.ReadStreamAsync(records);

                string[] before = order[..position];
                result.Events.ShouldBeNull();
                result.FirstUnreadable!.SequenceNumber.ShouldBe((ulong)(position + 1));
                result.FirstUnreadable.Route.ShouldBe(CompatibilityReadRoute.SharedV2);
                result.FirstUnreadable.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
                resolver.Calls.ShouldBe(before.Count(static kind => kind == "v2") + 1);
                reader.EventCalls.ShouldBe(before.Count(static kind => kind == "v1"));
                cases++;
            }
        }

        cases.ShouldBe(24 * 5);
    }

    /// <summary>The same stored stream always produces the same decision.</summary>
    [Fact]
    public async Task V119_SameInputAlwaysGivesTheSameDecisionAsync()
    {
        CompatibilityEventRecord[] readable =
        [
            CompatibilityTestData.V1Event(1),
            CompatibilityTestData.V2Event(2),
            CompatibilityTestData.RedactedEvent(3),
        ];
        CompatibilityEventRecord[] unreadable =
        [
            CompatibilityTestData.V1Event(1),
            CompatibilityTestData.LegacyEvent(2),
            CompatibilityTestData.Event(3, CompatibilityTestData.V1Json(), "json", null),
            CompatibilityTestData.V2Event(4),
        ];
        (PayloadCompatibilityRouter router, _, _) = CreateRouter();

        for (int attempt = 0; attempt < 3; attempt++)
        {
            CompatibilityStreamReadResult first = await router.ReadStreamAsync(readable);
            first.Events!.Select(static item => (item.SequenceNumber, item.Route, Encoding.UTF8.GetString(item.PayloadBytes!)))
                .ShouldBe(
                [
                    (1UL, CompatibilityReadRoute.RegisteredV1, Encoding.UTF8.GetString(CompatibilityTestData.PlainJson())),
                    (2UL, CompatibilityReadRoute.SharedV2, Encoding.UTF8.GetString(CompatibilityTestData.PlainJson())),
                    (3UL, CompatibilityReadRoute.Redacted, Encoding.UTF8.GetString(CompatibilityTestData.RedactedJson())),
                ]);

            CompatibilityStreamReadResult second = await router.ReadStreamAsync(unreadable);
            second.FirstUnreadable!.SequenceNumber.ShouldBe(3UL);
            second.FirstUnreadable.Route.ShouldBe(CompatibilityReadRoute.Rejected);
            second.FirstUnreadable.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }
    }

    /// <summary>
    /// A stopped stream clears router-owned plaintext from earlier records but never the caller's stored bytes.
    /// </summary>
    [Fact]
    public async Task V119_StoppedStream_ClearsOnlyRouterOwnedPlaintextAsync()
    {
        (PayloadCompatibilityRouter router, _, FakeLegacyPayloadReader reader) = CreateRouter();
        byte[] decrypted = CompatibilityTestData.PlainJson();
        reader.OnEvent = (_, _) => CoreUnprotectionResult.Readable(decrypted);
        CompatibilityEventRecord legacy = CompatibilityTestData.LegacyEvent(2);

        CompatibilityStreamReadResult result = await router.ReadStreamAsync(
        [
            CompatibilityTestData.V1Event(1),
            legacy,
            CompatibilityTestData.Event(3, CompatibilityTestData.PlainJson(), "json+pdenc-v2", CompatibilityTestData.V2Carrier()),
        ]);

        result.FirstUnreadable!.SequenceNumber.ShouldBe(3UL);
        decrypted.ShouldAllBe(static value => value == 0);
        legacy.PayloadBytes.ShouldBe(CompatibilityTestData.PlainJson());
    }

    /// <summary>An aborted stream clears the earlier v2 output buffer, as observed only after zeroing.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V119_AbortedStream_ClearsEarlierV2PlaintextAsync(bool cancel)
    {
        var observer = new RecordingBufferObserver();
        using var source = new CancellationTokenSource();
        var reader = new FakeLegacyPayloadReader
        {
            OnEvent = (_, _) =>
            {
                source.Cancel();
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.MissingKey);
            },
        };
        var router = new PayloadCompatibilityRouter(
            new CountingKeyResolver().ResolveAsync,
            [reader],
            bufferObserver: observer);
        CompatibilityEventRecord storedV2 = CompatibilityTestData.V2Event(1);

        if (cancel)
        {
            await Should.ThrowAsync<OperationCanceledException>(async () => await router.ReadStreamAsync(
                [storedV2, CompatibilityTestData.V1Event(2)], source.Token));
        }
        else
        {
            CompatibilityStreamReadResult result = await router.ReadStreamAsync(
                [storedV2, CompatibilityTestData.Event(2, CompatibilityTestData.PlainJson(), "json+pdenc-v2", CompatibilityTestData.V2Carrier())]);
            result.FirstUnreadable!.SequenceNumber.ShouldBe(2UL);
        }

        observer.Observed.ShouldBe([SensitiveBufferKind.DecryptedPlaintext]);
        storedV2.PayloadBytes.ShouldBe(CompatibilityTestData.V2Json(1));
    }

    /// <summary>
    /// A stream cancelled after an earlier protected record was read clears that record's router-owned plaintext.
    /// </summary>
    [Fact]
    public async Task V119_CancelledStream_ClearsEarlierRouterOwnedPlaintextAsync()
    {
        using var source = new CancellationTokenSource();
        byte[] decrypted = CompatibilityTestData.PlainJson();
        var reader = new FakeLegacyPayloadReader
        {
            OnEvent = (record, _) =>
            {
                if (record.SequenceNumber == 1)
                {
                    return CoreUnprotectionResult.Readable(decrypted);
                }

                source.Cancel();
                throw new OperationCanceledException(source.Token);
            },
        };
        var router = new PayloadCompatibilityRouter(new CountingKeyResolver().ResolveAsync, [reader]);

        await Should.ThrowAsync<OperationCanceledException>(async () => await router.ReadStreamAsync(
            [CompatibilityTestData.V1Event(1), CompatibilityTestData.V1Event(2)],
            source.Token));

        reader.EventCalls.ShouldBe(2);
        decrypted.ShouldAllBe(static value => value == 0);
    }

    /// <summary>Only the protected routes own their readable payload buffer.</summary>
    [Theory]
    [InlineData(nameof(CompatibilityReadRoute.RegisteredV1), true)]
    [InlineData(nameof(CompatibilityReadRoute.SharedV2), true)]
    [InlineData(nameof(CompatibilityReadRoute.LegacyUnprotected), false)]
    [InlineData(nameof(CompatibilityReadRoute.Unprotected), false)]
    [InlineData(nameof(CompatibilityReadRoute.Redacted), false)]
    [InlineData(nameof(CompatibilityReadRoute.Rejected), false)]
    public void V119_OwnsPayload_IsTrueOnlyForProtectedRoutes(string route, bool expected)
        => CompatibilityEventReadResult
            .Readable(1, Enum.Parse<CompatibilityReadRoute>(route), CompatibilityTestData.PlainJson(), "json", EventStorePayloadProtectionMetadata.Unprotected())
            .OwnsPayload
            .ShouldBe(expected);

    /// <summary>Records out of order, duplicated, gapped, foreign, or null are rejected before any record is examined.</summary>
    [Theory]
    [InlineData("descending")]
    [InlineData("duplicate")]
    [InlineData("gap")]
    [InlineData("foreign")]
    [InlineData("null")]
    public async Task V119_InvalidStreamShape_IsRejectedBeforeRoutingAsync(string shape)
    {
        (PayloadCompatibilityRouter router, CountingKeyResolver resolver, FakeLegacyPayloadReader reader) = CreateRouter();
        CompatibilityEventRecord?[] records = shape switch
        {
            "descending" => [CompatibilityTestData.V2Event(2), CompatibilityTestData.V1Event(1)],
            "duplicate" => [CompatibilityTestData.V2Event(1), CompatibilityTestData.V1Event(1)],
            "gap" => [CompatibilityTestData.V2Event(1), CompatibilityTestData.V1Event(2), CompatibilityTestData.LegacyEvent(4)],
            "foreign" =>
            [
                CompatibilityTestData.V2Event(1),
                CompatibilityTestData.V1Event(2) with { Identity = new AggregateIdentity("tenant-b", "parties", "party-01") },
            ],
            _ => [CompatibilityTestData.V2Event(1), null],
        };

        await Should.ThrowAsync<ArgumentException>(async () => await router.ReadStreamAsync(records!));

        resolver.Calls.ShouldBe(0);
        reader.EventCalls.ShouldBe(0);
    }

    /// <summary>Cancellation mid-stream propagates and later records are not examined.</summary>
    [Fact]
    public async Task V119_CancellationMidStream_PropagatesAsync()
    {
        using var source = new CancellationTokenSource();
        var resolver = new CountingKeyResolver();
        var reader = new FakeLegacyPayloadReader
        {
            OnEvent = (_, _) =>
            {
                source.Cancel();
                return CoreUnprotectionResult.Readable(CompatibilityTestData.PlainJson());
            },
        };
        var router = new PayloadCompatibilityRouter(resolver.ResolveAsync, [reader]);

        await Should.ThrowAsync<OperationCanceledException>(async () => await router.ReadStreamAsync(
            [CompatibilityTestData.LegacyEvent(1), CompatibilityTestData.V1Event(2), CompatibilityTestData.V2Event(3)],
            source.Token));

        reader.EventCalls.ShouldBe(1);
        resolver.Calls.ShouldBe(0);
    }

    /// <summary>A stream decision never renders a sentinel planted in a later or failing record.</summary>
    [Fact]
    public async Task V119_StreamDecision_CarriesNoSentinelAsync()
    {
        (PayloadCompatibilityRouter router, _, _) = CreateRouter();
        string sentinel = CompatibilityTestData.Sentinel;

        CompatibilityStreamReadResult result = await router.ReadStreamAsync(
        [
            CompatibilityTestData.LegacyEvent(1),
            CompatibilityTestData.Event(2, CompatibilityTestData.V2Json(2, sentinel), "json+pdenc-v2", CompatibilityTestData.V2Carrier()) with
            {
                EventTypeName = "Hexalith.Parties.Contracts.Events.PartyDeleted",
            },
            CompatibilityTestData.Event(3, Encoding.UTF8.GetBytes("{\"name\":\"" + sentinel + "\"}"), "json", null),
        ]);

        result.FirstUnreadable!.SequenceNumber.ShouldBe(2UL);
        CompatibilityTestData.ShouldNotLeak(result);
    }

    private static CompatibilityEventRecord Create(string kind, ulong sequence) => kind switch
    {
        "legacy" => CompatibilityTestData.LegacyEvent(sequence),
        "redacted" => CompatibilityTestData.RedactedEvent(sequence),
        "v1" => CompatibilityTestData.V1Event(sequence),
        _ => CompatibilityTestData.V2Event(sequence),
    };

    private static IEnumerable<string[]> Permutations(string[] items)
    {
        if (items.Length <= 1)
        {
            yield return items;
            yield break;
        }

        for (int index = 0; index < items.Length; index++)
        {
            string[] rest = [.. items[..index], .. items[(index + 1)..]];
            foreach (string[] tail in Permutations(rest))
            {
                yield return [items[index], .. tail];
            }
        }
    }

    private static (PayloadCompatibilityRouter Router, CountingKeyResolver Resolver, FakeLegacyPayloadReader Reader) CreateRouter()
    {
        var resolver = new CountingKeyResolver();
        var reader = new FakeLegacyPayloadReader();
        return (new PayloadCompatibilityRouter(resolver.ResolveAsync, [reader]), resolver, reader);
    }
}
