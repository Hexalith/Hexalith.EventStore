using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Testing.Builders;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Actors;

/// <summary>
/// Story 4.5 live evidence for an actor append racing an adversarial actor-state transaction.
/// </summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class AppendDurabilityRaceLiveSidecarTests
{
    private const string EvidenceDirectoryEnvironmentVariable = "HEXALITH_STORY_4_5_EVIDENCE_DIR";
    private const string MutationEnvironmentVariable = "HEXALITH_STORY_4_5_MUTATION";
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions ReadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private static readonly JsonSerializerOptions EvidenceJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly DaprTestContainerFixture _fixture;
    private readonly ITestOutputHelper _output;

    /// <summary>Initializes the live append-race evidence test.</summary>
    /// <param name="fixture">The shared isolated Dapr fixture.</param>
    /// <param name="output">The xUnit evidence output sink.</param>
    public AppendDurabilityRaceLiveSidecarTests(
        DaprTestContainerFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _fixture.ResetTestState();
        _fixture.SetupCounterDomain();
    }

    /// <summary>
    /// Holds the actor after its metadata read, proves the raw contender durable, then records the
    /// final Redis state without prescribing which supported race outcome must occur.
    /// </summary>
    [Fact]
    public async Task SameStreamActorAndRawTransaction_RecordsDurableRaceOutcome()
    {
        string aggregateId = $"append-race-{UniqueIdHelper.GenerateSortableUniqueStringId()}";
        var identity = new AggregateIdentity("tenant-a", "counter", aggregateId);
        string actorMessageId = UniqueIdHelper.GenerateSortableUniqueStringId();
        string rawMessageId = UniqueIdHelper.GenerateSortableUniqueStringId();
        string rawCorrelationId = UniqueIdHelper.GenerateSortableUniqueStringId();
        var command = new CommandEnvelopeBuilder()
            .WithMessageId(actorMessageId)
            .WithCorrelationId(actorMessageId)
            .WithCausationId(actorMessageId)
            .WithTenantId(identity.TenantId)
            .WithDomain(identity.Domain)
            .WithAggregateId(identity.AggregateId)
            .WithCommandType("IncrementCounter")
            .Build();
        var rawEvent = new EventEnvelope(
            MessageId: rawMessageId,
            AggregateId: identity.AggregateId,
            AggregateType: "counter",
            TenantId: identity.TenantId,
            Domain: identity.Domain,
            SequenceNumber: 1,
            GlobalPosition: 0,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: rawCorrelationId,
            CausationId: rawMessageId,
            UserId: "story-4-5-raw-contender",
            DomainServiceVersion: "story-4-5-probe",
            EventTypeName: typeof(Hexalith.EventStore.Sample.Counter.Events.CounterIncremented).FullName!,
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: Encoding.UTF8.GetBytes("{}"),
            Extensions: new Dictionary<string, string>
            {
                ["story-4-5-contender"] = "raw",
            });
        var rawMetadata = new AggregateMetadata(1, rawEvent.Timestamp, ETag: null);

        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = _fixture.DaprHttpEndpoint,
            RequestTimeout = OperationTimeout,
        });
        IAggregateActor actor = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId),
            _fixture.AggregateActorTypeName);

        CommandProcessingResult? actorResult = null;
        Exception? actorException = null;
        DateTimeOffset? actorCompletedAtUtc = null;
        async Task ExecuteActorAsync()
        {
            try
            {
                actorResult = await actor.ProcessCommandAsync(command).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                actorException = ex;
            }
            finally
            {
                actorCompletedAtUtc = DateTimeOffset.UtcNow;
            }
        }

        HttpStatusCode? rawStatusCode = null;
        string rawResponseBody = string.Empty;
        Exception? rawException = null;
        DateTimeOffset? rawCompletedAtUtc = null;
        HttpStatusCode? genericActorKeyStatusCode = null;
        string genericActorKeyResponseBody = string.Empty;
        Exception? genericActorKeyException = null;
        DateTimeOffset? genericActorKeyCompletedAtUtc = null;
        Exception? gateWaitException = null;
        bool? actorTaskIncompleteAtGate = null;
        bool? actorTaskIncompleteAfterRaw = null;
        bool? actorTaskIncompleteAfterIntermediate = null;
        DateTimeOffset? intermediateReadAtUtc = null;
        StateReadCapture<EventEnvelope>? intermediateEventCapture = null;
        StateReadCapture<AggregateMetadata>? intermediateMetadataCapture = null;

        using AppendDurabilityRaceSession session = _fixture.AppendDurabilityRaceControl.BeginSession(rawMessageId);
        _fixture.DomainServiceInvoker.SetupAggregateHandler(
            identity,
            (targetCommand, _) =>
            {
                session.Arm(identity.ActorId, targetCommand.MessageId);
                return Task.FromResult(
                    DomainResult.Success(
                    [new Hexalith.EventStore.Sample.Counter.Events.CounterIncremented()]));
            });

        Task actorTask = ExecuteActorAsync();
        try
        {
            try
            {
                using var gateCancellation = new CancellationTokenSource(OperationTimeout);
                await session.WaitForFirstAllocationAsync(gateCancellation.Token).ConfigureAwait(true);
                actorTaskIncompleteAtGate = !actorTask.IsCompleted;
            }
            catch (Exception ex)
            {
                gateWaitException = ex;
            }

            if (gateWaitException is null)
            {
                using var http = new HttpClient
                {
                    Timeout = Timeout.InfiniteTimeSpan,
                };

                try
                {
                    using var rawCancellation = new CancellationTokenSource(OperationTimeout);
                    using var rawRequest = new HttpRequestMessage(
                        HttpMethod.Post,
                        BuildActorStateUri(identity));
                    rawRequest.Content = new StringContent(
                        CreateActorStateTransaction(rawEvent, rawMetadata, identity),
                        Encoding.UTF8,
                        "application/json");
                    using HttpResponseMessage rawResponse = await http
                        .SendAsync(rawRequest, HttpCompletionOption.ResponseContentRead, rawCancellation.Token)
                        .ConfigureAwait(true);
                    rawStatusCode = rawResponse.StatusCode;
                    rawResponseBody = await rawResponse.Content
                        .ReadAsStringAsync(rawCancellation.Token)
                        .ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    rawException = ex;
                }
                finally
                {
                    rawCompletedAtUtc = DateTimeOffset.UtcNow;
                    actorTaskIncompleteAfterRaw = !actorTask.IsCompleted;
                }

                try
                {
                    using var addressCancellation = new CancellationTokenSource(OperationTimeout);
                    using var addressRequest = new HttpRequestMessage(
                        HttpMethod.Get,
                        $"{_fixture.DaprHttpEndpoint}/v1.0/state/statestore/{Uri.EscapeDataString(identity.MetadataKey)}");
                    using HttpResponseMessage addressResponse = await http
                        .SendAsync(addressRequest, HttpCompletionOption.ResponseContentRead, addressCancellation.Token)
                        .ConfigureAwait(true);
                    genericActorKeyStatusCode = addressResponse.StatusCode;
                    genericActorKeyResponseBody = await addressResponse.Content
                        .ReadAsStringAsync(addressCancellation.Token)
                        .ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    genericActorKeyException = ex;
                }
                finally
                {
                    genericActorKeyCompletedAtUtc = DateTimeOffset.UtcNow;
                }

                intermediateEventCapture = await TryReadActorStateAsync<EventEnvelope>(
                    $"{identity.EventStreamKeyPrefix}1").ConfigureAwait(true);
                intermediateMetadataCapture = await TryReadActorStateAsync<AggregateMetadata>(
                    identity.MetadataKey).ConfigureAwait(true);
                intermediateReadAtUtc = DateTimeOffset.UtcNow;
                actorTaskIncompleteAfterIntermediate = !actorTask.IsCompleted;
            }
        }
        finally
        {
            session.Release();
            await actorTask.ConfigureAwait(true);
        }

        StateReadCapture<AggregateMetadata> finalMetadataCapture = await TryReadActorStateAsync<AggregateMetadata>(
            identity.MetadataKey).ConfigureAwait(true);
        AggregateMetadata? finalMetadata = finalMetadataCapture.Value;
        long finalSequence = finalMetadata?.CurrentSequence ?? 0;
        bool finalSequenceWithinBounds = finalSequence is >= 0 and <= 2;
        var finalEventCaptures = new List<StateReadCapture<EventEnvelope>>();
        var finalEvents = new List<EventEnvelope>();
        if (finalMetadata is not null && finalSequenceWithinBounds)
        {
            for (long sequence = 1; sequence <= finalSequence; sequence++)
            {
                StateReadCapture<EventEnvelope> capture = await TryReadActorStateAsync<EventEnvelope>(
                    $"{identity.EventStreamKeyPrefix}{sequence}").ConfigureAwait(true);
                finalEventCaptures.Add(capture);
                if (capture.Value is not null)
                {
                    finalEvents.Add(capture.Value);
                }
            }
        }

        StateReadCapture<EventEnvelope>? unexpectedNextEventCapture = null;
        if (finalSequenceWithinBounds)
        {
            unexpectedNextEventCapture = await TryReadActorStateAsync<EventEnvelope>(
                $"{identity.EventStreamKeyPrefix}{finalSequence + 1}").ConfigureAwait(true);
        }

        DateTimeOffset finalReadAtUtc = DateTimeOffset.UtcNow;
        EventEnvelope? intermediateEvent = intermediateEventCapture?.Value;
        AggregateMetadata? intermediateMetadata = intermediateMetadataCapture?.Value;
        bool rawSucceeded = rawStatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices;
        bool rawDurabilityProven = IsExactRawContender(intermediateEvent, rawEvent)
            && intermediateMetadata?.CurrentSequence == 1;
        bool intermediateDurabilityConsistent = !rawSucceeded || rawDurabilityProven;
        bool gateTimingProven = gateWaitException is null
            && session.ArmedAtUtc is not null
            && session.FirstAllocationEnteredAtUtc is not null
            && rawCompletedAtUtc is not null
            && genericActorKeyCompletedAtUtc is not null
            && intermediateReadAtUtc is not null
            && session.ReleasedAtUtc is not null
            && session.ArmedAtUtc.Value <= session.FirstAllocationEnteredAtUtc.Value
            && session.FirstAllocationEnteredAtUtc.Value <= rawCompletedAtUtc.Value
            && rawCompletedAtUtc.Value <= genericActorKeyCompletedAtUtc.Value
            && genericActorKeyCompletedAtUtc.Value <= intermediateReadAtUtc.Value
            && intermediateReadAtUtc.Value <= session.ReleasedAtUtc.Value
            && actorTaskIncompleteAtGate == true
            && actorTaskIncompleteAfterRaw == true
            && actorTaskIncompleteAfterIntermediate == true
            && session.GateInterceptions == 1
            && string.Equals(session.TargetActorId, identity.ActorId, StringComparison.Ordinal)
            && string.Equals(session.TargetMessageId, actorMessageId, StringComparison.Ordinal);
        bool keyAddressabilityProven = genericActorKeyStatusCode == HttpStatusCode.NoContent
            && genericActorKeyException is null
            && string.IsNullOrEmpty(genericActorKeyResponseBody)
            && (intermediateMetadata is not null || finalMetadata is not null);
        bool exactContendersOnly = finalEvents.All(
            item => IsExactRawContender(item, rawEvent)
                || IsExactActorContender(item, identity, actorMessageId, rawEvent.EventTypeName));
        bool finalReadsSucceeded = finalMetadataCapture.ExceptionType is null
            && finalEventCaptures.All(item => item.ExceptionType is null)
            && unexpectedNextEventCapture?.ExceptionType is null;
        bool finalShapeConsistent = finalSequenceWithinBounds
            && finalReadsSucceeded
            && exactContendersOnly
            && (finalMetadata is null
                ? finalEvents.Count == 0 && unexpectedNextEventCapture?.Value is null
                : finalEvents.Count == finalSequence
                    && finalEvents.Select(item => item.SequenceNumber)
                        .SequenceEqual(Enumerable.Range(1, finalEvents.Count).Select(value => (long)value))
                    && finalEvents.Select(item => item.SequenceNumber).Distinct().Count() == finalEvents.Count
                    && finalEvents.Select(item => item.MessageId).Distinct(StringComparer.Ordinal).Count() == finalEvents.Count
                    && finalEvents.All(item => item.Identity == identity)
                    && (finalSequence == 0 || finalMetadata.LastModified == finalEvents[^1].Timestamp)
                    && finalMetadata.ETag is null
                    && unexpectedNextEventCapture?.Value is null);

        bool rawSurvives = finalEvents.Any(item => IsExactRawContender(item, rawEvent));
        bool actorSurvives = finalEvents.Any(
            item => IsExactActorContender(item, identity, actorMessageId, rawEvent.EventTypeName));
        bool rawDurableWriteLost = rawDurabilityProven && !rawSurvives;
        bool actorAcknowledgedWriteLost = actorResult?.Accepted == true && !actorSurvives;
        int retryCount = Math.Max(0, session.AllocationAttempts - 1);
        bool actorConflictSignalled = actorResult?.FailureReason == "ConcurrencyConflict"
            || actorException is InvalidOperationException
            || actorException?.GetType().Name.Contains("ConcurrencyConflict", StringComparison.Ordinal) == true;
        AppendDurabilityRaceClassifier.Result classification = AppendDurabilityRaceClassifier.Classify(
            new AppendDurabilityRaceClassifier.Input(
                rawStatusCode is null ? null : (int)rawStatusCode.Value,
                rawException?.GetType().FullName,
                rawDurabilityProven,
                rawSurvives,
                actorSurvives,
                actorResult?.Accepted == true,
                actorResult is not null && !actorResult.Accepted,
                actorConflictSignalled,
                actorException?.GetType().FullName,
                finalSequence,
                retryCount));
        bool retryClassificationConsistent = session.AllocationAttempts >= 1
            && session.GateInterceptions == 1
            && classification.IsInternallyConsistent
            && (finalSequence != 2
                || !(rawSurvives && actorSurvives)
                || retryCount >= 1);
        bool infrastructureFree = gateWaitException is null
            && rawException is null
            && genericActorKeyException is null
            && intermediateEventCapture?.ExceptionType is null
            && intermediateMetadataCapture?.ExceptionType is null
            && finalReadsSucceeded
            && !classification.IsInfrastructureFailure;

        string stateStoreComponentSha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(_fixture.StateStoreComponentYaml))).ToLowerInvariant();
        var evidence = new
        {
            schemaVersion = 2,
            baselineCommit = "0776785f494fcefc8ad933b5b17b9c8d5cbe0513",
            providerProfile = new
            {
                daprRuntime = "1.18.1",
                stateStoreType = "state.redis",
                redisImage = "redis:6",
                appId = _fixture.AppId,
                stateStoreComponentSha256,
                stateStoreComponentYaml = _fixture.StateStoreComponentYaml,
                allocatorDecoratorType = typeof(LiveSidecarGlobalPositionAllocator).FullName,
                productionAllocatorType = LiveSidecarGlobalPositionAllocator.ProductionAllocatorTypeName,
                allocatorIdentityLimitation = "IGlobalPositionAllocator exposes count and cancellation only; the aggregate-specific handler arms immediately before the intended command persists, but the allocator call itself carries no aggregate identity.",
            },
            session = new
            {
                session.SessionId,
                session.TargetActorId,
                session.TargetMessageId,
                session.ArmedAtUtc,
                session.FirstAllocationEnteredAtUtc,
                session.ReleasedAtUtc,
                session.ArmCalls,
                session.AllocationAttempts,
                session.GateInterceptions,
                retryCount,
                actorTaskIncompleteAtGate,
                actorTaskIncompleteAfterRaw,
                actorTaskIncompleteAfterIntermediate,
            },
            aggregate = new
            {
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                identity.ActorId,
                eventKey = $"{identity.EventStreamKeyPrefix}1",
                identity.MetadataKey,
            },
            actorContender = new
            {
                messageId = actorMessageId,
                correlationId = actorMessageId,
                completedAtUtc = actorCompletedAtUtc,
                result = actorResult,
                exceptionType = actorException?.GetType().FullName,
                exceptionMessage = actorException?.Message,
                survivingEventMessageId = finalEvents.FirstOrDefault(
                    item => IsExactActorContender(item, identity, actorMessageId, rawEvent.EventTypeName))?.MessageId,
            },
            rawContender = new
            {
                messageId = rawMessageId,
                correlationId = rawCorrelationId,
                completedAtUtc = rawCompletedAtUtc,
                httpStatus = rawStatusCode is null ? null : (int?)rawStatusCode.Value,
                responseBody = rawResponseBody,
                callerSuppliedEtag = (string?)null,
                exceptionType = rawException?.GetType().FullName,
                exceptionMessage = rawException?.Message,
            },
            gateWait = new
            {
                exceptionType = gateWaitException?.GetType().FullName,
                exceptionMessage = gateWaitException?.Message,
            },
            keyAddressability = new
            {
                completedAtUtc = genericActorKeyCompletedAtUtc,
                genericStateStatus = genericActorKeyStatusCode is null
                    ? null
                    : (int?)genericActorKeyStatusCode.Value,
                genericStateBody = genericActorKeyResponseBody,
                genericStateExceptionType = genericActorKeyException?.GetType().FullName,
                genericStateExceptionMessage = genericActorKeyException?.Message,
                compositeActorRedisReadable = intermediateMetadata is not null || finalMetadata is not null,
            },
            intermediate = new
            {
                readAtUtc = intermediateReadAtUtc,
                @event = intermediateEvent,
                eventRead = intermediateEventCapture,
                metadata = intermediateMetadata,
                metadataRead = intermediateMetadataCapture,
                rawDurabilityProven,
                attemptedRegardlessOfRawStatus = gateWaitException is null,
            },
            final = new
            {
                readAtUtc = finalReadAtUtc,
                metadata = finalMetadata,
                metadataRead = finalMetadataCapture,
                finalSequenceWithinBounds,
                eventReads = finalEventCaptures,
                events = finalEvents,
                unexpectedNextEventRead = unexpectedNextEventCapture,
                unexpectedNextEventPresent = unexpectedNextEventCapture?.Value is not null,
                exactContendersOnly,
                rawSurvives,
                actorSurvives,
                rawDurableWriteLost,
                actorAcknowledgedWriteLost,
            },
            invariants = new
            {
                gateTimingProven,
                intermediateDurabilityConsistent,
                finalShapeConsistent,
                retryClassificationConsistent,
                keyAddressabilityProven,
                infrastructureFree,
            },
            observation = new
            {
                classification = classification.Name,
                classification.IsInternallyConsistent,
                classification.IsInfrastructureFailure,
                classification.RecognizedRejectionOrConflict,
                invalidOperationExceptionSurfaced = actorException is InvalidOperationException,
                concurrencyConflictSignalled = actorConflictSignalled,
            },
        };
        await WriteEvidenceAsync("append-durability-race.json", evidence).ConfigureAwait(true);

        AssertInvariant(
            intermediateDurabilityConsistent,
            "intermediate-raw-durability",
            "an acknowledged raw write must be proven durable by the exact contender and metadata while the gate is held");
        AssertInvariant(
            gateTimingProven,
            "gate-timing",
            "the exact target must arm one gate interception and the actor task must remain incomplete through all gated probes");
        AssertInvariant(
            keyAddressabilityProven,
            "key-addressability",
            "the logical metadata key must be absent from generic state while the composite actor key is readable in Redis");
        AssertInvariant(
            finalShapeConsistent,
            "final-state-consistency",
            "final Redis state must be bounded, gapless, metadata-consistent, and contain only the exact two contenders");
        AssertInvariant(
            retryClassificationConsistent,
            "conflict-retry-classification",
            "allocation telemetry and final state must support a non-infrastructure race classification");
        infrastructureFree.ShouldBeTrue(
            $"Infrastructure/probe failures must fail only after evidence is written. Classification: {classification.Name}.");
    }

    private string BuildActorStateUri(AggregateIdentity identity)
        => $"{_fixture.DaprHttpEndpoint}/v1.0/actors/"
            + $"{Uri.EscapeDataString(_fixture.AggregateActorTypeName)}/"
            + $"{Uri.EscapeDataString(identity.ActorId)}/state";

    private static string CreateActorStateTransaction(
        EventEnvelope rawEvent,
        AggregateMetadata rawMetadata,
        AggregateIdentity identity)
    {
        object[] operations =
        [
            new
            {
                operation = "upsert",
                request = new
                {
                    key = $"{identity.EventStreamKeyPrefix}1",
                    value = rawEvent,
                },
            },
            new
            {
                operation = "upsert",
                request = new
                {
                    key = identity.MetadataKey,
                    value = rawMetadata,
                },
            },
        ];
        return JsonSerializer.Serialize(operations, EvidenceJsonOptions);
    }

    private static bool IsExactRawContender(EventEnvelope? candidate, EventEnvelope expected)
        => candidate is not null
            && candidate.MessageId == expected.MessageId
            && candidate.CorrelationId == expected.CorrelationId
            && candidate.CausationId == expected.CausationId
            && candidate.Identity == expected.Identity
            && candidate.SequenceNumber == expected.SequenceNumber
            && candidate.GlobalPosition == expected.GlobalPosition
            && candidate.EventTypeName == expected.EventTypeName
            && candidate.UserId == expected.UserId
            && candidate.DomainServiceVersion == expected.DomainServiceVersion
            && candidate.Extensions is not null
            && candidate.Extensions.TryGetValue("story-4-5-contender", out string? contender)
            && contender == "raw";

    private static bool IsExactActorContender(
        EventEnvelope candidate,
        AggregateIdentity identity,
        string actorMessageId,
        string eventTypeName)
        => candidate.Identity == identity
            && candidate.CorrelationId == actorMessageId
            && candidate.CausationId == actorMessageId
            && candidate.EventTypeName == eventTypeName
            && candidate.UserId == "test-user"
            && candidate.DomainServiceVersion == "v1"
            && candidate.GlobalPosition > 0;

    private async Task<StateReadCapture<T>> TryReadActorStateAsync<T>(string key)
    {
        try
        {
            string? json = await _fixture.TryGetAggregateActorStateJsonAsync(key).ConfigureAwait(true);
            if (json is null)
            {
                return new StateReadCapture<T>(key, Found: false, Value: default, null, null);
            }

            try
            {
                T? value = JsonSerializer.Deserialize<T>(json, ReadJsonOptions);
                if (value is null)
                {
                    return new StateReadCapture<T>(
                        key,
                        Found: true,
                        Value: default,
                        typeof(JsonException).FullName,
                        $"Persisted JSON deserialized to null for {typeof(T).Name}.");
                }

                return new StateReadCapture<T>(key, Found: true, value, null, null);
            }
            catch (Exception ex)
            {
                return new StateReadCapture<T>(
                    key,
                    Found: true,
                    Value: default,
                    ex.GetType().FullName,
                    ex.Message);
            }
        }
        catch (Exception ex)
        {
            return new StateReadCapture<T>(
                key,
                Found: false,
                Value: default,
                ex.GetType().FullName,
                ex.Message);
        }
    }

    private static void AssertInvariant(bool satisfied, string mutationName, string message)
    {
        bool mutate = string.Equals(
            Environment.GetEnvironmentVariable(MutationEnvironmentVariable),
            mutationName,
            StringComparison.Ordinal);
        (mutate ? !satisfied : satisfied).ShouldBeTrue(message);
    }

    private async Task WriteEvidenceAsync(string fileName, object evidence)
    {
        string json = JsonSerializer.Serialize(evidence, EvidenceJsonOptions);
        _output.WriteLine(json);

        string? directory = Environment.GetEnvironmentVariable(EvidenceDirectoryEnvironmentVariable);
        string? mutation = Environment.GetEnvironmentVariable(MutationEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(directory) || !string.IsNullOrWhiteSpace(mutation))
        {
            return;
        }

        _ = Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, fileName),
            string.Concat(json, Environment.NewLine)).ConfigureAwait(true);
    }

    private sealed record StateReadCapture<T>(
        string Key,
        bool Found,
        T? Value,
        string? ExceptionType,
        string? ExceptionMessage);
}
