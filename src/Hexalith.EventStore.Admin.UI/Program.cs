using Hexalith.EventStore.Admin.UI;
using Hexalith.EventStore.ServiceDefaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAdminUI();

WebApplication app = builder.Build();

app.UseAdminUI();
app.MapDefaultEndpoints();

await app.StartSignalRAsync().ConfigureAwait(false);

await app.RunAsync().ConfigureAwait(false);
