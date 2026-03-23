using Hexalith.EventStore.Admin.UI.Services;
using Hexalith.EventStore.ServiceDefaults;

using Microsoft.FluentUI.AspNetCore.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

// Blazor Server with interactive server-side rendering
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Fluent UI v4 components
builder.Services.AddFluentUIComponents();

// Authentication & authorization
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer();
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Admin API authentication for protected Admin.Server endpoints
builder.Services.AddSingleton<AdminApiAccessTokenProvider>();
builder.Services.AddTransient<AdminApiAuthorizationHandler>();

// Admin user context for role-based UI rendering
builder.Services.AddScoped<AdminUserContext>();
builder.Services.AddScoped<ThemeState>();

// HttpClient for querying Admin.Server via Aspire service discovery
builder.Services.AddHttpClient("AdminApi", client =>
    client.BaseAddress = new Uri(builder.Configuration["EventStore:AdminServer:BaseUrl"]
        ?? "https://admin-server"))
    .AddHttpMessageHandler<AdminApiAuthorizationHandler>();

WebApplication app = builder.Build();

_ = app.UseExceptionHandler();

if (!app.Environment.IsDevelopment()) {
    _ = app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<Hexalith.EventStore.Admin.UI.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
