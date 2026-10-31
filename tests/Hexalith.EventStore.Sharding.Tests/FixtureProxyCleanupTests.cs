using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Exercises independent proxy teardown/absence checks and receipts without Docker.</summary>
public sealed class FixtureProxyCleanupTests
{
    /// <summary>A retained accept failure cannot skip the independent listener probe or scratch removal.</summary>
    [Fact]
    public async Task ProxyDisposalFailureStillVerifiesAbsenceAndContinuesOwnedCleanupAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "sharding-proxy-cleanup-" + Guid.NewGuid().ToString("N"));
        var proxy = new PostgresFaultProxy(0, 1, CancellationToken.None);
        var fixture = new ShardingFixture();
        try
        {
            proxy.Start();
            Prepare(fixture, root, proxy, proxy.Port);
            proxy.InterruptListener();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!proxy.AcceptFailed) await Task.Delay(10, deadline.Token);
            InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() => fixture.DisposeAsync().AsTask());
            failure.Message.ShouldContain("provider proxy disposal: PostgreSQL proxy protocol failed.");
            proxy.Stopped.ShouldBeTrue();
            Directory.Exists(Path.Combine(root, "scratch")).ShouldBeFalse();
            fixture.CleanupVerified.ShouldBeFalse();
            fixture.CommandCount.ShouldBe(0);
            using JsonDocument receipt = ReadReceipt(root);
            receipt.RootElement.GetProperty("proxyListenerAndSessionsAbsent").GetBoolean().ShouldBeTrue();
            receipt.RootElement.GetProperty("scratchRemoved").GetBoolean().ShouldBeTrue();
            receipt.RootElement.GetProperty("verified").GetBoolean().ShouldBeFalse();
            receipt.RootElement.GetProperty("failures").EnumerateArray().Select(item => item.GetString())
                .ShouldContain(item => item!.Contains("provider proxy disposal:", StringComparison.Ordinal));
        }
        finally
        {
            _ = await Record.ExceptionAsync(() => fixture.DisposeAsync().AsTask());
            _ = await Record.ExceptionAsync(() => proxy.DisposeAsync().AsTask());
            Directory.Delete(root, recursive: true);
        }
        Directory.Exists(root).ShouldBeFalse();
    }

    /// <summary>False and throwing absence probes retain concrete causes while independent cleanup still completes.</summary>
    /// <param name="throwingProbe">Whether the probe throws instead of finding an occupied port.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedProxyAbsenceProbeDiagnosesCauseAndAllowsVerifiedRetryAsync(bool throwingProbe)
    {
        string root = Path.Combine(Path.GetTempPath(), "sharding-proxy-probe-" + Guid.NewGuid().ToString("N"));
        var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        var proxy = new PostgresFaultProxy(0, 1, CancellationToken.None);
        var fixture = new ShardingFixture();
        try
        {
            proxy.Start();
            Prepare(fixture, root, proxy, throwingProbe ? -1 : ((IPEndPoint)occupied.LocalEndpoint).Port);
            InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() => fixture.DisposeAsync().AsTask());
            failure.Message.ShouldContain("provider proxy absence check:");
            if (!throwingProbe) failure.Message.ShouldContain("bound port accepts connections");
            Directory.Exists(Path.Combine(root, "scratch")).ShouldBeFalse();
            fixture.CleanupVerified.ShouldBeFalse();
            fixture.CommandCount.ShouldBe(0);
            using (JsonDocument receipt = ReadReceipt(root))
            {
                receipt.RootElement.GetProperty("proxyListenerAndSessionsAbsent").GetBoolean().ShouldBeFalse();
                receipt.RootElement.GetProperty("verified").GetBoolean().ShouldBeFalse();
                receipt.RootElement.GetProperty("failures").GetArrayLength().ShouldBeGreaterThan(0);
            }

            SetField(fixture, "_proxyPort", proxy.Port);
            await fixture.DisposeAsync();
            fixture.CleanupVerified.ShouldBeTrue();
            using JsonDocument retried = ReadReceipt(root);
            retried.RootElement.GetProperty("proxyListenerAndSessionsAbsent").GetBoolean().ShouldBeTrue();
            retried.RootElement.GetProperty("verified").GetBoolean().ShouldBeTrue();
            retried.RootElement.GetProperty("failureHistory").EnumerateArray().Select(item => item.GetString())
                .ShouldContain(item => item!.Contains("provider proxy absence check:", StringComparison.Ordinal));
        }
        finally
        {
            occupied.Stop();
            SetField(fixture, "_proxyPort", proxy.Port);
            _ = await Record.ExceptionAsync(() => fixture.DisposeAsync().AsTask());
            await proxy.DisposeAsync();
            Directory.Delete(root, recursive: true);
        }
        Directory.Exists(root).ShouldBeFalse();
    }

    private static void Prepare(ShardingFixture fixture, string root, PostgresFaultProxy proxy, int probePort)
    {
        string scratch = Path.Combine(root, "scratch");
        string artifacts = Path.Combine(root, "artifacts");
        Directory.CreateDirectory(scratch);
        Directory.CreateDirectory(artifacts);
        File.WriteAllText(Path.Combine(scratch, "owned.txt"), "disposable");
        SetField(fixture, "_scratch", scratch);
        SetField(fixture, "_artifacts", artifacts);
        SetField(fixture, "_faultProxy", proxy);
        SetField(fixture, "_proxyPort", probePort);
    }

    private static void SetField(ShardingFixture fixture, string name, object value)
        => typeof(ShardingFixture).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture, value);

    private static JsonDocument ReadReceipt(string root)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "artifacts", "cleanup.json")));
}
