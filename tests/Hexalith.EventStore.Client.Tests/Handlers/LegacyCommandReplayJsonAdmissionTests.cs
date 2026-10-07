using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Tests.Aggregates;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Handlers;

/// <summary>Exercises JSON admission before private replay allocation and application callbacks.</summary>
public sealed class LegacyCommandReplayJsonAdmissionTests
{
    /// <summary>Checks whole-array count admission before private copying or state construction.</summary>
    [Fact]
    public void JsonArrayCountRefusesBeforeStateConstruction()
    {
        using var scope = new LegacyReplayMutationScope();
        using JsonDocument source = JsonDocument.Parse("[" + string.Join(",", Enumerable.Repeat("{}", 32_769)) + "]");
        Should.Throw<InvalidOperationException>(() => Rehydrate(source.RootElement)).Message.ShouldStartWith("LegacyArrayLimit:");
        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks raw metadata which expands under default Web encoding refuses before payload decoding.</summary>
    [Theory]
    [InlineData('<')]
    [InlineData('+')]
    [InlineData('\u00e9')]
    public void DefaultWebMetadataEscapingIsCheckedBeforeInvalidBase64(char character)
    {
        EventEnvelope envelope = Event(new Dictionary<string, string> { ["evidence"] = new string(character, 90_000) });
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonSerializer.SerializeToUtf8Bytes(new EventEnvelope(envelope.Metadata, [], envelope.Extensions), web)
            .Length.ShouldBeGreaterThan(512 * 1024);
        var relaxed = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        string json = JsonSerializer.Serialize(new DomainServiceCurrentState(null, [envelope], 0, 1), relaxed)
            .Replace(Convert.ToBase64String(envelope.Payload), "!", StringComparison.Ordinal);
        using JsonDocument source = JsonDocument.Parse(json);
        using var scope = new LegacyReplayMutationScope();
        Should.Throw<InvalidOperationException>(() => Rehydrate(source.RootElement)).Message.ShouldStartWith("MetadataLimit:");
        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks inline metadata follows the case-insensitive lookup before any private clone.</summary>
    [Theory]
    [InlineData("eventContractType")]
    [InlineData("EventContractType")]
    [InlineData("serializationFormat")]
    [InlineData("SerializationFormat")]
    public void InlineMetadataAliasesAreBoundedWithDefaultEscaping(string member)
    {
        using JsonDocument source = JsonDocument.Parse("[{\"eventTypeName\":\"LegacyReplayMutationEvent\",\"amount\":1,\""
            + member + "\":\"" + new string('<', 90_000) + "\"}]");
        using var scope = new LegacyReplayMutationScope();
        Should.Throw<InvalidOperationException>(() => Rehydrate(source.RootElement)).Message.ShouldStartWith("MetadataLimit:");
        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks contradictory wrapper aliases refuse before their payloads can be bound.</summary>
    [Theory]
    [InlineData("Events", "[{\"payload\":\"!\"}]")]
    [InlineData("SnapshotState", "[{\"payload\":\"!\"}]")]
    [InlineData("CurrentSequence", "1")]
    [InlineData("LastSnapshotSequence", "1")]
    public void ConflictingWrapperAliasesRefuseBeforeBinding(string member, string value)
    {
        string json = "{\"currentSequence\":0,\"lastSnapshotSequence\":0,\"events\":[],\"snapshotState\":null,\""
            + member + "\":" + value + "}";
        using JsonDocument source = JsonDocument.Parse(json);
        using var scope = new LegacyReplayMutationScope();
        Should.Throw<InvalidOperationException>(() => Rehydrate(source.RootElement)).Message.ShouldStartWith("CapabilityMismatch:");
        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks a later alias in a nested snapshot wrapper cannot bypass whole-source admission.</summary>
    [Fact]
    public void NestedWrapperAliasesRefuseBeforeBinding()
    {
        using JsonDocument source = JsonDocument.Parse("""
            {"currentSequence":0,"events":[],"snapshotState":
                {"currentSequence":0,"events":[],"Events":[{"payload":"!"}]}}
            """);
        using var scope = new LegacyReplayMutationScope();
        Should.Throw<InvalidOperationException>(() => Rehydrate(source.RootElement)).Message.ShouldStartWith("CapabilityMismatch:");
        scope.Constructed.ShouldBe(0);
    }

    /// <summary>Checks ordinary state JSON keeps the existing exact wrapper classification.</summary>
    [Fact]
    public void CapitalizedStatePropertiesRemainOrdinaryStateJson()
    {
        using JsonDocument source = JsonDocument.Parse("""{"Total":23,"CurrentSequence":5,"Events":[{"payload":"!"}]}""");
        using var scope = new LegacyReplayMutationScope();
        Rehydrate(source.RootElement).Total.ShouldBe(23);
        scope.Constructed.ShouldBe(1);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks inline replay uses only the exact lowercase payload member.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapitalizedInlinePayloadRemainsApplicationData(bool lowercasePayload)
    {
        string payload = lowercasePayload ? ",\"payload\":{\"amount\":5}" : string.Empty;
        using JsonDocument source = JsonDocument.Parse("[{\"eventTypeName\":\"LegacyReplayMutationEvent\",\"amount\":3,\"Payload\":\"!\""
            + payload + "}]");
        using var scope = new LegacyReplayMutationScope();
        Rehydrate(source.RootElement).Total.ShouldBe(lowercasePayload ? 5 : 3);
        scope.Applied.ShouldBe([lowercasePayload ? 5 : 3]);
    }

    /// <summary>Checks contract wrappers retain case-insensitive payload binding.</summary>
    [Fact]
    public void CapitalizedContractPayloadStillBinds()
    {
        string json = JsonSerializer.Serialize(new DomainServiceCurrentState(null, [Event(new Dictionary<string, string>())], 0, 1),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)).Replace("\"payload\":", "\"Payload\":", StringComparison.Ordinal);
        using JsonDocument source = JsonDocument.Parse(json);
        using var scope = new LegacyReplayMutationScope();
        Rehydrate(source.RootElement).Total.ShouldBe(1);
    }

    /// <summary>Checks uppercase inline members are not excluded from envelope metadata admission.</summary>
    [Fact]
    public void CapitalizedInlinePayloadIsNotExcludedFromMetadata()
    {
        using JsonDocument source = JsonDocument.Parse("[{\"eventTypeName\":\"LegacyReplayMutationEvent\",\"payload\":{\"amount\":1},\"Payload\":\""
            + new string('<', 90_000) + "\"}]");
        using var scope = new LegacyReplayMutationScope();
        Should.Throw<InvalidOperationException>(() => Rehydrate(source.RootElement)).Message.ShouldStartWith("MetadataLimit:");
        scope.Constructed.ShouldBe(0);
    }

    /// <summary>Checks capture preserves syntax already accepted by a caller's document parser.</summary>
    [Theory]
    [InlineData("state")]
    [InlineData("array")]
    [InlineData("wrapper")]
    public void AlreadyParsedCommentsAndTrailingCommasRemainReadable(string route)
    {
        string json = route switch
        {
            "state" => "{\"Total\":7,/*state*/}",
            "array" => "[{\"eventTypeName\":\"LegacyReplayMutationEvent\",/*event*/\"payload\":{\"amount\":1},},]",
            _ => JsonSerializer.Serialize(new DomainServiceCurrentState(null, [Event(new Dictionary<string, string>())], 0, 1),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)).Insert(1, "/*wrapper*/")[..^1] + ",}",
        };
        using JsonDocument source = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip,
        });
        using var scope = new LegacyReplayMutationScope();
        Rehydrate(source.RootElement).Total.ShouldBe(route == "state" ? 7 : 1);
    }

    /// <summary>Checks fixed metadata aliases refuse before built-in metadata deserialization.</summary>
    [Theory]
    [InlineData("messageId")]
    [InlineData("aggregateId")]
    [InlineData("aggregateType")]
    [InlineData("tenantId")]
    [InlineData("domain")]
    [InlineData("sequenceNumber")]
    [InlineData("globalPosition")]
    [InlineData("timestamp")]
    [InlineData("correlationId")]
    [InlineData("causationId")]
    [InlineData("userId")]
    [InlineData("domainServiceVersion")]
    [InlineData("eventTypeName")]
    [InlineData("metadataVersion")]
    [InlineData("serializationFormat")]
    [InlineData("eventContractType")]
    [InlineData("payloadVersion")]
    public void ContradictoryFixedMetadataAliasesRefuseBeforeBinding(string member)
    {
        ArgumentNullException.ThrowIfNull(member);
        EventEnvelope envelope = Event(new Dictionary<string, string>());
        EventMetadata metadata = envelope.Metadata with { EventContractType = "contract", PayloadVersion = 1 };
        string metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using JsonDocument fields = JsonDocument.Parse(metadataJson);
        JsonElement original = fields.RootElement.GetProperty(member);
        string different = original.ValueKind == JsonValueKind.Number ? "2" : "\"different\"";
        string alias = char.ToUpperInvariant(member[0]) + member[1..];
        string json = "{\"currentSequence\":1,\"events\":[{\"metadata\":" + metadataJson[..^1]
            + ",\"" + alias + "\":" + different + "},\"payload\":\"" + Convert.ToBase64String(envelope.Payload) + "\"}]}";
        using JsonDocument source = JsonDocument.Parse(json);
        using var scope = new LegacyReplayMutationScope();
        Should.Throw<InvalidOperationException>(() => Rehydrate(source.RootElement)).Message.ShouldStartWith("CapabilityMismatch:");
        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks case-sensitive retained extension keys remain valid independent dictionary entries.</summary>
    [Fact]
    public void CaseVariantExtensionKeysRetainTheirOriginalGrammar()
    {
        EventEnvelope envelope = Event(new Dictionary<string, string> { ["trace"] = "first", ["Trace"] = "second" });
        string json = JsonSerializer.Serialize(new DomainServiceCurrentState(null, [envelope], 0, 1), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using JsonDocument source = JsonDocument.Parse(json);
        using var scope = new LegacyReplayMutationScope();
        Rehydrate(source.RootElement).Total.ShouldBe(1);
        source.RootElement.GetProperty("events")[0].GetProperty("extensions").EnumerateObject().Count().ShouldBe(2);
    }

    /// <summary>Checks bounded name matching preserves mixed-case and escaped fixed aliases.</summary>
    [Theory]
    [InlineData("EvEnTtYpEnAmE", false)]
    [InlineData("\\u0045vEnTtYpEnAmE", false)]
    [InlineData("EvEnTtYpEnAmE", true)]
    [InlineData("\\u0045vEnTtYpEnAmE", true)]
    public void EscapedAndMixedCaseMetadataAliasesRetainTheirMeaning(string member, bool conflicting)
    {
        string json = JsonSerializer.Serialize(new DomainServiceCurrentState(null, [Event(new Dictionary<string, string>())], 0, 1),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        string value = conflicting ? "different" : nameof(LegacyReplayMutationEvent);
        json = json.Replace("\"metadataVersion\":", "\"" + member + "\":\"" + value + "\",\"metadataVersion\":", StringComparison.Ordinal);
        using JsonDocument source = JsonDocument.Parse(json);
        using var scope = new LegacyReplayMutationScope();
        if (conflicting)
        {
            Should.Throw<InvalidOperationException>(() => Rehydrate(source.RootElement)).Message.ShouldStartWith("CapabilityMismatch:");
            scope.Constructed.ShouldBe(0);
        }
        else
        {
            Rehydrate(source.RootElement).Total.ShouldBe(1);
        }
    }

    /// <summary>Checks a large ignored metadata name remains accepted without repeated alias-name decoding.</summary>
    [Fact]
    public void LargeIgnoredMetadataNameRemainsCompatible()
    {
        string key = new('k', 400_000);
        string json = JsonSerializer.Serialize(new DomainServiceCurrentState(null, [Event(new Dictionary<string, string>())], 0, 1),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json = json.Replace("\"metadataVersion\":", "\"" + key + "\":0,\"metadataVersion\":", StringComparison.Ordinal);
        using JsonDocument source = JsonDocument.Parse(json);
        using var scope = new LegacyReplayMutationScope();
        Rehydrate(source.RootElement).Total.ShouldBe(1);
        source.RootElement.GetProperty("events")[0].GetProperty("metadata").TryGetProperty(key, out _).ShouldBeTrue();
    }

    /// <summary>Checks equivalent raw and escaped ignored metadata names share the emitted metadata limit.</summary>
    [Fact]
    public void RawAndEscapedIgnoredMetadataNamesRemainEquivalent()
    {
        string key = new('k', 100_000);
        string json = JsonSerializer.Serialize(new DomainServiceCurrentState(null, [Event(new Dictionary<string, string>())], 0, 1),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json = json.Replace("\"metadataVersion\":", "\"" + key + "\":0,\"metadataVersion\":", StringComparison.Ordinal);
        string escaped = json.Replace(key, string.Concat(Enumerable.Repeat("\\u006b", key.Length)), StringComparison.Ordinal);
        using JsonDocument rawSource = JsonDocument.Parse(json);
        using JsonDocument escapedSource = JsonDocument.Parse(escaped);
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        byte[] rawImage = JsonSerializer.SerializeToUtf8Bytes(rawSource.RootElement.GetProperty("events")[0], web);
        byte[] escapedImage = JsonSerializer.SerializeToUtf8Bytes(escapedSource.RootElement.GetProperty("events")[0], web);
        rawImage.ShouldBe(escapedImage);
        rawImage.Length.ShouldBeLessThan(512 * 1024);
        using var scope = new LegacyReplayMutationScope();
        Rehydrate(rawSource.RootElement).Total.ShouldBe(1);
        Rehydrate(escapedSource.RootElement).Total.ShouldBe(1);
        scope.Applied.ShouldBe([1, 1]);
    }

    /// <summary>Checks exact metadata limits using independently serialized Web images and escaped keys/values.</summary>
    [Theory]
    [InlineData(true, -1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(false, -1)]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    public void SerializedMetadataBoundaryIsInclusive(bool wrapper, int boundaryOffset)
    {
        const int limit = 512 * 1024;
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var relaxed = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        Dictionary<string, string> extras = new() { ["<+é"] = "\n\"😀", ["padding"] = string.Empty };
        EventEnvelope envelope = Event(extras);
        Dictionary<string, object?> inline = new()
        {
            ["eventTypeName"] = nameof(LegacyReplayMutationEvent), ["payload"] = string.Empty,
            ["<+é"] = "\n\"😀", ["padding"] = string.Empty,
        };
        int initialLength = wrapper
            ? JsonSerializer.SerializeToUtf8Bytes(new EventEnvelope(envelope.Metadata, [], extras), web).Length
            : JsonSerializer.SerializeToUtf8Bytes(inline, web).Length;
        string padding = new('a', limit + boundaryOffset - initialLength);
        extras["padding"] = padding;
        inline["padding"] = padding;
        int independentlyEncoded = wrapper
            ? JsonSerializer.SerializeToUtf8Bytes(new EventEnvelope(envelope.Metadata, [], extras), web).Length
            : JsonSerializer.SerializeToUtf8Bytes(inline, web).Length;
        independentlyEncoded.ShouldBe(limit + boundaryOffset);
        inline["payload"] = Convert.ToBase64String(envelope.Payload);
        string json = wrapper
            ? JsonSerializer.Serialize(new DomainServiceCurrentState(null, [new EventEnvelope(envelope.Metadata, envelope.Payload, extras)], 0, 1), relaxed)
            : "[" + JsonSerializer.Serialize(inline, relaxed) + "]";
        // Escape a known property name independently of the serializer's encoder.
        json = json.Replace("\"eventTypeName\"", "\"event\\u0054ypeName\"", StringComparison.Ordinal);
        using JsonDocument source = JsonDocument.Parse(json);
        using var owner = new LegacyCommandReplayInput(CancellationToken.None);
        using var scope = new LegacyReplayMutationScope();
        if (boundaryOffset > 0)
        {
            Should.Throw<InvalidOperationException>(() => owner.CaptureJson(source.RootElement, reserveEvents: true))
                .Message.ShouldStartWith("MetadataLimit:");
        }
        else
        {
            JsonElement captured = owner.CaptureJson(source.RootElement, reserveEvents: true);
            captured.ValueKind.ShouldBe(wrapper ? JsonValueKind.Object : JsonValueKind.Array);
        }

        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks no-payload inline metadata aliases have the same inclusive default-Web boundary.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void SerializedInlineMetadataWithoutPayloadBoundaryIsInclusive(int boundaryOffset)
    {
        const int limit = 512 * 1024;
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Dictionary<string, string> metadata = new()
        {
            ["eventTypeName"] = nameof(LegacyReplayMutationEvent), ["EventContractType"] = "<+é\n\"😀",
        };
        int initialLength = JsonSerializer.SerializeToUtf8Bytes(metadata, web).Length;
        metadata["EventContractType"] += new string('a', limit + boundaryOffset - initialLength);
        JsonSerializer.SerializeToUtf8Bytes(metadata, web).Length.ShouldBe(limit + boundaryOffset);
        string json = JsonSerializer.Serialize(metadata,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
            .Replace("\"EventContractType\"", "\"\\u0045ventContractType\"", StringComparison.Ordinal);
        using JsonDocument source = JsonDocument.Parse("[" + json + "]");
        using var owner = new LegacyCommandReplayInput(CancellationToken.None);
        if (boundaryOffset > 0)
        {
            Should.Throw<InvalidOperationException>(() => owner.CaptureJson(source.RootElement, reserveEvents: true))
                .Message.ShouldStartWith("MetadataLimit:");
        }
        else
        {
            owner.CaptureJson(source.RootElement, reserveEvents: true).GetArrayLength().ShouldBe(1);
        }
    }

    /// <summary>Checks decoded token tables share admission before parsing or invoking domain code.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TokenDenseDecodedPayloadRefusesBeforeParsingOrCallbacks(bool contractEnvelope)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("{\"amount\":1,\"values\":[" + string.Concat(Enumerable.Repeat("0,", 2_097_152)) + "0]}");
        bytes.Length.ShouldBeLessThan(64 * 1024 * 1024);
        using var scope = new LegacyReplayMutationScope();
        if (contractEnvelope)
        {
            EventEnvelope original = Event(new Dictionary<string, string>());
            var envelope = new EventEnvelope(original.Metadata, bytes, original.Extensions);
            Should.Throw<InvalidOperationException>(() => Rehydrate(new[] { envelope })).Message.ShouldStartWith("LegacyArrayLimit:");
        }
        else
        {
            using JsonDocument source = JsonDocument.Parse("[{\"eventTypeName\":\"LegacyReplayMutationEvent\",\"payload\":\""
                + Convert.ToBase64String(bytes) + "\"}]");
            Should.Throw<InvalidOperationException>(() => Rehydrate(source.RootElement)).Message.ShouldStartWith("LegacyArrayLimit:");
            source.RootElement[0].GetProperty("payload").GetBytesFromBase64().ShouldBe(bytes);
        }

        bytes[0].ShouldBe((byte)'{');
        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks enumerable JsonElement entries share aggregate readable admission before domain callbacks.</summary>
    [Fact]
    public void IndividualEnumerableJsonEventsShareTheReadableCeiling()
    {
        const int typedBytes = 50 * 1024 * 1024;
        const int jsonPaddingBytes = 8 * 1024 * 1024;
        EventEnvelope original = Event(new Dictionary<string, string>());
        byte[] bytes = new byte[typedBytes];
        Array.Fill(bytes, (byte)' ');
        "{\"amount\":1}"u8.CopyTo(bytes);
        var envelope = new EventEnvelope(original.Metadata, bytes, original.Extensions);
        string padding = new('a', jsonPaddingBytes);
        using JsonDocument source = JsonDocument.Parse("{\"eventTypeName\":\"LegacyReplayMutationEvent\",\"amount\":1,\"padding\":\"" + padding + "\"}");
        (typedBytes + 2L * jsonPaddingBytes).ShouldBeGreaterThan(64L * 1024 * 1024);
        using var scope = new LegacyReplayMutationScope();
        using var owner = new LegacyCommandReplayInput(CancellationToken.None);
        Should.Throw<InvalidOperationException>(() => owner.CaptureEvents(new object[] { envelope, source.RootElement, source.RootElement }))
            .Message.ShouldStartWith("LegacyArrayLimit:");
        owner.Dispose();
        source.RootElement.GetProperty("padding").GetString()!.Length.ShouldBe(padding.Length);
        envelope.Payload[0].ShouldBe((byte)'{');
        envelope.Payload[^1].ShouldBe((byte)' ');
        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks fixed wrapper binding preserves snapshot values and Web numeric-string compatibility.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonSnapshotAndTailPreserveTheOriginalWebBinding(bool numericStrings)
    {
        string number = numericStrings ? "\"6\"" : "6";
        string eventJson = JsonSerializer.Serialize(Event(new Dictionary<string, string>()), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using JsonDocument source = JsonDocument.Parse("{\"currentSequence\":" + number + ",\"lastSnapshotSequence\":" + number
            + ",\"events\":[" + eventJson + "],\"snapshotState\":{\"Total\":7}}");
        using var scope = new LegacyReplayMutationScope();
        Rehydrate(source.RootElement).Total.ShouldBe(8);
        scope.Applied.ShouldBe([1]);
        source.RootElement.GetProperty("snapshotState").GetProperty("Total").GetInt32().ShouldBe(7);
    }

    /// <summary>Checks all nested readable payloads are measured before snapshot-aware binding.</summary>
    [Fact]
    public void NestedJsonHistoriesShareTheReadableCeilingBeforeBinding()
    {
        string payload = new('A', 4 * ((32 * 1024 * 1024 + 2) / 3));
        string item = "{\"payload\":\"" + payload + "\"}";
        using JsonDocument source = JsonDocument.Parse("{\"currentSequence\":2,\"events\":[" + item
            + "],\"snapshotState\":{\"currentSequence\":1,\"events\":[" + item + "]}}");
        using var scope = new LegacyReplayMutationScope();
        Should.Throw<InvalidOperationException>(() => Rehydrate(source.RootElement)).Message.ShouldStartWith("LegacyArrayLimit:");
        scope.Constructed.ShouldBe(0);
        scope.Applied.ShouldBeEmpty();
    }

    /// <summary>Checks private JSON survives source disposal and decoded plaintext is cleared at scope disposal.</summary>
    [Fact]
    public void JsonAndDecodedPayloadOwnershipEndsWithTheReplayOwner()
    {
        using var owner = new LegacyCommandReplayInput(CancellationToken.None);
        JsonElement captured;
        using (JsonDocument source = JsonDocument.Parse("""[{"eventTypeName":"event","payload":"YWJj"}]"""))
        {
            captured = owner.CaptureJson(source.RootElement, reserveEvents: true);
        }

        byte[] bytes = owner.DecodePayload(captured[0].GetProperty("payload"));
        bytes.ShouldBe("abc"u8.ToArray());
        owner.Dispose();
        bytes.ShouldAllBe(static value => value == 0);
        Should.Throw<ObjectDisposedException>(() => captured.GetArrayLength());
    }

    /// <summary>Checks all earlier retained bytes/documents are released when later work refuses, fails or cancels.</summary>
    [Theory]
    [InlineData("refusal")]
    [InlineData("failure")]
    [InlineData("cancellation")]
    public void LaterFailureClearsEarlierOwnedBuffersAndDocuments(string outcome)
    {
        using JsonDocument source = JsonDocument.Parse("""[{"eventTypeName":"event","payload":"eyJhbW91bnQiOjF9"}]""");
        string original = source.RootElement.GetRawText();
        using var cancellation = new CancellationTokenSource();
        List<byte[]> retained = [];
        JsonElement captured = default;
        JsonDocument? payloadDocument = null;
        Exception error = Should.Throw<Exception>(() =>
        {
            using var owner = new LegacyCommandReplayInput(cancellation.Token);
            captured = owner.CaptureJson(source.RootElement, reserveEvents: true);
            byte[] bytes = owner.DecodePayload(captured[0].GetProperty("payload"));
            payloadDocument = owner.ParsePayload(bytes);
            if (outcome == "failure")
            {
                using JsonDocument malformed = JsonDocument.Parse("""[{"eventTypeName":"event","payload":"e2ludmFsaWQ="}]""");
                JsonElement second = owner.CaptureJson(malformed.RootElement, reserveEvents: true);
                byte[] invalid = owner.DecodePayload(second[0].GetProperty("payload"));
                retained.AddRange(PrivateBuffers(owner));
                using JsonDocument ignored = owner.ParsePayload(invalid);
            }
            else
            {
                retained.AddRange(PrivateBuffers(owner));
                if (outcome == "cancellation")
                {
                    cancellation.Cancel();
                    using JsonDocument ignored = owner.ParsePayload(bytes);
                }
                else
                {
                    using JsonDocument excessive = JsonDocument.Parse("[{\"eventTypeName\":\"event\",\"SerializationFormat\":\""
                        + new string('<', 90_000) + "\"}]");
                    _ = owner.CaptureJson(excessive.RootElement, reserveEvents: true);
                }
            }
        });
        if (outcome == "cancellation")
        {
            error.ShouldBeOfType<OperationCanceledException>().CancellationToken.ShouldBe(cancellation.Token);
        }
        else if (outcome == "failure") { (error is JsonException).ShouldBeTrue(); }
        else { error.ShouldBeOfType<InvalidOperationException>().Message.ShouldStartWith("MetadataLimit:"); }
        retained.Count.ShouldBeGreaterThanOrEqualTo(2);
        foreach (byte[] buffer in retained) { buffer.ShouldAllBe(static value => value == 0); }
        Should.Throw<ObjectDisposedException>(() => captured.GetArrayLength());
        Should.Throw<ObjectDisposedException>(() => payloadDocument!.RootElement.GetRawText());
        source.RootElement.GetRawText().ShouldBe(original);
    }

    /// <summary>Checks exact escaped base64 measurement against independent framework decoding.</summary>
    [Theory]
    [InlineData("\"YWJj\"")]
    [InlineData("\"\\u0059WJj\"")]
    [InlineData("\"\\u002fw==\"")]
    [InlineData("\"YQ==\\n\"")]
    [InlineData("\" YW Jj \"")]
    [InlineData("\"\\u0020YW\\tJj\\r\\n\"")]
    [InlineData("\"\"")]
    public void Base64MeasurementMatchesTheFrameworkDecoder(string json)
    {
        using JsonDocument source = JsonDocument.Parse(json);
        byte[] expected = source.RootElement.GetBytesFromBase64();
        LegacyCommandReplayJsonAdmission.DecodedBase64Length(source.RootElement, CancellationToken.None)
            .ShouldBe(expected.LongLength);
        byte[] decoded = new byte[expected.Length];
        LegacyCommandReplayJsonAdmission.DecodeBase64(source.RootElement, decoded, CancellationToken.None);
        decoded.ShouldBe(expected);
    }

    /// <summary>Checks escaped base64 allocates the admitted output without an additional unescape-sized heap buffer.</summary>
    [Fact]
    public void EscapedBase64UsesOnlyTheAdmittedOutputBuffer()
    {
        using JsonDocument source = JsonDocument.Parse("[{\"eventTypeName\":\"event\",\"payload\":\""
            + string.Concat(Enumerable.Repeat("\\u0041", 200_000)) + "\"}]");
        using var owner = new LegacyCommandReplayInput(CancellationToken.None);
        JsonElement captured = owner.CaptureJson(source.RootElement, reserveEvents: true);
        long before = GC.GetAllocatedBytesForCurrentThread();
        byte[] decoded = owner.DecodePayload(captured[0].GetProperty("payload"));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        decoded.Length.ShouldBe(150_000);
        allocated.ShouldBeLessThan(decoded.LongLength + 16 * 1024);
        owner.Dispose();
        decoded.ShouldAllBe(static value => value == 0);
    }

    /// <summary>Checks malformed escaped/unescaped base64 is rejected by the incremental decoder.</summary>
    [Theory]
    [InlineData("\"YWJ\"")]
    [InlineData("\"YWJ!\"")]
    [InlineData("\"YQ==YQ==\"")]
    [InlineData("\"YQ==\\u00a0\"")]
    [InlineData("\"YQ==\\f\"")]
    public void IncrementalBase64RejectsTheExistingInvalidGrammar(string json)
    {
        using JsonDocument source = JsonDocument.Parse(json);
        Should.Throw<FormatException>(() => source.RootElement.GetBytesFromBase64());
        Should.Throw<FormatException>(() => LegacyCommandReplayJsonAdmission.DecodedBase64Length(source.RootElement, CancellationToken.None));
    }

    /// <summary>Checks no decode may consume capacity which the owner has not admitted.</summary>
    [Fact]
    public void UnadmittedPayloadDecodeRefusesBeforeAllocation()
    {
        using JsonDocument source = JsonDocument.Parse("\"YWJj\"");
        using var owner = new LegacyCommandReplayInput(CancellationToken.None);
        Should.Throw<InvalidOperationException>(() => owner.DecodePayload(source.RootElement)).Message.ShouldStartWith("LegacyArrayLimit:");
    }

    /// <summary>Checks cancellation wins before reading even a disposed source document.</summary>
    [Fact]
    public void CancellationBeforeAdmissionPreservesTheOriginalToken()
    {
        using var cancellation = new CancellationTokenSource();
        using var owner = new LegacyCommandReplayInput(cancellation.Token);
        JsonElement source;
        using (JsonDocument document = JsonDocument.Parse("[]")) { source = document.RootElement; }
        cancellation.Cancel();
        Should.Throw<OperationCanceledException>(() => owner.CaptureJson(source, reserveEvents: true))
            .CancellationToken.ShouldBe(cancellation.Token);
    }

    private static LegacyReplayMutationState Rehydrate(object source)
        => DomainProcessorStateRehydrator.RehydrateState<LegacyReplayMutationState>(source,
            DomainProcessorStateRehydrator.DiscoverApplyMethods(typeof(LegacyReplayMutationState)))!;

    private static List<byte[]> PrivateBuffers(LegacyCommandReplayInput owner)
        => (List<byte[]>)typeof(LegacyCommandReplayInput).GetField("_payloads", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

    private static EventEnvelope Event(IReadOnlyDictionary<string, string> extensions)
        => new(new EventMetadata("message", "aggregate", "mutation", "tenant", "domain", 1, 1,
            DateTimeOffset.UnixEpoch, "correlation", "cause", "user", "1", nameof(LegacyReplayMutationEvent), 1, "json"),
            "{\"amount\":1}"u8.ToArray(), extensions);
}
