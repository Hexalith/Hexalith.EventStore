using System.Text.Json.Nodes;
using static Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests.OwnedPostgresContainer;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Exercises the real allocation/cleanup boundary with controlled Docker responses only; never initializes the live fixture.</summary>
public sealed class OwnedPostgresContainerTests
{
    private const string Identity = "pp-lifecycle-0123456789abcdef0123456789abcdef";
    private const string Id = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OtherId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Canary = "fixture-private-password-and-stderr-canary";

    [Theory]
    [InlineData(65432)]
    [InlineData(33445)]
    public async Task DaemonAssignedLoopbackPortIsActuallyConsumedAndOnlyTheVerifiedOwnedIdIsRemovedOnce(int allocated)
    {
        JsonObject response = JsonNode.Parse(Snapshot())!.AsObject();
        response["Ports"]!["5432/tcp"]![0]!["HostPort"] = allocated.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var fake = new Docker { PortResponse = response.ToJsonString() }; var container = fake.Container(); fake.Calls.ShouldBeEmpty();
        (await container.StartAsync(Canary)).ShouldBe(allocated);
        fake.Calls.Select(call => call.Operation).ShouldBe([Operation.Run, Operation.PortLookup]);
        string[] run = fake.Calls[0].Arguments;
        run[Array.IndexOf(run, "--publish") + 1].ShouldBe("127.0.0.1::5432");
        run[Array.IndexOf(run, "--label") + 1].ShouldBe("hexalith.pf02.owner=" + Identity);
        run.ShouldNotContain(Canary); (fake.Calls[0].Password == Canary).ShouldBeTrue();
        fake.Calls.Take(2).ShouldAllBe(call => call.Timeout == TimeSpan.FromSeconds(60));
        await container.CleanupAsync(); await container.CleanupAsync();
        fake.Calls.Select(call => call.Operation).ShouldBe([Operation.Run, Operation.PortLookup, Operation.DiscoverOwned, Operation.InspectOwned, Operation.RemoveOwned]);
        fake.Calls[2].Arguments.ShouldBe(["ps", "--all", "--quiet", "--no-trunc", "--filter", "label=hexalith.pf02.owner=" + Identity]);
        fake.Calls[^1].Arguments.ShouldBe(["rm", "--force", Id]);
        fake.Calls.Skip(2).ShouldAllBe(call => call.Timeout > TimeSpan.Zero && call.Timeout <= TimeSpan.FromSeconds(10) && call.Password == null);
    }

    [Theory]
    [InlineData("missing-ports")]
    [InlineData("null-ports")]
    [InlineData("ambiguous-binding")]
    [InlineData("malformed")]
    [InlineData("foreign-owner")]
    [InlineData("foreign-name")]
    [InlineData("foreign-id")]
    [InlineData("foreign-image")]
    [InlineData("stopped")]
    [InlineData("all-addresses")]
    [InlineData("ipv6")]
    [InlineData("zero")]
    [InlineData("over-range")]
    [InlineData("whitespace")]
    [InlineData("duplicate-owner")]
    [InlineData("extra-port")]
    [InlineData("wrong-port-type")]
    [InlineData("oversized")]
    public async Task UnprovedAllocatedPortRefusesAndStillCleansTheActualOwnedContainer(string mutation)
    {
        var fake = new Docker { PortResponse = Change(mutation) }; var container = fake.Container();
        var failure = await Should.ThrowAsync<InvalidDataException>(() => container.StartAsync(Canary));
        failure.ToString().ShouldNotContain(Canary);
        fake.Calls.Count(call => call.Operation == Operation.RemoveOwned).ShouldBe(1);
        await container.CleanupAsync(); fake.Calls.Count(call => call.Operation == Operation.RemoveOwned).ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonzeroLaunchPreservesTheFirstExitAndStderrIdentityWithOrWithoutAPartialContainer(bool created)
    {
        var fake = new Docker { LaunchFailure = Failure(Operation.Run, 125), Discovered = created ? Id + "\n" : "" };
        var container = fake.Container();
        var failure = await Should.ThrowAsync<OperationFailure>(() => container.StartAsync(Canary));
        ReferenceEquals(failure, fake.LaunchFailure).ShouldBeTrue(); failure.ExitCode.ShouldBe(125); failure.StderrSha256.ShouldBe(Hash(Canary));
        fake.Calls.Count(call => call.Operation == Operation.Run).ShouldBe(1);
        fake.Calls.Count(call => call.Operation == Operation.RemoveOwned).ShouldBe(created ? 1 : 0);
        fake.Calls.ShouldNotContain(call => call.Operation == Operation.PortLookup);
        fake.Reports.ShouldBeEmpty(); failure.ToString().ShouldNotContain(Canary);
        await container.CleanupAsync(); fake.Calls.Count(call => call.Operation == Operation.RemoveOwned).ShouldBe(created ? 1 : 0);
    }

    [Theory]
    [InlineData("ambiguous")]
    [InlineData("malformed")]
    [InlineData("foreign-owner")]
    [InlineData("foreign-name")]
    [InlineData("foreign-image")]
    [InlineData("foreign-id")]
    [InlineData("discovery-failed")]
    [InlineData("remove-failed")]
    public async Task UnknownPartialLaunchCleanupCannotReplaceTheFirstFailureOrRemoveAForeignContainer(string mutation)
    {
        var fake = new Docker { LaunchFailure = Failure(Operation.Run, 125) };
        if (mutation == "ambiguous") { fake.Discovered = Id + "\n" + OtherId + "\n"; }
        else if (mutation == "malformed") { fake.Discovered = "not-a-container-id"; }
        else if (mutation == "discovery-failed") { fake.CleanupFailure = Failure(Operation.DiscoverOwned, 1); }
        else if (mutation == "remove-failed") { fake.CleanupFailure = Failure(Operation.RemoveOwned, 1); }
        else { fake.OwnedResponse = Change(mutation); }
        var container = fake.Container(); var first = fake.LaunchFailure;
        var failure = await Should.ThrowAsync<OperationFailure>(() => container.StartAsync(Canary));
        ReferenceEquals(first, failure).ShouldBeTrue(); failure.Operation.ShouldBe(Operation.Run); failure.ExitCode.ShouldBe(125);
        failure.StderrSha256.ShouldBe(Hash(Canary)); failure.Data.Contains("PF02OwnedCleanupFailure").ShouldBeTrue();
        fake.Reports.Count.ShouldBe(1); fake.Reports.ShouldAllBe(report => !report.Contains(Canary, StringComparison.Ordinal));
        fake.Calls.Count(call => call.Operation == Operation.RemoveOwned).ShouldBe(mutation == "remove-failed" ? 1 : 0);
        int before = fake.Calls.Count; await Should.ThrowAsync<Exception>(() => container.CleanupAsync().AsTask()); fake.Calls.Count.ShouldBe(before);
    }

    [Fact]
    public async Task CleanupAfterSuccessfulLaunchReportsItsOwnFailureWithoutRetryOrSuccessfulRelabelling()
    {
        var fake = new Docker { CleanupFailure = Failure(Operation.RemoveOwned, 1) }; var container = fake.Container();
        (await container.StartAsync(Canary)).ShouldBe(65432);
        var first = await Should.ThrowAsync<OperationFailure>(() => container.CleanupAsync().AsTask());
        var second = await Should.ThrowAsync<OperationFailure>(() => container.CleanupAsync().AsTask());
        ReferenceEquals(first, second).ShouldBeTrue(); first.Operation.ShouldBe(Operation.RemoveOwned);
        fake.Calls.Count(call => call.Operation == Operation.RemoveOwned).ShouldBe(1); first.ToString().ShouldNotContain(Canary);
    }

    [Fact]
    public async Task ReadOnlyLookupCaptureIsBoundedAndHonorsCancellationWithoutAnyClient()
    {
        using var exact = new StringReader(new string('x', 8192));
        (await ReadBoundedAsync(exact, 8192, default)).Length.ShouldBe(8192);
        using var over = new StringReader(new string('x', 8193));
        await Should.ThrowAsync<InvalidDataException>(() => ReadBoundedAsync(over, 8192, default));
        using var canceled = new CancellationTokenSource(); canceled.Cancel(); using var reader = new StringReader("x");
        await Should.ThrowAsync<OperationCanceledException>(() => ReadBoundedAsync(reader, 8192, canceled.Token));
    }

    private static OperationFailure Failure(Operation operation, int exit) => new(operation, FailureStatus.Failed, exit, Hash(Canary));
    private static string Snapshot() => new JsonObject
    {
        ["Id"] = Id, ["Name"] = "/" + Identity, ["Owner"] = Identity, ["Image"] = PayloadProtectionDaprFixture.PostgresImage, ["Running"] = true,
        ["Ports"] = new JsonObject { ["5432/tcp"] = new JsonArray(new JsonObject { ["HostIp"] = "127.0.0.1", ["HostPort"] = "65432" }) },
    }.ToJsonString();
    private static string Change(string mutation)
    {
        JsonObject root = JsonNode.Parse(Snapshot())!.AsObject(); JsonObject port = root["Ports"]!["5432/tcp"]![0]!.AsObject();
        switch (mutation)
        {
            case "missing-ports": root.Remove("Ports"); break;
            case "null-ports": root["Ports"] = null; break;
            case "ambiguous-binding": root["Ports"]!["5432/tcp"]!.AsArray().Add(port.DeepClone()); break;
            case "malformed": return "{";
            case "foreign-owner": root["Owner"] = "foreign"; break;
            case "foreign-name": root["Name"] = "/foreign"; break;
            case "foreign-id": root["Id"] = OtherId; break;
            case "foreign-image": root["Image"] = "postgres:latest"; break;
            case "stopped": root["Running"] = false; break;
            case "all-addresses": port["HostIp"] = "0.0.0.0"; break;
            case "ipv6": port["HostIp"] = "::1"; break;
            case "zero": port["HostPort"] = "0"; break;
            case "over-range": port["HostPort"] = "65536"; break;
            case "whitespace": port["HostPort"] = " 65432"; break;
            case "duplicate-owner": return "{\"Owner\":\"foreign\"," + root.ToJsonString()[1..];
            case "extra-port": root["Ports"]!["80/tcp"] = new JsonArray(); break;
            case "wrong-port-type": port["HostPort"] = 65432; break;
            case "oversized": return new string('x', 8193);
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        return root.ToJsonString();
    }
    private sealed record Call(Operation Operation, string[] Arguments, string? Password, TimeSpan Timeout);
    private sealed class Docker
    {
        internal List<Call> Calls { get; } = [];
        internal List<string> Reports { get; } = [];
        internal string PortResponse { get; set; } = Snapshot();
        internal string OwnedResponse { get; set; } = Snapshot();
        internal string Discovered { get; set; } = Id + "\n";
        internal OperationFailure? LaunchFailure { get; init; }
        internal OperationFailure? CleanupFailure { get; set; }
        internal OwnedPostgresContainer Container() => new(Identity, Execute, Reports.Add);
        private Task<string> Execute(Operation operation, string[] arguments, string? password, TimeSpan timeout)
        {
            Calls.Add(new(operation, arguments, password, timeout));
            if (operation == Operation.Run && LaunchFailure is not null) { return Task.FromException<string>(LaunchFailure); }
            if (operation == CleanupFailure?.Operation) { return Task.FromException<string>(CleanupFailure); }
            return Task.FromResult(operation switch
            {
                Operation.Run => Id + "\n", Operation.PortLookup => PortResponse, Operation.DiscoverOwned => Discovered,
                Operation.InspectOwned => OwnedResponse, Operation.RemoveOwned => "", _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            });
        }
    }
}
