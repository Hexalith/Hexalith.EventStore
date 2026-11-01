using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Exercises the real producer SAVE guard with actual Redis CLI success and exit-zero error replies.</summary>
public sealed class QualificationRedisSaveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealRedisMustAcknowledgeSaveBeforeAnyBackupCopy(bool deny)
    {
        await using var runtime = new QualificationRuntime();
        await runtime.CaptureSharedContainersAsync();
        string name = runtime.ReserveContainer("save-response");
        string[] extra = deny ? ["--rename-command", "SAVE", "DISABLED_SAVE"] : [];
        await runtime.RequireAsync("docker", ["run", "--detach", "--name", name, "--label", "p1r.owner=" + runtime.Id,
            "--tmpfs", "/data", PublishedCluster.RedisImage, "redis-server", "--save", "", "--appendonly", "no", .. extra]);
        using var ready = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        ready.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            var ping = await runtime.RunAsync(new("docker", ["exec", name, "redis-cli", "PING"], runtime.Root, TimeSpan.FromSeconds(2)), ready.Token);
            if (ping.ExitCode == 0 && ping.StandardOutput.Trim() == "PONG") break;
            await Task.Delay(50, ready.Token);
        }
        if (deny) (await Should.ThrowAsync<InvalidOperationException>(() => runtime.RequireRedisSaveAsync(name))).Message.ShouldContain("did not acknowledge");
        else await runtime.RequireRedisSaveAsync(name);
        var save = runtime.Commands.Single(command => command.Command.Arguments.SequenceEqual(new[] { "exec", name, "redis-cli", "SAVE" }));
        save.ExitCode.ShouldBe(0);
        if (deny) save.StandardOutput.ShouldStartWith("ERR"); else save.StandardOutput.Trim().ShouldBe("OK");
        runtime.Commands.ShouldNotContain(command => command.Command.Arguments.FirstOrDefault() == "cp");
        await runtime.CleanupAsync(); runtime.CleanupFailures.ShouldBeEmpty();
    }
}
