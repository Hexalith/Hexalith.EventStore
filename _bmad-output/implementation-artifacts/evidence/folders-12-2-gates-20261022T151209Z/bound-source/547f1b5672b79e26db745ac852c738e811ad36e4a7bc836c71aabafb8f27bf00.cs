using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Security checks for the gateway's short-lived human authority handoff.</summary>
public class HumanAuthorityReceiptCodecTests
{
    private static readonly string _key = Convert.ToBase64String(Enumerable.Repeat((byte)17, 32).ToArray());
    private static readonly DateTimeOffset _now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidReceiptBindsAuthenticatedClaimsAndExactCommand()
    {
        CommandEnvelope command = Command();
        string receipt = HumanAuthorityReceiptCodec.Issue(command, "tenant-a", "human-a", ["folders:write"], _key, _now);

        HumanAuthorityReceiptCodec.TryVerify(receipt, command, _key, _now.AddSeconds(60), out HumanAuthorityReceiptClaims? claims).ShouldBeTrue();
        claims.ShouldNotBeNull();
        claims.Tenant.ShouldBe("tenant-a");
        claims.Subject.ShouldBe("human-a");
        claims.Permissions.ShouldBe(["folders:write"]);
        claims.InvocationId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TamperedOrMismatchedReceiptsFailClosed()
    {
        CommandEnvelope command = Command();
        string receipt = HumanAuthorityReceiptCodec.Issue(command, "tenant-a", "human-a", ["folders:write"], _key, _now);
        string wrongKey = Convert.ToBase64String(Enumerable.Repeat((byte)18, 32).ToArray());
        string tampered = (receipt[0] == 'A' ? "B" : "A") + receipt[1..];

        HumanAuthorityReceiptCodec.TryVerify(tampered, command, _key, _now, out _).ShouldBeFalse();
        HumanAuthorityReceiptCodec.TryVerify(receipt, command, wrongKey, _now, out _).ShouldBeFalse();
        HumanAuthorityReceiptCodec.TryVerify(receipt, command with { TenantId = "tenant-b" }, _key, _now, out _).ShouldBeFalse();
        HumanAuthorityReceiptCodec.TryVerify(receipt, command with { UserId = "human-b" }, _key, _now, out _).ShouldBeFalse();
        HumanAuthorityReceiptCodec.TryVerify(receipt, command with { CommandType = "DeleteFolder" }, _key, _now, out _).ShouldBeFalse();
        HumanAuthorityReceiptCodec.TryVerify(receipt, command with { Domain = "orders" }, _key, _now, out _).ShouldBeFalse();
        HumanAuthorityReceiptCodec.TryVerify(receipt, command with { Payload = Encoding.UTF8.GetBytes("different") }, _key, _now, out _).ShouldBeFalse();
        HumanAuthorityReceiptCodec.TryVerify(receipt, command with { MessageId = "another-message" }, _key, _now, out _).ShouldBeFalse();
        HumanAuthorityReceiptCodec.TryVerify(receipt, command with { Extensions = new Dictionary<string, string> { ["taskId"] = "another-task" } }, _key, _now, out _).ShouldBeFalse();
    }

    [Fact]
    public void ExpiredOrPrematureReceiptsFailClosed()
    {
        CommandEnvelope command = Command();
        string receipt = HumanAuthorityReceiptCodec.Issue(command, "tenant-a", "human-a", [], _key, _now);

        HumanAuthorityReceiptCodec.TryVerify(receipt, command, _key, _now.AddSeconds(-1), out _).ShouldBeFalse();
        HumanAuthorityReceiptCodec.TryVerify(receipt, command, _key, _now.AddSeconds(120), out _).ShouldBeFalse();
        HumanAuthorityReceiptCodec.TryVerify(null, command, _key, _now, out _).ShouldBeFalse();
    }

    [Fact]
    public void ReceiptNeverEntersArchivedCommandOrExtensions()
    {
        SubmitCommand submit = new("message-a", "tenant-a", "folders", "folder-a", "CreateFolder",
            Encoding.UTF8.GetBytes("{}"), "correlation-a", "human-a")
        {
            HumanAuthority = new HumanAuthoritySource("tenant-a", "human-a", ["folders:write"]),
            HumanAuthorityReceipt = "secret-receipt-marker",
        };
        string archived = JsonSerializer.Serialize(submit.ToArchivedCommand());

        archived.ShouldNotContain("secret-receipt-marker");
        archived.ShouldNotContain("folders:write");
        submit.ToCommandEnvelope().Extensions.ShouldBeNull();
        JsonSerializer.Serialize(submit).ShouldNotContain("secret-receipt-marker");
    }

    private static CommandEnvelope Command() => new(
        "message-a", "tenant-a", "folders", "folder-a", "CreateFolder",
        Encoding.UTF8.GetBytes("{}"), "correlation-a", "message-a", "human-a", null);
}
