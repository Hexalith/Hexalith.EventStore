using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies production authentication startup and anonymous access against SDK publish output.
/// </summary>
public sealed class PublishedProductionAuthenticationTests
{
    private const string _sampleName = "Hexalith.EventStore.Sample.Api";
    private const string _listeningMessage = "Now listening on: ";
    private static readonly TimeSpan _startupTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Starts the published Sample API with RS256 and rejects the same settings with that algorithm omitted.</summary>
    [Fact]
    public async Task PublishedSampleApiRequiresAnExplicitProductionAuthorityAlgorithmAsync()
    {
        string root = FindRepositoryRoot();
        DirectoryInfo scratch = Directory.CreateTempSubdirectory("sample-api-production-publish-");
        try
        {
            string publish = Path.Combine(scratch.FullName, "publish");
            string project = Path.Combine("samples", _sampleName, _sampleName + ".csproj");
            ProcessStartInfo publishStart = Dotnet(root,
                "publish", project, "--configuration", "Release", "--disable-build-servers", "-m:1",
                "-p:UseSharedCompilation=false", "-p:MinVerVersionOverride=1.0.0", "-p:UseNuGetDeps=true",
                "-p:RestoreFallbackFolders=", "--artifacts-path", Path.Combine(scratch.FullName, "artifacts"),
                "--output", publish);
            (int publishExit, string publishOutput) = await RunAsync(publishStart, TimeSpan.FromSeconds(120));
            Assert.True(publishExit == 0, $"Release Sample API publish exited {publishExit}.{Environment.NewLine}{publishOutput}");
            string executable = Path.Combine(publish, _sampleName + ".dll");
            File.Exists(executable).ShouldBeTrue();

            string runtimeCredential = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            ProcessStartInfo configured = ProductionHost(executable, runtimeCredential);
            configured.Environment["Authentication__JwtBearer__AllowedAlgorithms__0"] = "RS256";
            _ = await RunAsync(configured, _startupTimeout, AssertEndpointsAsync);

            ProcessStartInfo missingAlgorithm = ProductionHost(executable, runtimeCredential);
            (int hostExit, string diagnostics) = await RunAsync(missingAlgorithm, _startupTimeout);
            Assert.False(diagnostics.Contains(runtimeCredential, StringComparison.OrdinalIgnoreCase),
                "The published host included the generated credential in its startup diagnostics.");
            hostExit.ShouldNotBe(0);
            diagnostics.ShouldContain("OptionsValidationException");
            diagnostics.ShouldContain("Authentication:JwtBearer:AllowedAlgorithms must contain at least one non-blank supported asymmetric algorithm");
            diagnostics.ShouldNotContain(_listeningMessage);
            diagnostics.ShouldNotContain("Application started.");
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    private static async Task AssertEndpointsAsync(StreamReader output, CancellationToken cancellationToken)
    {
        while (await output.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.Contains(_listeningMessage, StringComparison.Ordinal))
            {
                continue;
            }

            using JsonDocument status = JsonDocument.Parse(line);
            status.RootElement.GetProperty("Category").GetString().ShouldBe("Microsoft.Hosting.Lifetime");
            string? listener = status.RootElement.GetProperty("State").GetProperty("address").GetString();
            var address = new Uri(listener.ShouldNotBeNull(), UriKind.Absolute);
            address.Scheme.ShouldBe(Uri.UriSchemeHttp);
            address.Host.ShouldBe(IPAddress.Loopback.ToString());
            address.Port.ShouldBeGreaterThan(0);
            using var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false })
            {
                BaseAddress = address,
            };
            using HttpResponseMessage liveness = await client.GetAsync("/alive", cancellationToken);
            liveness.StatusCode.ShouldBe(HttpStatusCode.OK);
            using HttpResponseMessage protectedRoute = await client.GetAsync("/api/tenant-a/counter/counter-1", cancellationToken);
            protectedRoute.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            protectedRoute.Headers.WwwAuthenticate.ShouldContain(header => header.Scheme == "Bearer");
            return;
        }

        Assert.Fail("The published Sample API exited before listening on loopback with explicit RS256 settings.");
    }

    private static ProcessStartInfo ProductionHost(string executable, string runtimeCredential)
    {
        ProcessStartInfo start = Dotnet(Path.GetDirectoryName(executable)!, executable);
        start.Environment.Clear();
        foreach (string name in new[] { "PATH", "SystemRoot", "DOTNET_ROOT", "HOME", "TMPDIR", "TMP", "TEMP" })
        {
            if (Environment.GetEnvironmentVariable(name) is { } value)
            {
                start.Environment[name] = value;
            }
        }

        const string authority = "https://127.0.0.1:1";
        start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        start.Environment["Authentication__JwtBearer__Authority"] = authority;
        start.Environment["Authentication__JwtBearer__Issuer"] = authority;
        start.Environment["Authentication__JwtBearer__Audience"] = "sample-api-publish";
        start.Environment["Authentication__JwtBearer__RequireHttpsMetadata"] = "true";
        start.Environment["DAPR_HTTP_ENDPOINT"] = "http://127.0.0.1:1";
        start.Environment["DAPR_API_TOKEN"] = runtimeCredential;
        start.Environment["Logging__LogLevel__Microsoft.Hosting.Lifetime"] = "Information";
        return start;
    }

    private static ProcessStartInfo Dotnet(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return start;
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(ProcessStartInfo start, TimeSpan timeout,
        Func<StreamReader, CancellationToken, Task>? inspectListener = null)
    {
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start the SDK or published Sample API.");
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(timeout);
        Task<string> errors = process.StandardError.ReadToEndAsync();
        Task<string>? output = null;
        try
        {
            if (inspectListener is null)
            {
                output = process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(bound.Token);
            }
            else
            {
                await inspectListener(process.StandardOutput, bound.Token);
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(cleanup.Token);
            output ??= process.StandardOutput.ReadToEndAsync();
            await Task.WhenAll(output, errors).WaitAsync(cleanup.Token);
        }

        return (process.ExitCode, await output + await errors);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.EventStore.slnx")))
        {
            root = root.Parent;
        }

        return root?.FullName ?? throw new DirectoryNotFoundException("Cannot find the EventStore repository containing the Sample API.");
    }
}
