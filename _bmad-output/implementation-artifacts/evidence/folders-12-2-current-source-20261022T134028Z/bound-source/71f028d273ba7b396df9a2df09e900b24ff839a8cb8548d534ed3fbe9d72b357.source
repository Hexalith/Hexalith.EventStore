using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Results;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Serialization;

/// <summary>
/// Story 4.3: the command, rehydrate, projection, pub/sub, and replay payload readers must share one
/// serializer options definition. Two guarantees are tested: behaviour (camelCase and PascalCase both
/// bind on the acceptance paths) and a source guardrail that fails if any of the five readers stops
/// naming the shared instance.
/// </summary>
public sealed class PayloadSerializationConsistencyTests {
    /// <summary>
    /// Every EventStore payload reader path. Each must contribute at least one inspected call.
    /// <c>AggregateReplayer</c> is included because it is a fifth reader: leaving it out is exactly the
    /// drift this guardrail exists to catch.
    /// </summary>
    private static readonly string[] ReaderSourcePaths = [
        "src/Hexalith.EventStore.Client/Aggregates/AggregateReplayer.cs",
        "src/Hexalith.EventStore.Client/Aggregates/EventStoreAggregate.cs",
        "src/Hexalith.EventStore.Client/Aggregates/EventStoreProjection.cs",
        "src/Hexalith.EventStore.Client/Handlers/DomainProcessorStateRehydrator.cs",
        "src/Hexalith.EventStore.Client/Subscriptions/EventStoreDomainEventProcessor.cs",
    ];

    private const string SharedOptionsToken = "EventStorePayloadSerialization.Options";

    // Longest name first so the alternation cannot stop early on a shared prefix.
    private static readonly Regex SerializerCallPattern = new(
        @"(?<![A-Za-z0-9_])(?<name>SerializeToUtf8Bytes|SerializeToElement|DeserializeAsync|SerializeAsync|Deserialize|Serialize)(?![A-Za-z0-9_])\s*(?:<[^()]*?>)?\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string CamelCasePayload = """{"name":"camel","quantity":7}""";

    private const string PascalCasePayload = """{"Name":"pascal","Quantity":9}""";

    private const string CamelCaseInlineProperties = "\"name\":\"camel\",\"quantity\":7";

    private const string PascalCaseInlineProperties = "\"Name\":\"pascal\",\"Quantity\":9";

    // --- Behaviour: both casings bind on the acceptance reader paths (command/rehydrate/project/pubsub);
    //     AggregateReplayer is covered by the five-path source guardrail below ---

    [Theory]
    [InlineData(CamelCasePayload, "camel", 7)]
    [InlineData(PascalCasePayload, "pascal", 9)]
    public async Task CommandPath_EitherCasing_BindsEveryProperty(string payloadJson, string expectedName, int expectedQuantity) {
        var aggregate = new CasingAggregate();
        var command = new CommandEnvelope(
            MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
            TenantId: "tenant-1",
            Domain: "casing",
            AggregateId: "agg-1",
            CommandType: nameof(AddThing),
            Payload: Encoding.UTF8.GetBytes(payloadJson),
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "user-1",
            Extensions: null);

        DomainResult result = await aggregate.ProcessAsync(command, null);

        result.IsSuccess.ShouldBeTrue();
        ThingAdded added = result.Events[0].ShouldBeOfType<ThingAdded>();
        added.Name.ShouldBe(expectedName);
        added.Quantity.ShouldBe(expectedQuantity);
    }

    [Theory]
    [InlineData(CamelCasePayload, "camel", 7)]
    [InlineData(PascalCasePayload, "pascal", 9)]
    public void RehydratePath_EitherCasing_BindsEveryProperty(string payloadJson, string expectedName, int expectedQuantity) {
        JsonElement events = EventArray(typeof(ThingAdded).FullName!, payloadJson);

        CasingState? state = DomainProcessorStateRehydrator.RehydrateState<CasingState>(
            events,
            ApplyMethodResolver.GetOrBuildTable(typeof(CasingState)));

        state.ShouldNotBeNull();
        state.Name.ShouldBe(expectedName);
        state.Quantity.ShouldBe(expectedQuantity);
    }

    [Theory]
    [InlineData(CamelCasePayload, "camel", 7)]
    [InlineData(PascalCasePayload, "pascal", 9)]
    public void ProjectPath_EitherCasing_BindsEveryProperty(string payloadJson, string expectedName, int expectedQuantity) {
        JsonElement events = EventArray(typeof(ThingAdded).FullName!, payloadJson);

        CasingState model = new CasingProjection().ProjectFromJson(events);

        model.Name.ShouldBe(expectedName);
        model.Quantity.ShouldBe(expectedQuantity);
    }

    [Theory]
    [InlineData(CamelCasePayload, "camel", 7)]
    [InlineData(PascalCasePayload, "pascal", 9)]
    public async Task PubSubPath_EitherCasing_BindsEveryProperty(string payloadJson, string expectedName, int expectedQuantity) {
        var handler = new CapturingThingHandler();
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IEventStoreDomainEventHandler<ThingAdded>>(handler)
            .BuildServiceProvider();
        var processor = new EventStoreDomainEventProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new Dictionary<string, Type>(StringComparer.Ordinal) {
                [typeof(ThingAdded).FullName!] = typeof(ThingAdded),
            },
            NullLogger<EventStoreDomainEventProcessor>.Instance);

        EventStoreDomainEventProcessingResult result = await processor.ProcessAsync(new EventStoreDomainEventEnvelope(
            MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
            AggregateId: "agg-1",
            TenantId: "tenant-1",
            EventTypeName: typeof(ThingAdded).FullName!,
            SequenceNumber: 1,
            Timestamp: DateTimeOffset.UnixEpoch,
            CorrelationId: "corr-1",
            SerializationFormat: "json",
            Payload: Encoding.UTF8.GetBytes(payloadJson)));

        result.ShouldBe(EventStoreDomainEventProcessingResult.Processed);
        handler.Handled.Count.ShouldBe(1);
        handler.Handled[0].Name.ShouldBe(expectedName);
        handler.Handled[0].Quantity.ShouldBe(expectedQuantity);
    }

    [Theory]
    [InlineData(CamelCaseInlineProperties, "camel", 7)]
    [InlineData(PascalCaseInlineProperties, "pascal", 9)]
    public void ProjectPath_EventWithoutPayloadWrapper_EitherCasing_BindsEveryProperty(
        string inlineProperties, string expectedName, int expectedQuantity) {
        // The payload-less branch is a second, independent reader in the same method. Without this case
        // every behavioural test emits a "payload" member, so reverting that branch alone breaks nothing.
        JsonElement events = InlineEventArray(typeof(ThingAdded).FullName!, inlineProperties);

        CasingState model = new CasingProjection().ProjectFromJson(events);

        model.Name.ShouldBe(expectedName);
        model.Quantity.ShouldBe(expectedQuantity);
    }

    [Theory]
    [InlineData(CamelCaseInlineProperties, "camel", 7)]
    [InlineData(PascalCaseInlineProperties, "pascal", 9)]
    public void RehydratePath_EventWithoutPayloadWrapper_EitherCasing_BindsEveryProperty(
        string inlineProperties, string expectedName, int expectedQuantity) {
        JsonElement events = InlineEventArray(typeof(ThingAdded).FullName!, inlineProperties);

        CasingState? state = DomainProcessorStateRehydrator.RehydrateState<CasingState>(
            events,
            ApplyMethodResolver.GetOrBuildTable(typeof(CasingState)));

        state.ShouldNotBeNull();
        state.Name.ShouldBe(expectedName);
        state.Quantity.ShouldBe(expectedQuantity);
    }

    [Theory]
    [InlineData(CamelCasePayload, "camel", 7)]
    [InlineData(PascalCasePayload, "pascal", 9)]
    public void ReplayPath_EitherCasing_BindsEveryProperty(string payloadJson, string expectedName, int expectedQuantity) {
        AggregateReconstructionResult result = AggregateReplayer.Replay<CasingState>(new AggregateReconstructionRequest(
            TenantId: "tenant-1",
            Domain: "casing",
            AggregateType: string.Empty,
            AggregateId: "agg-1",
            UpToSequence: 1,
            Events: [
                new ReplayEventEnvelope(
                    SequenceNumber: 1,
                    EventTypeName: typeof(ThingAdded).FullName!,
                    Payload: Encoding.UTF8.GetBytes(payloadJson),
                    SerializationFormat: "json",
                    MetadataVersion: 1,
                    MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
                    CorrelationId: "corr-1",
                    CausationId: null),
            ],
            IncludeTimeline: false,
            RequestId: null));

        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        result.StateJson.ShouldNotBeNull();
        result.StateJson.ShouldContain(expectedName);
        result.StateJson.ShouldContain(expectedQuantity.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void SharedOptions_AreCaseInsensitiveAndReadOnly() {
        Contracts.Serialization.EventStorePayloadSerialization.Options.PropertyNameCaseInsensitive.ShouldBeTrue();
        Contracts.Serialization.EventStorePayloadSerialization.Options.IsReadOnly.ShouldBeTrue();
    }

    // --- Source guardrail ---

    [Fact]
    public void ReaderSources_EveryPayloadBindingCall_NamesTheSharedOptionsInstance() {
        string repositoryRoot = RepositoryRoot();
        var violations = new List<string>();

        foreach (string relativePath in ReaderSourcePaths) {
            string source = ReadSource(repositoryRoot, relativePath);
            IReadOnlyList<SerializerCall> calls = FindSerializerCalls(source);

            // Per-file coverage control. A total-count control is satisfiable by one file contributing
            // every call, which is exactly how the first attempt's guardrail became vacuous.
            calls.ShouldNotBeEmpty(
                $"{relativePath} contributed no inspected serializer call. Either the file no longer binds "
                + "payloads or the scanner tokens have drifted from the source.");

            violations.AddRange(calls
                .Where(static call => !call.Arguments.Contains(SharedOptionsToken, StringComparison.Ordinal))
                .Select(call => $"{relativePath}: {call.MethodName}({call.Arguments.Trim()}) does not bind through {SharedOptionsToken}."));
        }

        violations.ShouldBeEmpty(
            "Every payload reader must bind through the shared options instance; offending call sites are listed above.");
    }

    [Fact]
    public void ReaderSources_ContainNoResidualDefaultOptionsBinding() {
        string repositoryRoot = RepositoryRoot();

        string[] offenders = [.. ReaderSourcePaths
            .Where(relativePath => ReadSource(repositoryRoot, relativePath)
                .Contains("JsonSerializerOptions.Default", StringComparison.Ordinal))];

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void ReaderSources_DefineNoLocalSerializerOptions() {
        // Argument inspection alone leaves an escape hatch: a reader could keep naming a local
        // `SerializerOptions` symbol that is re-pointed at a privately constructed options object. No new
        // serializer call would appear in any scanned file, so the argument check would stay green while
        // the path silently re-forks. Ban the definitional forms outright.
        string repositoryRoot = RepositoryRoot();
        string[] bannedDefinitions = ["new JsonSerializerOptions(", "JsonSerializerDefaults."];

        string[] offenders = [.. ReaderSourcePaths
            .SelectMany(relativePath => bannedDefinitions
                .Where(banned => ReadSource(repositoryRoot, relativePath).Contains(banned, StringComparison.Ordinal))
                .Select(banned => $"{relativePath}: defines serializer options locally via '{banned}'."))];

        offenders.ShouldBeEmpty(
            "Reader paths must consume EventStorePayloadSerialization.Options, never define their own.");
    }

    [Fact]
    public void Scanner_LocalOptionsDefinition_IsReported() {
        // Mutation control for the definitional check above.
        const string source = "private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);";

        source.Contains("JsonSerializerDefaults.", StringComparison.Ordinal).ShouldBeTrue();
        source.Contains(SharedOptionsToken, StringComparison.Ordinal).ShouldBeFalse();
    }

    // --- Guardrail self-tests: prove the scanner can go red before trusting it green ---

    [Fact]
    public void Scanner_DroppedOptionsArgument_IsReported() {
        IReadOnlyList<SerializerCall> calls = FindSerializerCalls(
            "object? x = JsonSerializer.Deserialize(command.Payload, handleInfo.CommandType);");

        calls.Count.ShouldBe(1);
        calls[0].Arguments.Contains(SharedOptionsToken, StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void Scanner_DefaultOptionsArgument_DoesNotSatisfyThePredicate() {
        // Loop-1 failure #1: a substring predicate on "SerializerOptions" is satisfied by the literal
        // JsonSerializerOptions.Default — the exact defect this story fixes.
        IReadOnlyList<SerializerCall> calls = FindSerializerCalls(
            "_ = JsonSerializer.Deserialize(payload, type, JsonSerializerOptions.Default);");

        calls.Count.ShouldBe(1);
        calls[0].Arguments.Contains(SharedOptionsToken, StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void Scanner_CompliantCall_Passes() {
        IReadOnlyList<SerializerCall> calls = FindSerializerCalls(
            "_ = JsonSerializer.Deserialize(payload, type, EventStorePayloadSerialization.Options);");

        calls.Count.ShouldBe(1);
        calls[0].Arguments.Contains(SharedOptionsToken, StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void Scanner_CountsEveryCallShapeExactlyOnce() {
        // Loop-1 failure #3: the token list missed the non-generic instance form and JsonSerializer.Serialize,
        // while ".Deserialize<" double-counted the generic form.
        const string source = """
            _ = JsonSerializer.Deserialize<Thing>(json, EventStorePayloadSerialization.Options);
            _ = JsonSerializer.Deserialize(json, type, EventStorePayloadSerialization.Options);
            _ = element.Deserialize(type, EventStorePayloadSerialization.Options);
            _ = element.Deserialize<Thing>(EventStorePayloadSerialization.Options);
            _ = JsonSerializer.Serialize(value, EventStorePayloadSerialization.Options);
            _ = JsonSerializer.SerializeToElement(value, type, EventStorePayloadSerialization.Options);
            """;

        IReadOnlyList<SerializerCall> calls = FindSerializerCalls(source);

        calls.Count.ShouldBe(6);
        calls.Select(static call => call.MethodName).ShouldBe(
            ["Deserialize", "Deserialize", "Deserialize", "Deserialize", "Serialize", "SerializeToElement"]);
        calls.ShouldAllBe(call => call.Arguments.Contains(SharedOptionsToken, StringComparison.Ordinal));
    }

    [Fact]
    public void Scanner_IgnoresCommentsAndStringLiterals() {
        // Loop-1 failure #4: an unbalanced parenthesis inside a literal or an XML doc comment desynced the
        // argument scanner, so later call sites were mis-parsed and silently passed.
        const string source = """"
            // JsonSerializer.Deserialize(payload, type);
            /* JsonSerializer.Deserialize(payload, type) ( */
            /// <see cref="JsonSerializer.Deserialize(System.Text.Json.JsonElement,System.Type)"/>
            string unbalanced = "Deserialize( )) (";
            string raw = """Deserialize( ((( """;
            char paren = '(';
            _ = JsonSerializer.Deserialize(payload, type, EventStorePayloadSerialization.Options);
            """";

        IReadOnlyList<SerializerCall> calls = FindSerializerCalls(source);

        calls.Count.ShouldBe(1);
        calls[0].Arguments.Contains(SharedOptionsToken, StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void Scanner_NestedParenthesesAndGenerics_AreCapturedWhole() {
        IReadOnlyList<SerializerCall> calls = FindSerializerCalls(
            "_ = JsonSerializer.Deserialize<Dictionary<string, int>>(Encode(a, b), EventStorePayloadSerialization.Options);");

        calls.Count.ShouldBe(1);
        calls[0].Arguments.ShouldBe("Encode(a, b), EventStorePayloadSerialization.Options");
    }

    [Fact]
    public void Scanner_MaskingPreservesParenthesisBalance() {
        // Whole-file control: if masking desynced, the masked source would not balance and every
        // argument capture after the desync would be meaningless.
        string repositoryRoot = RepositoryRoot();

        foreach (string relativePath in ReaderSourcePaths) {
            string masked = MaskLiteralsAndComments(ReadSource(repositoryRoot, relativePath));
            int depth = 0;
            int minimum = 0;
            foreach (char c in masked) {
                if (c == '(') {
                    depth++;
                }
                else if (c == ')') {
                    depth--;
                    minimum = Math.Min(minimum, depth);
                }
            }

            depth.ShouldBe(0, $"{relativePath}: masked source parentheses do not balance.");
            minimum.ShouldBe(0, $"{relativePath}: masked source closes a parenthesis that was never opened.");
        }
    }

    private static string ReadSource(string repositoryRoot, string relativePath)
        => File.ReadAllText(Path.Combine(repositoryRoot, relativePath)).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static JsonElement EventArray(string eventTypeName, string payloadJson)
        => JsonSerializer.Deserialize<JsonElement>(
            "[{\"eventTypeName\":"
            + JsonSerializer.Serialize(eventTypeName)
            + ",\"payload\":"
            + payloadJson
            + "}]");

    /// <summary>Builds an event entry with no <c>payload</c> wrapper, so the whole element is bound.</summary>
    private static JsonElement InlineEventArray(string eventTypeName, string inlineProperties)
        => JsonSerializer.Deserialize<JsonElement>(
            "[{\"eventTypeName\":"
            + JsonSerializer.Serialize(eventTypeName)
            + ","
            + inlineProperties
            + "}]");

    private static IReadOnlyList<SerializerCall> FindSerializerCalls(string source) {
        string masked = MaskLiteralsAndComments(source);
        var calls = new List<SerializerCall>();

        foreach (Match match in SerializerCallPattern.Matches(masked)) {
            int openIndex = match.Index + match.Length - 1;
            int depth = 0;
            int closeIndex = -1;
            for (int i = openIndex; i < masked.Length; i++) {
                if (masked[i] == '(') {
                    depth++;
                }
                else if (masked[i] == ')') {
                    depth--;
                    if (depth == 0) {
                        closeIndex = i;
                        break;
                    }
                }
            }

            closeIndex.ShouldBeGreaterThan(
                openIndex,
                $"Unterminated argument list for '{match.Groups["name"].Value}' at offset {match.Index}.");
            calls.Add(new SerializerCall(
                match.Groups["name"].Value,
                masked[(openIndex + 1)..closeIndex]));
        }

        return calls;
    }

    /// <summary>
    /// Blanks out comment and literal content while preserving offsets and newlines, so the parenthesis
    /// scanner cannot be desynced by a parenthesis inside a string, a char literal, or an XML doc comment.
    /// </summary>
    private static string MaskLiteralsAndComments(string source) {
        char[] result = source.ToCharArray();
        int index = 0;

        while (index < source.Length) {
            char current = source[index];

            if (current == '/' && index + 1 < source.Length && source[index + 1] == '/') {
                while (index < source.Length && source[index] != '\n') {
                    result[index++] = ' ';
                }

                continue;
            }

            if (current == '/' && index + 1 < source.Length && source[index + 1] == '*') {
                result[index++] = ' ';
                result[index++] = ' ';
                while (index < source.Length && !(source[index] == '*' && index + 1 < source.Length && source[index + 1] == '/')) {
                    if (source[index] != '\n') {
                        result[index] = ' ';
                    }

                    index++;
                }

                if (index < source.Length) {
                    result[index++] = ' ';
                    result[index++] = ' ';
                }

                continue;
            }

            if (current == '\'') {
                result[index++] = ' ';
                while (index < source.Length && source[index] != '\'') {
                    if (source[index] == '\\' && index + 1 < source.Length) {
                        result[index++] = ' ';
                    }

                    if (index < source.Length) {
                        result[index++] = ' ';
                    }
                }

                if (index < source.Length) {
                    result[index++] = ' ';
                }

                continue;
            }

            if (current == '"') {
                index = MaskString(source, result, index);
                continue;
            }

            index++;
        }

        return new string(result);
    }

    private static int MaskString(string source, char[] result, int index) {
        int openingQuotes = 0;
        while (index + openingQuotes < source.Length && source[index + openingQuotes] == '"') {
            openingQuotes++;
        }

        if (openingQuotes >= 3) {
            for (int i = 0; i < openingQuotes; i++) {
                result[index + i] = ' ';
            }

            index += openingQuotes;
            while (index < source.Length) {
                if (source[index] == '"') {
                    int run = 0;
                    while (index + run < source.Length && source[index + run] == '"') {
                        run++;
                    }

                    for (int i = 0; i < run; i++) {
                        result[index + i] = ' ';
                    }

                    index += run;
                    if (run >= openingQuotes) {
                        return index;
                    }

                    continue;
                }

                if (source[index] != '\n') {
                    result[index] = ' ';
                }

                index++;
            }

            return index;
        }

        bool verbatim = IsVerbatimStringStart(source, index);
        result[index++] = ' ';
        while (index < source.Length) {
            if (verbatim) {
                if (source[index] == '"') {
                    if (index + 1 < source.Length && source[index + 1] == '"') {
                        result[index++] = ' ';
                        result[index++] = ' ';
                        continue;
                    }

                    result[index++] = ' ';
                    return index;
                }
            }
            else {
                if (source[index] == '\\' && index + 1 < source.Length) {
                    result[index++] = ' ';
                    result[index++] = ' ';
                    continue;
                }

                if (source[index] == '"') {
                    result[index++] = ' ';
                    return index;
                }

                if (source[index] == '\n') {
                    return index;
                }
            }

            if (source[index] != '\n') {
                result[index] = ' ';
            }

            index++;
        }

        return index;
    }

    private static bool IsVerbatimStringStart(string source, int quoteIndex) {
        for (int i = quoteIndex - 1; i >= 0; i--) {
            if (source[i] == '@') {
                return true;
            }

            if (source[i] != '$') {
                return false;
            }
        }

        return false;
    }

    private static string RepositoryRoot() {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null) {
            if (File.Exists(Path.Combine(current.FullName, "Hexalith.EventStore.slnx"))) {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Hexalith.EventStore.slnx from the test output path.");
    }

    private sealed record SerializerCall(string MethodName, string Arguments);

    private sealed record AddThing(string Name, int Quantity);

    private sealed record ThingAdded(string Name, int Quantity) : IEventPayload;

    private sealed class CasingState {
        public string? Name { get; private set; }

        public int Quantity { get; private set; }

        public void Apply(ThingAdded e) {
            Name = e.Name;
            Quantity = e.Quantity;
        }
    }

    private sealed class CasingProjection : EventStoreProjection<CasingState>;

    private sealed class CasingAggregate : EventStoreAggregate<CasingState> {
        public static DomainResult Handle(AddThing command, CasingState? state)
            => DomainResult.Success(new IEventPayload[] { new ThingAdded(command.Name, command.Quantity) });
    }

    private sealed class CapturingThingHandler : IEventStoreDomainEventHandler<ThingAdded> {
        public List<ThingAdded> Handled { get; } = [];

        public Task HandleAsync(ThingAdded @event, EventStoreDomainEventContext context, CancellationToken cancellationToken = default) {
            Handled.Add(@event);
            return Task.CompletedTask;
        }
    }
}
