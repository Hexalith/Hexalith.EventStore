using Hexalith.EventStore.Admin.UI;
using Hexalith.EventStore.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Fail fast (with actionable guidance) if launched without a DAPR sidecar — Admin.UI
// reaches Admin.Server only via DAPR service invocation (D13). Guard lives here, not in
// AddAdminUI, so test/E2E hosts that build the UI without a sidecar are unaffected.
builder.RequireDaprSidecar();

builder.AddAdminUI();

WebApplication app = builder.Build();

app.UseAdminUI();
app.MapDefaultEndpoints();

await app.StartSignalRAsync().ConfigureAwait(false);

await app.RunAsync().ConfigureAwait(false);
