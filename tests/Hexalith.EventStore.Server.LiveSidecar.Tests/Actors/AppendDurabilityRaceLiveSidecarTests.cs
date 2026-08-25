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
    private const string UnreachableProbeEndpoint = "http://127.0.0.1:1";
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
        string? mutationArmed = Story45MutationSwitch.Armed;
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
        string genericActorKeyProbeUrl = string.Empty;
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
                // Perturbation: arm the gate for a message id the command does not carry, so the
                // recorded gate target no longer identifies the intended writer.
                string armedMessageId = Story45MutationSwitch.IsArmed("gate-targeting")
                    ? $"{targetCommand.MessageId}-mutated"
                    : targetCommand.MessageId;
                session.Arm(identity.ActorId, armedMessageId);
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

                // Perturbation: release the gate before the contending writers run, so the actor
                // is no longer held across the probes. Breaks the observed hold, not an assertion.
                if (Story45MutationSwitch.IsArmed("gate-hold"))
                {
                    session.Release();
                    await actorTask.ConfigureAwait(true);
                }
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

                // Perturbation: skip the namespace probe entirely, so the run cannot say which
                // namespace exposed the key.
                if (!Story45MutationSwitch.IsArmed("key-addressability"))
                {
                    genericActorKeyProbeUrl =
                        $"{_fixture.DaprHttpEndpoint}/v1.0/state/statestore/{Uri.EscapeDataString(identity.MetadataKey)}";
                    try
                    {
                        using var addressCancellation = new CancellationTokenSource(OperationTimeout);
                        using var addressRequest = new HttpRequestMessage(
                            HttpMethod.Get,
                            genericActorKeyProbeUrl);
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
                }

                genericActorKeyCompletedAtUtc = DateTimeOffset.UtcNow;

                intermediateEventCapture = await TryReadActorStateAsync<EventEnvelope>(
                    $"{identity.EventStreamKeyPrefix}1").ConfigureAwait(true);

                // Perturbation: skip the gated metadata half of the readback, so an acknowledged
                // raw write is never corroborated by the metadata the same transaction wrote.
                if (!Story45MutationSwitch.IsArmed("intermediate-raw-durability"))
                {
                    intermediateMetadataCapture = await TryReadActorStateAsync<AggregateMetadata>(
                        identity.MetadataKey).ConfigureAwait(true);
                }

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

        // Perturbation: skip the one-past-the-end probe, so nothing rules out an event beyond the
        // metadata sequence and the final shape becomes unclassifiable.
        StateReadCapture<EventEnvelope>? unexpectedNextEventCapture = null;
        bool nextEventProbed = false;
        if (finalSequenceWithinBounds && !Story45MutationSwitch.IsArmed("final-state-classified"))
        {
            unexpectedNextEventCapture = await TryReadActorStateAsync<EventEnvelope>(
                $"{identity.EventStreamKeyPrefix}{finalSequence + 1}").ConfigureAwait(true);
            nextEventProbed = true;
        }

        // Perturbation: send the sidecar liveness probe to an unroutable endpoint, so the run can
        // no longer claim it completed against healthy infrastructure.
        string healthProbeUrl = Story45MutationSwitch.IsArmed("infrastructure-free")
            ? $"{UnreachableProbeEndpoint}/v1.0/healthz"
            : $"{_fixture.DaprHttpEndpoint}/v1.0/healthz";
        (HttpStatusCode? sidecarHealthStatus, Exception? sidecarHealthException) =
            await ProbeSidecarHealthAsync(healthProbeUrl).ConfigureAwait(true);
        bool sidecarHealthy = sidecarHealthException is null
            && sidecarHealthStatus is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices;

        DateTimeOffset finalReadAtUtc = DateTimeOffset.UtcNow;
        EventEnvelope? intermediateEvent = intermediateEventCapture?.Value;
        AggregateMetadata? intermediateMetadata = intermediateMetadataCapture?.Value;
        bool rawSucceeded = rawStatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices;

        // The classifier only needs to know whether the raw *event* was durable while gated; the
        // invariant additionally requires the metadata written by the same transaction, so the
        // metadata-only perturbation falsifies the invariant without changing the classification.
        bool rawEventDurabilityProven = IsExactRawContender(intermediateEvent, rawEvent);
        bool rawDurabilityProven = rawEventDurabilityProven
            && intermediateMetadata?.CurrentSequence == 1;
        bool intermediateDurabilityConsistent = !rawSucceeded || rawDurabilityProven;

        // Recorded, NOT an invariant: these comparisons are stamped in sequential program order on
        // the test thread and hold regardless of sidecar behavior, so they are not evidence. Only
        // the observed hold below (actor task incomplete, release after the gated reads) is.
        bool timestampChainInProgramOrder = session.ArmedAtUtc is not null
            && session.FirstAllocationEnteredAtUtc is not null
            && rawCompletedAtUtc is not null
            && genericActorKeyCompletedAtUtc is not null
            && intermediateReadAtUtc is not null
            && session.ArmedAtUtc.Value <= session.FirstAllocationEnteredAtUtc.Value
            && session.FirstAllocationEnteredAtUtc.Value <= rawCompletedAtUtc.Value
            && rawCompletedAtUtc.Value <= genericActorKeyCompletedAtUtc.Value
            && genericActorKeyCompletedAtUtc.Value <= intermediateReadAtUtc.Value;

        bool gateHoldProven = gateWaitException is null
            && intermediateReadAtUtc is not null
            && session.ReleasedAtUtc is not null
            && intermediateReadAtUtc.Value <= session.ReleasedAtUtc.Value
            && actorTaskIncompleteAtGate == true
            && actorTaskIncompleteAfterRaw == true
            && actorTaskIncompleteAfterIntermediate == true;
        bool gateTargetingProven = gateWaitException is null
            && session.ArmCalls == 1
            && session.GateInterceptions == 1
            && string.Equals(session.TargetActorId, identity.ActorId, StringComparison.Ordinal)
            && string.Equals(session.TargetMessageId, actorMessageId, StringComparison.Ordinal);

        string compositeMetadataRedisKey = _fixture.GetAggregateActorRedisKeyForEvidence(identity.MetadataKey);
        bool compositeActorKeyReadable = intermediateMetadata is not null || finalMetadata is not null;

        // Outcome-neutral: record which namespace exposed the actor key instead of requiring the
        // profile-specific answer. The run fails only when the probe cannot be classified at all,
        // or when neither namespace was readable -- the vacuous pass the I/O matrix forbids.
        string keyAddressabilityClassification = genericActorKeyException is not null
            ? "generic-probe-failed"
            : genericActorKeyStatusCode is null
                ? "generic-probe-not-attempted"
                : genericActorKeyStatusCode == HttpStatusCode.NoContent
                    && string.IsNullOrEmpty(genericActorKeyResponseBody)
                    ? "actor-key-absent-from-generic-namespace"
                    : genericActorKeyStatusCode == HttpStatusCode.OK
                        && !string.IsNullOrEmpty(genericActorKeyResponseBody)
                        ? "actor-key-readable-through-generic-namespace"
                        : "generic-probe-unrecognized-response";
        bool keyAddressabilityProven = (string.Equals(
                keyAddressabilityClassification,
                "actor-key-absent-from-generic-namespace",
                StringComparison.Ordinal)
            || string.Equals(
                keyAddressabilityClassification,
                "actor-key-readable-through-generic-namespace",
                StringComparison.Ordinal))
            && compositeActorKeyReadable;

        bool exactContendersOnly = finalEvents.All(
            item => IsExactRawContender(item, rawEvent)
                || IsExactActorContender(item, identity, actorMessageId, rawEvent.EventTypeName));
        bool finalReadsSucceeded = finalMetadataCapture.ExceptionType is null
            && finalEventCaptures.All(item => item.ExceptionType is null)
            && unexpectedNextEventCapture?.ExceptionType is null;
        bool finalStateFullyRead = finalReadsSucceeded
            && nextEventProbed
            && (finalMetadata is null || finalEventCaptures.Count == finalSequence);

        // Demoted from a hard requirement to a recorded classification (owner decision, loop-2 D2):
        // a torn interleaving is the anomaly this spike exists to catch and must be recorded, not
        // red-gate the required live-sidecar check. Only an *unclassifiable* shape fails.
        string finalShapeClassification = ClassifyFinalShape(
            finalStateFullyRead,
            finalSequenceWithinBounds,
            finalSequence,
            finalMetadata,
            finalEvents,
            unexpectedNextEventCapture?.Value is not null,
            exactContendersOnly,
            identity);
        bool finalStateClassified = !string.Equals(
            finalShapeClassification,
            "unclassified-final-shape",
            StringComparison.Ordinal);

        // Recorded, not required: a provider that populates the metadata ETag is a first-class
        // observation for the deferred fencing decision, not a test failure. Three-state so an
        // absent metadata record is never reported as an absent ETag.
        string metadataEtagState = finalMetadata is null
            ? "metadata-absent"
            : string.IsNullOrEmpty(finalMetadata.ETag)
                ? "etag-absent"
                : "etag-present";

        bool rawSurvives = finalEvents.Any(item => IsExactRawContender(item, rawEvent));
        bool actorSurvives = finalEvents.Any(
            item => IsExactActorContender(item, identity, actorMessageId, rawEvent.EventTypeName));
        bool rawDurableWriteLost = rawDurabilityProven && !rawSurvives;
        bool actorAcknowledgedWriteLost = actorResult?.Accepted == true && !actorSurvives;
        int retryCount = Math.Max(0, session.AllocationAttempts - 1);
        bool actorConflictSignalled = actorResult?.FailureReason == "ConcurrencyConflict"
            || actorException is InvalidOperationException
            || actorException?.GetType().Name.Contains("ConcurrencyConflict", StringComparison.Ordinal) == true;
        // Perturbation: classify against a sequence the final state does not exhibit, so the
        // classification stops corroborating the observed allocation telemetry.
        long classifierSequence = Story45MutationSwitch.IsArmed("conflict-retry-classification")
            ? finalSequence + 1
            : finalSequence;
        AppendDurabilityRaceClassifier.Result classification = AppendDurabilityRaceClassifier.Classify(
            new AppendDurabilityRaceClassifier.Input(
                rawStatusCode is null ? null : (int)rawStatusCode.Value,
                rawException?.GetType().FullName,
                rawEventDurabilityProven,
                rawSurvives,
                actorSurvives,
                actorResult?.Accepted == true,
                actorResult is not null && !actorResult.Accepted,
                actorConflictSignalled,
                actorException?.GetType().FullName,
                classifierSequence,
                retryCount));
        bool retryClassificationConsistent = session.AllocationAttempts >= 1
            && session.GateInterceptions == 1
            && classifierSequence == finalSequence
            && classification.IsInternallyConsistent
            && (classifierSequence != 2
                || !(rawSurvives && actorSurvives)
                || retryCount >= 1);
        bool infrastructureFree = gateWaitException is null
            && rawException is null
            && genericActorKeyException is null
            && intermediateEventCapture?.ExceptionType is null
            && intermediateMetadataCapture?.ExceptionType is null
            && finalReadsSucceeded
            && sidecarHealthy
            && !classification.IsInfrastructureFailure;

        string observedDaprRuntime = await DaprTestContainerFixture
            .ReadObservedDaprRuntimeVersionAsync().ConfigureAwait(true);
        string observedRedisImage = await DaprTestContainerFixture
            .ReadObservedRedisImageAsync().ConfigureAwait(true);
        string observedRedisImageDigest = await DaprTestContainerFixture
            .ReadObservedRedisImageDigestAsync().ConfigureAwait(true);
        string observedRedisPersistence = await DaprTestContainerFixture
            .ReadObservedRedisPersistenceAsync().ConfigureAwait(true);
        string stateStoreComponentSha256 = Sha256Hex(_fixture.StateStoreComponentCanonicalYaml);
        var invariants = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["gate-hold"] = gateHoldProven,
            ["gate-targeting"] = gateTargetingProven,
            ["intermediate-raw-durability"] = intermediateDurabilityConsistent,
            ["key-addressability"] = keyAddressabilityProven,
            ["final-state-classified"] = finalStateClassified,
            ["conflict-retry-classification"] = retryClassificationConsistent,
            ["infrastructure-free"] = infrastructureFree,
        };
        var evidence = new
        {
            schemaVersion = 4,
            baselineCommit = "0776785f494fcefc8ad933b5b17b9c8d5cbe0513",
            mutationArmed,
            providerProfile = new
            {
                daprRuntimeObserved = observedDaprRuntime,
                daprRuntimeSource = "daprd --version, read from the exact binary this fixture launches",
                stateStoreType = "state.redis",
                redisImageObserved = observedRedisImage,
                redisImageDigestObserved = observedRedisImageDigest,
                redisPersistenceObserved = observedRedisPersistence,
                redisProbeSource = "docker inspect dapr_redis / redis-cli config get appendonly save",
                appId = _fixture.AppId,
                stateStoreComponentSha256,
                stateStoreComponentSha256Scope =
                    "SHA-256 of the generated component with the per-run scopes list removed, so the digest binds configuration identity across runs",
                stateStoreComponentCanonicalYaml = _fixture.StateStoreComponentCanonicalYaml,
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
                timestampChainInProgramOrder,
                timestampChainIsEvidence = false,
            },
            aggregate = new
            {
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                identity.ActorId,
                eventKey = $"{identity.EventStreamKeyPrefix}1",
                identity.MetadataKey,
                compositeMetadataRedisKey,
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
                genericStateProbeUrl = genericActorKeyProbeUrl,
                genericStateKey = identity.MetadataKey,
                genericStateStatus = genericActorKeyStatusCode is null
                    ? null
                    : (int?)genericActorKeyStatusCode.Value,
                genericStateBody = genericActorKeyResponseBody,
                genericStateExceptionType = genericActorKeyException?.GetType().FullName,
                genericStateExceptionMessage = genericActorKeyException?.Message,
                compositeRedisKey = compositeMetadataRedisKey,
                compositeRedisMetadata = intermediateMetadata ?? finalMetadata,
                compositeActorRedisReadable = compositeActorKeyReadable,
                classification = keyAddressabilityClassification,
            },
            intermediate = new
            {
                readAtUtc = intermediateReadAtUtc,
                @event = intermediateEvent,
                eventRead = intermediateEventCapture,
                metadata = intermediateMetadata,
                metadataRead = intermediateMetadataCapture,
                rawEventDurabilityProven,
                rawDurabilityProven,
                attemptedRegardlessOfRawStatus = gateWaitException is null,
            },
            final = new
            {
                readAtUtc = finalReadAtUtc,
                metadata = finalMetadata,
                metadataRead = finalMetadataCapture,
                metadataEtagState,
                finalSequenceWithinBounds,
                nextEventProbed,
                finalStateFullyRead,
                shapeClassification = finalShapeClassification,
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
            infrastructure = new
            {
                sidecarHealthProbeUrl = healthProbeUrl,
                sidecarHealthStatus = sidecarHealthStatus is null ? null : (int?)sidecarHealthStatus.Value,
                sidecarHealthExceptionType = sidecarHealthException?.GetType().FullName,
                sidecarHealthy,
            },
            invariants,
            observation = new
            {
                classification = classification.Name,
                classifierSequence,
                classification.IsInternallyConsistent,
                classification.IsInfrastructureFailure,
                classification.RecognizedRejectionOrConflict,
                invalidOperationExceptionSurfaced = actorException is InvalidOperationException,
                concurrencyConflictSignalled = actorConflictSignalled,
            },
        };
        await WriteEvidenceAsync("append-durability-race.json", evidence).ConfigureAwait(true);

        AssertInvariants(invariants);
    }

    /// <summary>
    /// Classifies the observed final Redis shape. Every anomaly gets its own recorded name so a
    /// torn interleaving is captured rather than failing the run; only a shape the harness did not
    /// fully read is reported as unclassifiable.
    /// </summary>
    private static string ClassifyFinalShape(
        bool finalStateFullyRead,
        bool finalSequenceWithinBounds,
        long finalSequence,
        AggregateMetadata? finalMetadata,
        List<EventEnvelope> finalEvents,
        bool unexpectedNextEventPresent,
        bool exactContendersOnly,
        AggregateIdentity identity)
    {
        if (!finalSequenceWithinBounds)
        {
            return "final-sequence-out-of-bounds";
        }

        if (!finalStateFullyRead)
        {
            return "unclassified-final-shape";
        }

        if (finalMetadata is null)
        {
            return finalEvents.Count == 0 && !unexpectedNextEventPresent
                ? "no-metadata-no-events"
                : "events-without-metadata";
        }

        if (finalEvents.Count != finalSequence)
        {
            return "metadata-sequence-without-matching-events";
        }

        if (!finalEvents.Select(item => item.SequenceNumber)
            .SequenceEqual(Enumerable.Range(1, finalEvents.Count).Select(value => (long)value)))
        {
            return "non-contiguous-event-sequence";
        }

        if (finalEvents.Select(item => item.MessageId).Distinct(StringComparer.Ordinal).Count() != finalEvents.Count)
        {
            return "duplicate-event-message-ids";
        }

        if (unexpectedNextEventPresent)
        {
            return "event-beyond-metadata-sequence";
        }

        if (!finalEvents.All(item => item.Identity == identity))
        {
            return "foreign-aggregate-identity-present";
        }

        if (!exactContendersOnly)
        {
            return "foreign-writer-present";
        }

        if (finalSequence > 0 && finalMetadata.LastModified != finalEvents[^1].Timestamp)
        {
            return "metadata-timestamp-mismatch";
        }

        return $"gapless-{finalSequence}-event-stream";
    }

    private static async Task<(HttpStatusCode? Status, Exception? Error)> ProbeSidecarHealthAsync(string url)
    {
        try
        {
            using var http = new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
            using var cancellation = new CancellationTokenSource(OperationTimeout);
            using HttpResponseMessage response = await http
                .GetAsync(url, cancellation.Token)
                .ConfigureAwait(true);
            return (response.StatusCode, null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }

    private string BuildActorStateUri(AggregateIdentity identity)
        => $"{_fixture.DaprHttpEndpoint}/v1.0/actors/"
            + $"{Uri.EscapeDataString(_fixture.AggregateActorTypeName)}/"
            + $"{Uri.EscapeDataString(identity.ActorId)}/state";

    private static string Sha256Hex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

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

    /// <summary>
    /// Asserts every named invariant in one assertion whose message enumerates <em>all</em> failed
    /// invariant tags. Attribution therefore does not depend on assertion order: the receipt names
    /// every invariant the run falsified, and the committed capture carries the same per-invariant
    /// booleans so a validator can pin the exact set a perturbation is expected to falsify.
    /// </summary>
    /// <param name="invariants">The named invariant results.</param>
    private static void AssertInvariants(IReadOnlyDictionary<string, bool> invariants)
    {
        string[] failed = [.. invariants.Where(item => !item.Value).Select(item => item.Key)];
        failed.ShouldBeEmpty(
            "Falsified Story 4.5 invariants: "
            + string.Join(" ", failed.Select(name => $"[invariant:{name}]")));
    }

    private async Task WriteEvidenceAsync(string fileName, object evidence)
    {
        string json = JsonSerializer.Serialize(evidence, EvidenceJsonOptions);
        _output.WriteLine(json);

        string? directory = Environment.GetEnvironmentVariable(EvidenceDirectoryEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(directory) || Story45MutationSwitch.Armed is not null)
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
