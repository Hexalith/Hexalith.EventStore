using System.Reflection;

using Hexalith.EventStore.Admin.Mcp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Validate required environment variables before starting
string? adminUrl = Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_URL");
string? adminToken = Environment.GetEnvironmentVariable("EVENTSTORE_ADMIN_TOKEN")?.Trim();

List<string> errors = [];
if (string.IsNullOrWhiteSpace(adminUrl))
{
    errors.Add("Missing EVENTSTORE_ADMIN_URL");
}
else if (!Uri.TryCreate(adminUrl, UriKind.Absolute, out Uri? parsedUri)
    || (parsedUri.Scheme != "http" && parsedUri.Scheme != "https"))
{
    errors.Add($"EVENTSTORE_ADMIN_URL '{adminUrl}' is not a valid absolute HTTP(S) URI");
}
else
{
    adminUrl = adminUrl.TrimEnd('/');
}

if (string.IsNullOrWhiteSpace(adminToken))
{
    errors.Add("Missing EVENTSTORE_ADMIN_TOKEN");
}

if (errors.Count > 0)
{
    await Console.Error.WriteLineAsync(
        $"Error: {string.Join("; ", errors)}\n"
        + "Usage: Set EVENTSTORE_ADMIN_URL (e.g., https://localhost:5443) "
        + "and EVENTSTORE_ADMIN_TOKEN (Bearer token for Admin API authentication).")
        .ConfigureAwait(false);
    return 1;
}

string version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion ?? "0.0.0";

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Logging to stderr only — stdout is reserved for MCP JSON-RPC protocol
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
    options.LogToStandardErrorThreshold = LogLevel.Trace);

// Register AdminApiClient as typed HttpClient
builder.Services.AddHttpClient<AdminApiClient>(client =>
{
    client.BaseAddress = new Uri(adminUrl!);
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Register InvestigationSession as singleton for MCP session context
builder.Services.AddSingleton<InvestigationSession>();

// Register MCP server with stdio transport
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "hexalith-eventstore-admin",
            Version = version,
            Description = "Hexalith EventStore administration MCP server — query streams, inspect projections, diagnose issues, and manage operations via AI-callable tools",
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync().ConfigureAwait(false);
return 0;
