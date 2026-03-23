using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Admin.Server.Host.Authentication;
using Hexalith.EventStore.Admin.Server.Host.Middleware;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.ServiceDefaults;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddDaprClient();
builder.Services.AddProblemDetails();
builder.Services.AddAdminApi(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAdminAuthContext, HttpContextAdminAuthContext>();
builder.Services
    .AddOptions<AdminServerAuthenticationOptions>()
    .BindConfiguration("Authentication:JwtBearer")
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AdminServerAuthenticationOptions>, ValidateAdminServerAuthenticationOptions>();
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddControllers()
    .AddApplicationPart(typeof(AdminStreamsController).Assembly);

WebApplication app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();

// OpenAPI/Swagger UI (gated by configuration)
if (app.Configuration.GetValue("EventStore:Admin:OpenApi:Enabled", true))
{
    _ = app.MapOpenApi();
    _ = app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Hexalith EventStore Admin API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapControllers();

app.Run();

/// <summary>
/// Entry point class, made partial for WebApplicationFactory test access.
/// </summary>
public partial class Program;
