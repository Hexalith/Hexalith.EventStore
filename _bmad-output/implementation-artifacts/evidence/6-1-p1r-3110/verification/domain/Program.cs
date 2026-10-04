using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Client.Registration;
using Microsoft.AspNetCore.DataProtection;
using System.Reflection;
using System.Security.Cryptography;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddEventStoreDomainService();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(
    builder.Configuration["P1R_KEYS_PATH"] ?? throw new InvalidOperationException("Owned cursor key path required."))).SetApplicationName("P1R-fixture");
builder.Services.AddEventStoreQueryCursorCodec("P1R.fixture.cursor.v1");
WebApplication app = builder.Build();
app.UseEventStoreDomainService();
app.MapGet("/ready", () => Results.Ok(new
{
    ready = true,
    assemblies = new[] { "Hexalith.EventStore.DomainService", "Hexalith.EventStore.Client", "Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults" }
        .Select(Assembly.Load).Select(a => new { name = a.GetName().Name, version = a.GetName().Version?.ToString(), sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(a.Location))) }),
}));
app.MapGet("/cursor-mint", (IQueryCursorCodec codec) => Results.Ok(new { cursor = codec.Encode("fixture", "tenant-a|watermark:987", "position-3") }));
app.Run();
