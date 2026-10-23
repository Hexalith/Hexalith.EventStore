using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.DomainService.Content;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.Content;

/// <summary>Opted-in real Redis/Dapr persistence qualification; it owns and replaces its exact sidecar process.</summary>
public sealed class ContentPersistenceQualificationTests
{
    /// <summary>Whether the operator explicitly enabled the live dependency lane.</summary>
    public static bool Enabled => Environment.GetEnvironmentVariable("EVENTSTORE_CONTENT_LIVE") == "1";

    [Fact(Skip = "Enable EVENTSTORE_CONTENT_LIVE=1 for the real Dapr/Redis qualification lane.", SkipUnless = nameof(Enabled))]
    public async Task PersistedBytesSurviveSidecarProcessReplacementAndRejectCorruption()
    {
        string directory = Path.Combine(Path.GetTempPath(), "eventstore-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string appId = "content-qualification-" + Guid.NewGuid().ToString("N");
        int httpPort = FreePort();
        int grpcPort = FreePort();
        string redisEndpoint = Environment.GetEnvironmentVariable("EVENTSTORE_CONTENT_REDIS_ENDPOINT") ?? "127.0.0.1:6379";
        await File.WriteAllTextAsync(Path.Combine(directory, "content.yaml"), $$"""
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: content-qualification
            spec:
              type: state.redis
              version: v1
              metadata:
                - name: redisHost
                  value: "{{redisEndpoint}}"
                - name: redisPassword
                  value: ""
                - name: keyPrefix
                  value: appid
            """);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant", "tenant-a")], "qualification"));
        var scope = new ContentScope("tenant-a", "folders", "folder-qualification", "workspace-qualification");
        var authority = new ContentTestAuthority();
        var options = Options.Create(new AuthoritativeContentOptions { StateStoreName = "content-qualification" });
        var clock = new ContentTestTime();
        byte[] bytes = Enumerable.Range(0, 8192).Select(static value => (byte)(value % 256)).ToArray();
        Process? sidecar = null;
        try
        {
            sidecar = StartSidecar(appId, directory, httpPort, grpcPort);
            await WaitReadyAsync(httpPort, sidecar);
            int firstPid = sidecar.Id;
            using var client = new DaprClientBuilder().UseGrpcEndpoint($"http://127.0.0.1:{grpcPort}").Build();
            var first = new DaprAuthoritativeContentStore(client, authority, options, clock);
            ContentReceipt receipt = await first.UploadAsync(principal, new(scope, "task-a", Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length, "application/octet-stream", "stable-live-upload"), new MemoryStream(bytes));
            (await first.InspectUploadAsync(principal, new(scope, "task-a", receipt.Id))).Bytes.ShouldBe(bytes);
            ContentPublicationRequest publication = new(scope, "live-command", "task-a", 1, 0,
                [new("file-a", "incarnation-a", 1, receipt.Id, new Dictionary<string, string> { ["path"] = "src/example.bin" }, receipt)]);
            await first.PrepareAsync(principal, publication);
            (await first.ListAsync(principal, scope)).ShouldBeEmpty();
            await StopAsync(sidecar);
            sidecar.Dispose();
            sidecar = StartSidecar(appId, directory, httpPort, grpcPort);
            await WaitReadyAsync(httpPort, sidecar);
            sidecar.Id.ShouldNotBe(firstPid);
            using var replacementClient = new DaprClientBuilder().UseGrpcEndpoint($"http://127.0.0.1:{grpcPort}").Build();
            var replacement = new DaprAuthoritativeContentStore(replacementClient, authority, options, clock);
            authority.Outcome = ContentPublicationOutcome.Accepted;
            ContentReadResult result = await replacement.ReadAsync(principal, new(scope, "file-a", "incarnation-a", 1, bytes.Length));
            result.Bytes.ShouldBe(bytes);
            string scopeHash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(scope)));
            string key = "authoritative-content:body:" + scopeHash + ":" + receipt.Id;
            ContentStoredBody persisted = await replacementClient.GetStateAsync<ContentStoredBody>(options.Value.StateStoreName, key, ConsistencyMode.Strong);
            persisted.Bytes.ShouldBe(bytes);
            ContentLedger metadata = await replacementClient.GetStateAsync<ContentLedger>(options.Value.StateStoreName, "authoritative-content:metadata:" + scopeHash, ConsistencyMode.Strong);
            metadata.Publications["live-command"].Outcome.ShouldBe(ContentPublicationOutcome.Accepted);
            JsonSerializer.Serialize(metadata).ShouldNotContain(Convert.ToBase64String(bytes));
            (_, string bodyEtag) = await replacementClient.GetStateAndETagAsync<ContentStoredBody>(options.Value.StateStoreName, key, ConsistencyMode.Strong);
            (await replacementClient.TrySaveStateAsync(options.Value.StateStoreName, key, persisted with { Bytes = [0] }, bodyEtag,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong })).ShouldBeTrue();
            await Should.ThrowAsync<ContentUnavailableException>(() => replacement.ReadAsync(principal, new(scope, "file-a", "incarnation-a", 1, bytes.Length)));
            await Should.ThrowAsync<ContentUnavailableException>(() => replacement.InspectUploadAsync(principal, new(scope, "task-a", receipt.Id)));
            (_, bodyEtag) = await replacementClient.GetStateAndETagAsync<ContentStoredBody>(options.Value.StateStoreName, key, ConsistencyMode.Strong);
            (await replacementClient.TrySaveStateAsync(options.Value.StateStoreName, key, persisted, bodyEtag,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong })).ShouldBeTrue();
            authority.Outcome = ContentPublicationOutcome.Pending;
            ContentPublicationRequest race = publication with { CommandId = "race-a", ExpectedVersion = 1, Changes = [publication.Changes[0] with { Version = 2 }] };
            async Task<bool> TryPrepareAsync(ContentPublicationRequest contender)
            {
                try { await replacement.PrepareAsync(principal, contender); return true; }
                catch (ContentConflictException) { return false; }
            }
            bool[] winners = await Task.WhenAll(TryPrepareAsync(race), TryPrepareAsync(race with { CommandId = "race-b" }));
            winners.Count(static winner => winner).ShouldBe(1);
            string winner = winners[0] ? "race-a" : "race-b";
            authority.Outcomes[winner] = ContentPublicationOutcome.Rejected;
            await replacement.RecoverAsync(principal, scope, winner);
            var deletion = publication with { CommandId = "delete", ExpectedVersion = 1, Changes = [new("file-a", "incarnation-a", 2, null)] };
            await replacement.PrepareAsync(principal, deletion);
            authority.Outcomes["delete"] = ContentPublicationOutcome.Accepted;
            await replacement.RecoverAsync(principal, scope, "delete");
            await Should.ThrowAsync<ContentUnavailableException>(() => replacement.ReadAsync(principal, new(scope, "file-a", "incarnation-a", 1, bytes.Length)));
            authority.Disposition = new(2, true, false, false, false);
            await replacement.ApplyDispositionAsync(principal, scope, receipt.Id);
            clock.Now = clock.Now.AddDays(8);
            await replacement.SetHoldAsync(principal, scope, receipt.Id, true);
            (await replacement.ApplyDispositionAsync(principal, scope, receipt.Id)).ShouldBeFalse();
            (await replacementClient.GetStateAsync<ContentStoredBody>(options.Value.StateStoreName, key, ConsistencyMode.Strong)).ShouldNotBeNull();
            await replacement.SetHoldAsync(principal, scope, receipt.Id, false);
            (await replacement.ApplyDispositionAsync(principal, scope, receipt.Id)).ShouldBeFalse();
            clock.Now = clock.Now.AddDays(7);
            (await replacement.ApplyDispositionAsync(principal, scope, receipt.Id)).ShouldBeTrue();
            ContentStoredBody erased = await replacementClient.GetStateAsync<ContentStoredBody>(options.Value.StateStoreName, key, ConsistencyMode.Strong);
            erased.Erased.ShouldBeTrue(); erased.Bytes.ShouldBeEmpty();
            bool resurrected;
            try { resurrected = await replacementClient.TrySaveStateAsync(options.Value.StateStoreName, key, persisted, string.Empty,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong }); }
            catch (Dapr.DaprException) { resurrected = false; }
            resurrected.ShouldBeFalse();
            metadata = await replacementClient.GetStateAsync<ContentLedger>(options.Value.StateStoreName, "authoritative-content:metadata:" + scopeHash, ConsistencyMode.Strong);
            metadata.Uploads[receipt.Id] = metadata.Uploads[receipt.Id] with { Erased = false, Erasing = true };
            (_, string ledgerEtag) = await replacementClient.GetStateAndETagAsync<ContentLedger>(options.Value.StateStoreName, "authoritative-content:metadata:" + scopeHash, ConsistencyMode.Strong);
            (await replacementClient.TrySaveStateAsync(options.Value.StateStoreName, "authoritative-content:metadata:" + scopeHash, metadata, ledgerEtag,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong })).ShouldBeTrue();
            using var recoveredClient = new DaprClientBuilder().UseGrpcEndpoint($"http://127.0.0.1:{grpcPort}").Build();
            (await new DaprAuthoritativeContentStore(recoveredClient, authority, options, clock).ApplyDispositionAsync(principal, scope, receipt.Id)).ShouldBeTrue();
            erased = await recoveredClient.GetStateAsync<ContentStoredBody>(options.Value.StateStoreName, key, ConsistencyMode.Strong);
            erased.Erased.ShouldBeTrue(); erased.Bytes.ShouldBeEmpty();
            ContentReceipt empty = await replacement.UploadAsync(principal, new(scope, "task-a", Convert.ToHexStringLower(SHA256.HashData([])), 0, "text/plain"), new MemoryStream());
            (await replacement.InspectUploadAsync(principal, new(scope, "task-a", empty.Id))).Bytes.ShouldBeEmpty();
            await replacement.ApplyDispositionAsync(principal, scope, empty.Id); clock.Now = clock.Now.AddDays(7);
            (await replacement.ApplyDispositionAsync(principal, scope, empty.Id)).ShouldBeTrue();
            Console.WriteLine($"Authoritative content qualification: first PID {firstPid}, replacement PID {sidecar.Id}, verified length {bytes.Length}, receipt {receipt.Id}, SHA256 {receipt.Sha256}; corruption rejected; one CAS race winner; tombstone and hold/P7D erasure verified.");
        }
        finally
        {
            if (sidecar is not null)
            {
                await StopAsync(sidecar);
                sidecar.Dispose();
            }
            Directory.Delete(directory, true);
        }
    }

    private static Process StartSidecar(string appId, string directory, int httpPort, int grpcPort)
    {
        var start = new ProcessStartInfo("daprd") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "--app-id", appId, "--resources-path", directory, "--dapr-http-port", httpPort.ToString(), "--dapr-grpc-port", grpcPort.ToString(), "--dapr-internal-grpc-port", FreePort().ToString(), "--metrics-port", FreePort().ToString(), "--max-body-size", "16Mi", "--log-level", "error" })
            start.ArgumentList.Add(argument);
        // daprd is installed by dapr init in the standard per-user runtime location.
        string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dapr", "bin", "daprd");
        if (File.Exists(installed)) start.FileName = installed;
        Process process = Process.Start(start) ?? throw new InvalidOperationException("Content sidecar did not start.");
        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += static (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static async Task WaitReadyAsync(int port, Process process)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (process.HasExited) throw new InvalidOperationException("Content sidecar exited before readiness.");
            try
            {
                if ((await http.GetAsync($"http://127.0.0.1:{port}/v1.0/healthz/outbound")).IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(100);
        }
        throw new TimeoutException("Content sidecar did not become ready.");
    }

    private static async Task StopAsync(Process process)
    {
        if (process.HasExited) return;
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
