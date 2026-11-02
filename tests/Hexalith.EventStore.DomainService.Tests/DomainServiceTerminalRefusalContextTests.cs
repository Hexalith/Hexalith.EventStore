using System.Collections.Immutable;
using System.Text;

using Hexalith.EventStore.Contracts.Commands;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Detached refusal data preserves raw metadata within finite pre-copy budgets.</summary>
public sealed class DomainServiceTerminalRefusalContextTests
{
    [Fact]
    public void OrdinalSnapshotPreservesRawAmbiguityNullEmptyAndMalformedValuesAndIsolatesBothInputs()
    {
        var command = Command() with { Extensions = new(StringComparer.Ordinal)
        {
            ["surfaceOrigin"] = "relay", ["SurfaceOrigin"] = "", ["actorType"] = null!,
            ["serviceClientId"] = "{malformed", ["taskId"] = "safe-task", ["malformed\ud800"] = "\ud800",
        } };
        var context = new DomainServiceTerminalRefusalContext(command, 4096);
        context.Extensions!.KeyComparer.ShouldBe(StringComparer.Ordinal);
        context.Extensions.Count.ShouldBe(6);
        context.Extensions["surfaceOrigin"].ShouldBe("relay");
        context.Extensions["SurfaceOrigin"].ShouldBe("");
        context.Extensions.ContainsKey("SURFACEORIGIN").ShouldBeFalse();
        context.Extensions["actorType"].ShouldBeNull();
        context.Extensions["serviceClientId"].ShouldBe("{malformed");
        context.Extensions["malformed\ud800"].ShouldBe("\ud800");
        command.Extensions!["taskId"] = "mutated";
        command.Extensions.Clear();
        command.Payload[0] = 0;
        context.Extensions["taskId"].ShouldBe("safe-task");
        context.Payload[0].ShouldBe((byte)'{');
        Should.Throw<NotSupportedException>(() => ((IDictionary<string, string?>)context.Extensions)["taskId"] = "mutation");
        Should.Throw<NotSupportedException>(() => ((IList<byte>)context.Payload)[0] = 0);
    }

    [Fact]
    public void MissingAndEmptyExtensionSnapshotsRemainDistinct()
    {
        new DomainServiceTerminalRefusalContext(Command(), 4096).Extensions.ShouldBeNull();
        new DomainServiceTerminalRefusalContext(Command() with { Extensions = [] }, 4096).Extensions.ShouldBe(ImmutableDictionary<string, string?>.Empty);
    }

    [Theory]
    [InlineData(128)]
    [InlineData(129)]
    public void FiniteEntryCeilingIsPreflighted(int count)
    {
        var command = Command() with { Extensions = Enumerable.Range(0, count).ToDictionary(index => "key" + index, _ => "") };
        if (count == 128) new DomainServiceTerminalRefusalContext(command, 4096).Extensions!.Count.ShouldBe(count);
        else Should.Throw<InvalidOperationException>(() => new DomainServiceTerminalRefusalContext(command, 4096)).Message.ShouldStartWith("PayloadLimit:");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ScalarsPayloadAndExtensionKeysAndValuesShareOneUtf8Budget(int excess)
    {
        var command = Command() with { UserId = "é😀", Extensions = new() { ["clé😀"] = "é😀", ["null"] = null! } };
        int bytes = command.Payload.Length + new[] { command.MessageId, command.TenantId, command.Domain, command.AggregateId,
            command.CommandType, command.CorrelationId, command.CausationId, command.UserId }.Sum(value => value is null ? 0 : Encoding.UTF8.GetByteCount(value))
            + command.Extensions.Sum(entry => Encoding.UTF8.GetByteCount(entry.Key) + (entry.Value is null ? 0 : Encoding.UTF8.GetByteCount(entry.Value)));
        if (excess == 0) new DomainServiceTerminalRefusalContext(command, bytes).Extensions!["clé😀"].ShouldBe("é😀");
        else Should.Throw<InvalidOperationException>(() => new DomainServiceTerminalRefusalContext(command, bytes - excess)).Message.ShouldStartWith("PayloadLimit:");
    }

    private static CommandEnvelope Command() => new("message", "tenant-a", "counter", "aggregate", "unsupported", "{}"u8.ToArray(), "correlation", null, "user", null);
}
