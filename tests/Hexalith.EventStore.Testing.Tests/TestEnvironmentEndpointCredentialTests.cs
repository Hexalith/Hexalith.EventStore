namespace Hexalith.EventStore.Testing.Tests;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

using Shouldly;

/// <summary>Exercises explicit private endpoint bindings and actual confined HTTP sends.</summary>
public sealed class TestEnvironmentEndpointCredentialTests
{
    /// <summary>Network namespaces are rejected in both private path inputs before filesystem inspection.</summary>
    /// <param name="path">The network path spelling.</param>
    [Theory]
    [InlineData(@"\\server\share\native-endpoint-credentials.json")]
    [InlineData(@"\\?\UNC\server\share\native-endpoint-credentials.json")]
    [InlineData("//server/share/native-endpoint-credentials.json")]
    public void NetworkCredentialPathsRefuseWithoutValues(string path)
    {
        using var files = new EndpointCredentialTestEnvironment();
        TestEnvironmentDescriptorException credential = Should.Throw<TestEnvironmentDescriptorException>(() =>
            TestEnvironmentEndpointCredentials.Read(path, files.DescriptorPath, files.Environment));
        credential.Rule.ShouldBe("HXT110");
        credential.Message.ShouldNotContain(path);
        TestEnvironmentDescriptorException descriptor = Should.Throw<TestEnvironmentDescriptorException>(() =>
            TestEnvironmentEndpointCredentials.Read(files.CredentialPath, path, files.Environment));
        descriptor.Rule.ShouldBe("HXT110");
        descriptor.Message.ShouldNotContain(path);
    }

    /// <summary>The private token cannot turn a non-HTTP declared transport into an HTTP client.</summary>
    [Fact]
    public void InvocationHttpUriWithTcpProtocolRefuses()
    {
        using var files = new EndpointCredentialTestEnvironment();
        files.Bind();
        JsonNode root = JsonNode.Parse(File.ReadAllText(files.DescriptorPath))!;
        root["bindings"]!["environment"]!["resources"]![0]!["endpoints"]![1]!["protocol"] = "tcp";
        File.WriteAllText(files.DescriptorPath, root.ToJsonString());
        using TestEnvironmentDescriptor descriptor = TestEnvironmentDescriptorReader.ReadDescriptor(files.DescriptorPath);
        descriptor.BindCredentials(descriptor.RunId, descriptor.SyntheticTenantId, descriptor.DataIsolationId, EndpointCredentialTestEnvironment.BearerToken);
        descriptor.BindEndpointCredentials(files.CredentialPath, files.DescriptorPath);
        Should.Throw<TestEnvironmentDescriptorException>(() => descriptor.CreateDaprInvocationClient("eventstore", "Hexalith.EventStore", "dapr-invocation", TimeSpan.FromSeconds(1))).Rule.ShouldBe("HXT110");
    }

    /// <summary>Missing private endpoint credentials never fall back to the bearer-only client.</summary>
    [Fact]
    public void MissingExplicitCredentialRefusesDaprClient()
    {
        using var files = new EndpointCredentialTestEnvironment();
        Should.Throw<TestEnvironmentDescriptorException>(() => files.Environment.CreateDaprInvocationClient(
            "eventstore", "Hexalith.EventStore", "dapr-invocation", TimeSpan.FromSeconds(1))).Rule.ShouldBe("HXT110");
    }

    /// <summary>The public descriptor and object representation never carry the private token.</summary>
    [Fact]
    public void CredentialStaysSeparateFromDescriptor()
    {
        using var files = new EndpointCredentialTestEnvironment();
        files.Bind();
        File.ReadAllText(files.DescriptorPath).ShouldNotContain(EndpointCredentialTestEnvironment.ApiToken);
        files.Environment.ToString()!.ShouldNotContain(EndpointCredentialTestEnvironment.ApiToken);
        files.Environment.GetDaprInvocationEndpoint("eventstore", "Hexalith.EventStore", "dapr-invocation").AbsolutePath
            .ShouldBe("/v1.0/invoke/declared-owner/method/");
    }

    /// <summary>Every malformed or mismatched private handoff is an exact metadata-only HXT110 refusal.</summary>
    [Theory]
    [InlineData("run")]
    [InlineData("tenant")]
    [InlineData("isolation")]
    [InlineData("resource")]
    [InlineData("module")]
    [InlineData("endpoint")]
    [InlineData("uri")]
    [InlineData("header")]
    [InlineData("missing-value")]
    [InlineData("crlf")]
    [InlineData("duplicate-binding")]
    [InlineData("duplicate-key")]
    [InlineData("unknown-root")]
    [InlineData("unknown-entry")]
    [InlineData("malformed")]
    [InlineData("utf8")]
    [InlineData("oversized")]
    [InlineData("private-mode")]
    [InlineData("wrong-path")]
    [InlineData("missing-file")]
    public void InvalidCredentialRefusesAtomicallyWithoutValues(string mutation)
    {
        using var files = new EndpointCredentialTestEnvironment();
        JsonObject root = files.Credentials;
        JsonNode entry = root["endpoints"]![0]!;
        switch (mutation)
        {
            case "run": root["runId"] = "other-run"; break;
            case "tenant": root["syntheticTenantId"] = "other-tenant"; break;
            case "isolation": root["dataIsolationId"] = "other-isolation"; break;
            case "resource": entry["resourceId"] = "other-resource"; break;
            case "module": entry["moduleId"] = "other-module"; break;
            case "endpoint": entry["endpointName"] = "other-endpoint"; break;
            case "uri": entry["uri"] = "http://127.0.0.1:18080/v1.0/invoke/other/method/"; break;
            case "header": entry["headerName"] = "Authorization"; break;
            case "missing-value": entry.AsObject().Remove("value"); break;
            case "crlf": entry["value"] = "private\r\nheader"; break;
            case "duplicate-binding": root["endpoints"]!.AsArray().Add(entry.DeepClone()); break;
            case "unknown-root": root["unknown"] = "private-value"; break;
            case "unknown-entry": entry["unknown"] = "private-value"; break;
        }
        files.WriteCredentials();
        string path = files.CredentialPath;
        switch (mutation)
        {
            case "duplicate-key": File.WriteAllText(path, File.ReadAllText(path).Replace("\"schema\":", "\"schema\":\"duplicate\",\"schema\":", StringComparison.Ordinal)); break;
            case "malformed": File.WriteAllText(path, "{\"private-value\":"); break;
            case "utf8": File.WriteAllBytes(path, [0xff, 0xfe]); break;
            case "oversized": File.WriteAllBytes(path, new byte[65537]); break;
            case "private-mode":
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
                else { File.Delete(path); Directory.CreateDirectory(path); }
                break;
            case "wrong-path": path += ".other"; File.Copy(files.CredentialPath, path); break;
            case "missing-file": File.Delete(path); break;
        }
        TestEnvironmentDescriptorException failure = Should.Throw<TestEnvironmentDescriptorException>(() => files.Environment.BindEndpointCredentials(path, files.DescriptorPath));
        failure.Rule.ShouldBe("HXT110");
        failure.ToString().ShouldNotContain(EndpointCredentialTestEnvironment.ApiToken);
        failure.ToString().ShouldNotContain(path);
        Should.Throw<ObjectDisposedException>(() => files.Environment.GetEndpoint("eventstore", "Hexalith.EventStore", "http"));
    }

    /// <summary>A linked credential file is refused through its held nofollow handle.</summary>
    [Fact]
    public void LinkedCredentialFileIsRefused()
    {
        if (OperatingSystem.IsWindows()) return;
        using var files = new EndpointCredentialTestEnvironment();
        files.WriteCredentials();
        string target = files.CredentialPath + ".target";
        File.Move(files.CredentialPath, target);
        File.CreateSymbolicLink(files.CredentialPath, target);
        Should.Throw<TestEnvironmentDescriptorException>(() => files.Environment.BindEndpointCredentials(files.CredentialPath, files.DescriptorPath)).Rule.ShouldBe("HXT110");
    }

    /// <summary>Normalized and encoded prefix escapes cannot reach a socket with the private credential.</summary>
    [Theory]
    [InlineData("/api/v1/tenants")]
    [InlineData("../outside")]
    [InlineData("../../../../v1.0/state/store/key")]
    [InlineData("/v1.0/metadata")]
    [InlineData("api/%2foutside")]
    [InlineData("api/%5coutside")]
    [InlineData("%2e%2e/%2e%2e/state")]
    [InlineData("api/%252foutside")]
    [InlineData("api/%252e%252e/outside")]
    [InlineData("conflicting-header")]
    [InlineData("host-override")]
    [InlineData("routing-header")]
    [InlineData("basic-auth")]
    [InlineData("missing-bearer")]
    [InlineData("duplicate-authorization")]
    public async Task InvocationEscapeRefusesBeforeSending(string path)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var files = new EndpointCredentialTestEnvironment($"http://127.0.0.1:{port}/v1.0/invoke/declared-owner/method/");
        files.Bind();
        using HttpClient client = files.Environment.CreateDaprInvocationClient("eventstore", "Hexalith.EventStore", "dapr-invocation", TimeSpan.FromSeconds(2));
        using var request = new HttpRequestMessage(HttpMethod.Get, path is "conflicting-header" or "host-override" or "routing-header" or "basic-auth" or "missing-bearer" or "duplicate-authorization" ? "api/v1/tenants" : path);
        if (path == "conflicting-header") request.Headers.Add("dapr-api-token", "other-credential");
        if (path == "host-override") request.Headers.Host = "other.example";
        if (path == "routing-header") request.Headers.Add("dapr-app-id", "other-app");
        if (path == "basic-auth") request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", "b3RoZXI6YXBw");
        if (path == "missing-bearer") client.DefaultRequestHeaders.Authorization = null;
        if (path == "duplicate-authorization") request.Headers.TryAddWithoutValidation("Authorization", ["Bearer one", "Bearer two"]);
        TestEnvironmentDescriptorException failure = await Should.ThrowAsync<TestEnvironmentDescriptorException>(async () =>
            await client.SendAsync(request, TestContext.Current.CancellationToken));
        failure.Rule.ShouldBe("HXT109");
        failure.ToString().ShouldNotContain(EndpointCredentialTestEnvironment.ApiToken);
        request.ToString().ShouldNotContain(EndpointCredentialTestEnvironment.ApiToken);
        listener.Pending().ShouldBeFalse();
    }

    /// <summary>Content headers cannot override the supplied Dapr target or credential before any socket is opened.</summary>
    /// <param name="header">The conflicting Dapr header admitted by HttpContentHeaders.</param>
    [Theory]
    [InlineData("dapr-app-id")]
    [InlineData("dapr-api-token")]
    public async Task InvocationContentHeadersRefuseBeforeSending(string header)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var files = new EndpointCredentialTestEnvironment($"http://127.0.0.1:{port}/v1.0/invoke/declared-owner/method/");
        files.Bind();
        using HttpClient client = files.Environment.CreateDaprInvocationClient("eventstore", "Hexalith.EventStore", "dapr-invocation", TimeSpan.FromSeconds(1));
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/tenants")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { }),
        };
        request.Content.Headers.TryAddWithoutValidation(header, "conflicting-value").ShouldBeTrue();
        TestEnvironmentDescriptorException failure = await Should.ThrowAsync<TestEnvironmentDescriptorException>(async () =>
            await client.SendAsync(request, TestContext.Current.CancellationToken));
        failure.Rule.ShouldBe("HXT109");
        failure.ToString().ShouldNotContain(EndpointCredentialTestEnvironment.ApiToken);
        request.ToString().ShouldNotContain(EndpointCredentialTestEnvironment.ApiToken);
        listener.Pending().ShouldBeFalse();
    }

    /// <summary>Relative sends preserve the exact invocation prefix and carry the two distinct supplied credentials.</summary>
    [Theory]
    [InlineData("", "api/v1/tenants")]
    [InlineData("api/v1/", "tenants")]
    public async Task RelativeInvocationSendsOnlyToSuppliedPrefix(string prefix, string relative)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var files = new EndpointCredentialTestEnvironment($"http://127.0.0.1:{port}/v1.0/invoke/declared-owner/method/{prefix}");
        files.Bind();
        using HttpClient client = files.Environment.CreateDaprInvocationClient("eventstore", "Hexalith.EventStore", "dapr-invocation", TimeSpan.FromSeconds(5));
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(10));
        Task<HttpResponseMessage> pending = client.GetAsync(relative, bound.Token);
        using TcpClient accepted = await listener.AcceptTcpClientAsync(bound.Token);
        string headers = await ReadHeadersAsync(accepted.GetStream(), bound.Token);
        headers.ShouldContain("GET /v1.0/invoke/declared-owner/method/" + prefix + relative + " HTTP/1.1");
        headers.ShouldContain("dapr-api-token: " + EndpointCredentialTestEnvironment.ApiToken);
        headers.ShouldContain("Authorization: Bearer " + EndpointCredentialTestEnvironment.BearerToken);
        await accepted.GetStream().WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}"), TestContext.Current.CancellationToken);
        using HttpResponseMessage response = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>The declared public REST prefix cannot be replaced by a framework callback on the same callee.</summary>
    [Fact]
    public async Task PublicInvocationPrefixRefusesCalleeFrameworkCallback()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var files = new EndpointCredentialTestEnvironment($"http://127.0.0.1:{port}/v1.0/invoke/declared-owner/method/api/v1/");
        files.Bind();
        using HttpClient client = files.Environment.CreateDaprInvocationClient("eventstore", "Hexalith.EventStore", "dapr-invocation", TimeSpan.FromSeconds(2));
        TestEnvironmentDescriptorException failure = await Should.ThrowAsync<TestEnvironmentDescriptorException>(async () =>
            await client.GetAsync($"http://127.0.0.1:{port}/v1.0/invoke/declared-owner/method/actors/Any/id/method/Foo", TestContext.Current.CancellationToken));
        failure.Rule.ShouldBe("HXT109");
        listener.Pending().ShouldBeFalse();
    }

    /// <summary>Private invocation clients retain cancellation through outstanding streamed bodies.</summary>
    [Fact]
    public async Task InvocationDisposalStopsStreamWithoutCallerToken()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var files = new EndpointCredentialTestEnvironment($"http://127.0.0.1:{port}/v1.0/invoke/declared-owner/method/");
        files.Bind();
        using HttpClient client = files.Environment.CreateDaprInvocationClient("eventstore", "Hexalith.EventStore", "dapr-invocation", TimeSpan.FromSeconds(10));
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(10));
        Task<HttpResponseMessage> pending = client.GetAsync("api/v1/tenants", HttpCompletionOption.ResponseHeadersRead, bound.Token);
        using TcpClient accepted = await listener.AcceptTcpClientAsync(bound.Token);
        _ = await ReadHeadersAsync(accepted.GetStream(), bound.Token);
        await accepted.GetStream().WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 4096\r\nConnection: close\r\n\r\nx"), TestContext.Current.CancellationToken);
        using HttpResponseMessage response = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using Stream stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        byte[] buffer = new byte[1];
        (await stream.ReadAsync(buffer, TestContext.Current.CancellationToken)).ShouldBe(1);
        Task<int> blocked = stream.ReadAsync(buffer).AsTask();
        files.Environment.Dispose();
        Exception? failure = await Record.ExceptionAsync(async () => await blocked.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        (failure is IOException or ObjectDisposedException or OperationCanceledException).ShouldBeTrue();
    }

    /// <summary>A redirect reaches the caller unchanged; the Dapr credential is never re-sent to its target.</summary>
    [Fact]
    public async Task InvocationRedirectIsReturnedWithoutFollowing()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        using var target = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        target.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var files = new EndpointCredentialTestEnvironment($"http://127.0.0.1:{port}/v1.0/invoke/declared-owner/method/");
        files.Bind();
        using HttpClient client = files.Environment.CreateDaprInvocationClient("eventstore", "Hexalith.EventStore", "dapr-invocation", TimeSpan.FromSeconds(10));
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(10));
        Task<HttpResponseMessage> pending = client.GetAsync("tenants", bound.Token);
        using TcpClient accepted = await listener.AcceptTcpClientAsync(bound.Token);
        _ = await ReadHeadersAsync(accepted.GetStream(), bound.Token);
        string location = $"http://127.0.0.1:{((IPEndPoint)target.LocalEndpoint).Port}/v1.0/invoke/declared-owner/method/elsewhere";
        await accepted.GetStream().WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 307 Temporary Redirect\r\nLocation: {location}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), bound.Token);
        using HttpResponseMessage response = await pending.WaitAsync(TimeSpan.FromSeconds(5), bound.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.TemporaryRedirect);
        target.Pending().ShouldBeFalse("No credential-bearing request may follow a redirect.");
    }

    /// <summary>Both client factories require a finite positive request bound of at most five minutes.</summary>
    /// <param name="ticks">The requested timeout in ticks: zero, negative, infinite, and five minutes plus one tick.</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(-10_000L)]
    [InlineData(3_000_000_001L)]
    public void UnboundedOrNonPositiveTimeoutsRefuseBothClients(long ticks)
    {
        using var files = new EndpointCredentialTestEnvironment();
        files.Bind();
        TimeSpan timeout = TimeSpan.FromTicks(ticks);
        Should.Throw<TestEnvironmentDescriptorException>(() => files.Environment.CreateHttpClient("eventstore", "Hexalith.EventStore", "http", timeout)).Rule.ShouldBe("HXT110");
        Should.Throw<TestEnvironmentDescriptorException>(() => files.Environment.CreateDaprInvocationClient("eventstore", "Hexalith.EventStore", "dapr-invocation", timeout)).Rule.ShouldBe("HXT110");
        files.Environment.CreateHttpClient("eventstore", "Hexalith.EventStore", "http", TimeSpan.FromMinutes(5)).Dispose();
        files.Environment.CreateDaprInvocationClient("eventstore", "Hexalith.EventStore", "dapr-invocation", TimeSpan.FromMinutes(5)).Dispose();
    }

    /// <summary>Reads one bounded loopback request head.</summary>
    /// <param name="stream">The accepted loopback stream.</param>
    /// <param name="token">The bounded test token.</param>
    /// <returns>The request line and headers.</returns>
    internal static async Task<string> ReadHeadersAsync(NetworkStream stream, CancellationToken token)
    {
        byte[] bytes = new byte[16384];
        int used = 0;
        while (used < bytes.Length)
        {
            int count = await stream.ReadAsync(bytes.AsMemory(used), token);
            if (count == 0) break;
            used += count;
            string text = Encoding.ASCII.GetString(bytes, 0, used);
            if (text.Contains("\r\n\r\n", StringComparison.Ordinal)) return text;
        }
        throw new InvalidOperationException("The bounded loopback request did not contain complete headers.");
    }
}
