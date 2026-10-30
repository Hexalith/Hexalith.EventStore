using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.Testing.Tests;

public sealed class TestEnvironmentDescriptorReaderTests
{
    [Fact]
    public void ReadyOwner_ResolvesExactBindingsAndSyntheticTenant()
    {
        using var file = new DescriptorTestFile();
        using TestEnvironmentDescriptor environment = TestEnvironmentDescriptorReader.ReadDescriptor(file.Path);
        environment.SyntheticTenantId.ShouldBe("sample-synthetic-tenant");
        environment.GetEndpoint("eventstore", "Hexalith.EventStore", "http").ShouldBe(new Uri("http://127.0.0.1:18080"));
        Should.Throw<TestEnvironmentDescriptorException>(() => environment.GetEndpoint("eventstore", "wrong-owner", "http")).Rule.ShouldBe("HXT109");
        Should.Throw<TestEnvironmentDescriptorException>(() => environment.GetEndpoint("eventstore", "Hexalith.EventStore", "HTTP")).Rule.ShouldBe("HXT109");
        Should.Throw<TestEnvironmentDescriptorException>(() => environment.CreateHttpClient("eventstore", "Hexalith.EventStore", "http", TimeSpan.FromSeconds(1))).Rule.ShouldBe("HXT110");
    }

    [Theory]
    [InlineData("version", "HXT101")]
    [InlineData("policy", "HXT102")]
    [InlineData("unknown", "HXT104")]
    [InlineData("identity", "HXT104")]
    [InlineData("attempt", "HXT104")]
    [InlineData("resource-duplicate", "HXT104")]
    [InlineData("endpoint-duplicate", "HXT104")]
    [InlineData("bad-uri", "HXT104")]
    [InlineData("diagnostic", "HXT104")]
    [InlineData("timing", "HXT107")]
    [InlineData("tick", "HXT107")]
    [InlineData("hold-effective", "HXT107")]
    [InlineData("hold-expiry", "HXT107")]
    [InlineData("hold-end", "HXT107")]
    [InlineData("hold-owner", "HXT104")]
    [InlineData("isolation", "HXT108")]
    [InlineData("resources", "HXT108")]
    [InlineData("readiness", "HXT108")]
    [InlineData("resource-unready", "HXT108")]
    [InlineData("resource-readiness-missing", "HXT108")]
    [InlineData("startup-unavailable", "HXT108")]
    [InlineData("startup-failed", "HXT108")]
    public void InconsistentHandoff_RefusesAtomicallyWithoutSuppliedValues(string mutation, string rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutation);
        using var file = new DescriptorTestFile();
        JsonNode root = JsonNode.Parse(File.ReadAllText(file.Path))!;
        JsonNode bindings = root["bindings"]!;
        JsonNode env = bindings["environment"]!;
        if (mutation.StartsWith("hold-", StringComparison.Ordinal))
        {
            bindings["attachments"]!["holds"]!.AsArray().Add(JsonNode.Parse("""
                {"originalAttachmentRequestId":"request-1","environmentId":"sample-environment","ownerRunId":"sample-owner-run","attachedRunId":"attached-run","acceptedAt":"2026-10-22T12:00:00Z","requestedHoldSeconds":300,"effectiveHoldSeconds":300,"expiresAt":"2026-10-22T12:05:00Z","end":{"state":"active"}}
                """));
        }
        switch (mutation)
        {
            case "version": root["schema"] = "secret-value"; break;
            case "policy": root["policyId"] = "secret-value"; break;
            case "unknown": root["secret-value"] = "secret-value"; break;
            case "identity": bindings["run"]!["ownerRunId"] = "secret-value"; break;
            case "attempt": bindings["run"]!["attemptId"] = bindings["run"]!["runId"]!.GetValue<string>(); break;
            case "resource-duplicate": env["resources"]!.AsArray().Add(env["resources"]![0]!.DeepClone()); break;
            case "endpoint-duplicate": env["resources"]![0]!["endpoints"]!.AsArray().Add(env["resources"]![0]!["endpoints"]![0]!.DeepClone()); break;
            case "bad-uri": env["resources"]![0]!["endpoints"]![0]!["uri"] = "http://localhost:999999"; break;
            case "diagnostic": env["readiness"]!["resources"]![0]!["diagnosticIds"]!.AsArray().Add("secret-value"); break;
            case "timing": bindings["deadlines"]!["startup"]!["expiresAt"] = "2026-10-22T12:10:01Z"; break;
            case "tick": bindings["deadlines"]!["startup"]!["effectiveSeconds"] = 0.00000001; break;
            case "hold-effective": bindings["attachments"]!["holds"]![0]!["effectiveHoldSeconds"] = 301; break;
            case "hold-expiry": bindings["attachments"]!["holds"]![0]!["expiresAt"] = "2026-10-22T12:05:01Z"; break;
            case "hold-end": bindings["attachments"]!["holds"]![0]!["end"] = JsonNode.Parse("{\"state\":\"ended\",\"endedAt\":\"2026-10-22T12:05:00Z\",\"reason\":\"terminal-outcome\"}"); break;
            case "hold-owner": bindings["attachments"]!["holds"]![0]!["ownerRunId"] = "secret-value"; break;
            case "isolation": env["isolation"] = JsonNode.Parse("{\"state\":\"not-yet-populated\"}"); break;
            case "resources": env["resources"] = new JsonArray(); env["readiness"]!["resources"] = new JsonArray(); break;
            case "readiness": env["readiness"]!["status"] = "pending"; break;
            case "resource-unready": env["readiness"]!["resources"]![0]!["status"] = "failed"; break;
            case "resource-readiness-missing": env["readiness"]!["resources"] = new JsonArray(); break;
            case "startup-unavailable": env["startupTasks"] = JsonNode.Parse("{\"state\":\"not-yet-populated\"}"); break;
            case "startup-failed": env["startupTasks"]!["tasks"]!.AsArray().Add(JsonNode.Parse("{\"taskId\":\"setup\",\"required\":true,\"status\":\"failed\",\"diagnosticIds\":[]}")); break;
        }

        File.WriteAllText(file.Path, root.ToJsonString());
        TestEnvironmentDescriptorException exception = Should.Throw<TestEnvironmentDescriptorException>(() => TestEnvironmentDescriptorReader.ReadDescriptor(file.Path));
        exception.Rule.ShouldBe(rule);
        exception.Message.ShouldNotContain("secret-value");
        exception.Message.ShouldNotContain(file.Path);
    }

    [Theory]
    [InlineData("malformed", "HXT103")]
    [InlineData("duplicate", "HXT103")]
    [InlineData("utf8", "HXT103")]
    [InlineData("depth", "HXT103")]
    [InlineData("integer", "HXT104")]
    [InlineData("oversized", "HXT105")]
    public void InvalidFile_RefusesWithoutLeakingContents(string mutation, string rule)
    {
        using var file = new DescriptorTestFile();
        string content = File.ReadAllText(file.Path);
        byte[] bytes = mutation switch
        {
            "malformed" => Encoding.UTF8.GetBytes("{\"secret-value\":"),
            "duplicate" => Encoding.UTF8.GetBytes(content.Replace("\"diagnostics\": []", "\"diagnostics\": [],\"diagnostics\": []", StringComparison.Ordinal)),
            "utf8" => [0xff, 0xfe],
            "depth" => Encoding.UTF8.GetBytes(new string('[', 65) + new string(']', 65)),
            "integer" => Encoding.UTF8.GetBytes(content.Replace("\"attemptSequence\": 1", "\"attemptSequence\": 1.0", StringComparison.Ordinal)),
            _ => new byte[TestEnvironmentDescriptorReader.MaximumDocumentBytes + 1],
        };
        File.WriteAllBytes(file.Path, bytes);
        Should.Throw<TestEnvironmentDescriptorException>(() => TestEnvironmentDescriptorReader.ReadDescriptor(file.Path)).Rule.ShouldBe(rule);
    }

    [Fact]
    public void MissingRelativeLinkedAndLockedFiles_Refuse()
    {
        Should.Throw<TestEnvironmentDescriptorException>(() => TestEnvironmentDescriptorReader.ReadDescriptor("relative.json")).Rule.ShouldBe("HXT106");
        Should.Throw<TestEnvironmentDescriptorException>(() => TestEnvironmentDescriptorReader.ReadDescriptor(System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N")))).Rule.ShouldBe("HXT106");
        using var file = new DescriptorTestFile();
        using (FileStream held = new(file.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Should.Throw<TestEnvironmentDescriptorException>(() => TestEnvironmentDescriptorReader.ReadDescriptor(file.Path)).Rule.ShouldBe("HXT106");
        }
        if (!OperatingSystem.IsWindows())
        {
            string link = file.Path + ".link";
            try
            {
                File.CreateSymbolicLink(link, file.Path);
                Should.Throw<TestEnvironmentDescriptorException>(() => TestEnvironmentDescriptorReader.ReadDescriptor(link)).Rule.ShouldBe("HXT106");
            }
            finally { File.Delete(link); }
        }
    }

    [Fact]
    public async Task Disposal_CancelsOutstandingRequestsAndReleasesClients()
    {
        using var file = new DescriptorTestFile();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        string content = File.ReadAllText(file.Path).Replace("18080", ((IPEndPoint)listener.LocalEndpoint).Port.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        File.WriteAllText(file.Path, content);
        using TestEnvironmentDescriptor environment = TestEnvironmentDescriptorReader.ReadDescriptor(file.Path);
        using HttpClient client = environment.CreateHttpClient("eventstore", "Hexalith.EventStore", "http", TimeSpan.FromSeconds(10), authenticated: false);
        Task<HttpResponseMessage> request = client.GetAsync("/", TestContext.Current.CancellationToken);
        using TcpClient accepted = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
        environment.Dispose();
        await Should.ThrowAsync<OperationCanceledException>(async () => await request);
        Should.Throw<ObjectDisposedException>(() => environment.GetEndpoint("eventstore", "Hexalith.EventStore", "http"));
        environment.Dispose();
    }

    [Fact]
    public async Task Disposal_CancelsResponseBodyReadAfterHeadersWithoutCallerToken()
    {
        using var file = new DescriptorTestFile();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        File.WriteAllText(file.Path, File.ReadAllText(file.Path).Replace("18080", port.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
        using TestEnvironmentDescriptor environment = TestEnvironmentDescriptorReader.ReadDescriptor(file.Path);
        using HttpClient client = environment.CreateHttpClient("eventstore", "Hexalith.EventStore", "http", TimeSpan.FromSeconds(30), authenticated: false);
        Task<HttpResponseMessage> request = client.GetAsync("/", HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        using TcpClient accepted = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
        await accepted.GetStream().WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 4096\r\nConnection: close\r\n\r\nx"), TestContext.Current.CancellationToken);
        using HttpResponseMessage response = await request.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using Stream body = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        byte[] buffer = new byte[1];
        (await body.ReadAsync(buffer, TestContext.Current.CancellationToken)).ShouldBe(1);
        Task<int> pending = body.ReadAsync(buffer).AsTask();
        pending.IsCompleted.ShouldBeFalse();
        environment.Dispose();
        Exception? failure = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        (failure is IOException or ObjectDisposedException or OperationCanceledException).ShouldBeTrue(
            "Fixture disposal must end an outstanding streamed body read without a caller-supplied token.");
    }

    [Fact]
    public async Task AbsoluteRequestOverride_RefusesBeforeSendingCredentialOrRequest()
    {
        using var file = new DescriptorTestFile();
        using TestEnvironmentDescriptor environment = TestEnvironmentDescriptorReader.ReadDescriptor(file.Path);
        environment.BindCredentials(environment.RunId, environment.SyntheticTenantId, environment.DataIsolationId, "private-runner-token");
        using HttpClient client = environment.CreateHttpClient("eventstore", "Hexalith.EventStore", "http", TimeSpan.FromSeconds(2));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        TestEnvironmentDescriptorException failure = await Should.ThrowAsync<TestEnvironmentDescriptorException>(async () =>
            await client.GetAsync(new Uri($"http://127.0.0.1:{port}/outside"), TestContext.Current.CancellationToken));
        failure.Rule.ShouldBe("HXT109");
        listener.Pending().ShouldBeFalse("No credential-bearing request may reach an undeclared origin.");
    }

    [Fact]
    public void DisposalFailure_IsReportedWithoutLosingClientCleanupOrExposingDetails()
    {
        using var file = new DescriptorTestFile();
        using TestEnvironmentDescriptor environment = TestEnvironmentDescriptorReader.ReadDescriptor(file.Path);
        using HttpClient client = environment.CreateHttpClient("eventstore", "Hexalith.EventStore", "http", TimeSpan.FromSeconds(1), false);
        using CancellationTokenRegistration failure = environment.RequestCancellation.Register(() => throw new InvalidOperationException("private-failure-details"));
        AggregateException exception = Should.Throw<AggregateException>(environment.Dispose);
        exception.ToString().ShouldNotContain("private-failure-details");
        Should.Throw<ObjectDisposedException>(() => environment.GetEndpoint("eventstore", "Hexalith.EventStore", "http"));
        environment.Dispose();
    }

    [Fact]
    public void Cancellation_PropagatesAndCredentialIdentityMustMatch()
    {
        using var file = new DescriptorTestFile();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Should.Throw<OperationCanceledException>(() => TestEnvironmentDescriptorReader.ReadDescriptor(file.Path, cancelled.Token));
        using TestEnvironmentDescriptor environment = TestEnvironmentDescriptorReader.ReadDescriptor(file.Path);
        Should.Throw<TestEnvironmentDescriptorException>(() => environment.BindCredentials("wrong", environment.SyntheticTenantId, environment.DataIsolationId, "private-token")).Rule.ShouldBe("HXT110");
    }
}
