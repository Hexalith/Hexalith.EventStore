using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Exercises real generated program files, an actual HTTP host and its measured owned process lifetime.</summary>
public sealed class QualificationManagedLaunchTests
{
    [Fact]
    public async Task ActualGeneratedHostIdentityAndLifetimeAreBoundBeforeExecution()
    {
        await using var runtime = new QualificationRuntime();
        string root = Directory.CreateDirectory(Path.Combine(runtime.Root, "consumer")).FullName;
        string project = Path.Combine(root, "Host.csproj"), output = Path.Combine(root, "output");
        await File.WriteAllTextAsync(project, "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, "Program.cs"), "var app = WebApplication.CreateBuilder(args).Build(); app.MapGet(\"/value\", () => Results.Text(\"owned-value\")); app.Run();", TestContext.Current.CancellationToken);
        string config = Path.Combine(root, "NuGet.Config");
        await File.WriteAllTextAsync(config, "<configuration><packageSources><clear/></packageSources></configuration>", TestContext.Current.CancellationToken);
        await runtime.RequireAsync("dotnet", ["build", project, "--output", output, "--nologo", "-m:1", "-nr:false", "-p:RestoreConfigFile=" + config], root, TimeSpan.FromMinutes(2));
        string assembly = Path.Combine(output, "Host.dll");
        var build = new ConsumerBuild("Host", "1.0.0", "Debug", project, assembly, Path.Combine(root, "packages"), new string('a', 64), new string('b', 64), null,
            PreparedExecutableHashes: ConsumerBuild.ReadExecutableHashes("Host", assembly));
        using var port = new TcpListener(IPAddress.Loopback, 0); port.Start(); int number = ((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
        string address = "http://127.0.0.1:" + number, profile = Guid.NewGuid().ToString("N");
        var owned = runtime.Start(new("dotnet", [assembly], root, TimeSpan.FromSeconds(30), new Dictionary<string, string> { ["ASPNETCORE_URLS"] = address }), build, profile);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        using var client = new HttpClient(new QualificationHttpTransportObserver(runtime));
        string? value = null;
        while (value is null)
        {
            try { value = await client.GetStringAsync(address + "/value", deadline.Token); }
            catch (HttpRequestException) { await Task.Delay(25, deadline.Token); }
        }
        value.ShouldBe("owned-value");
        var live = runtime.ManagedLaunches.ShouldHaveSingleItem(); live.Pid.ShouldBe(owned.Process.Id); live.Address.ShouldBe(address); live.Profile.ShouldBe(profile); live.Ended.ShouldBeNull();
        await QualificationRuntime.StopAsync(owned, TestContext.Current.CancellationToken);
        var stopped = runtime.ManagedLaunches.ShouldHaveSingleItem(); stopped.Ended.ShouldNotBeNull(); stopped.Ended!.Value.ShouldBeGreaterThanOrEqualTo(stopped.Started);
        var exchange = runtime.HttpExchanges.Single(exchange => exchange.Method == "GET" && exchange.Address == address + "/value");
        exchange.Started.ShouldBeGreaterThanOrEqualTo(stopped.Started); exchange.Ended.ShouldBeLessThanOrEqualTo(stopped.Ended.Value);
        exchange.ResponseSha256.ShouldBe(QualificationRuntime.Hash(Encoding.UTF8.GetBytes("owned-value")));
        JsonSerializer.Serialize(stopped.ExecutableHashes).ShouldBe(JsonSerializer.Serialize(build.PreparedExecutableHashes));
        foreach (string file in build.PreparedExecutableHashes!.Keys)
        {
            string path = Path.Combine(output, file); byte[] original = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            try
            {
                if (file.EndsWith(".deps.json", StringComparison.Ordinal))
                {
                    File.Delete(path);
                    Should.Throw<FileNotFoundException>(() => runtime.Start(new("dotnet", [assembly], root, TimeSpan.FromSeconds(5)), build));
                }
                else
                {
                    await File.AppendAllTextAsync(path, "modified-generated-output", TestContext.Current.CancellationToken);
                    Should.Throw<InvalidOperationException>(() => runtime.Start(new("dotnet", [assembly], root, TimeSpan.FromSeconds(5)), build));
                }
                runtime.ManagedLaunches.Count.ShouldBe(1);
            }
            finally { await File.WriteAllBytesAsync(path, original, CancellationToken.None); }
        }
        build.VerifyExecutableHashes().Count.ShouldBe(3);
    }
}
