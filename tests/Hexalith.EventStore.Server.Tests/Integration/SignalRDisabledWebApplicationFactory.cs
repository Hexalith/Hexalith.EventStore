extern alias eventstore;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

using EventStoreProgram = eventstore::Program;

namespace Hexalith.EventStore.Server.Tests.Integration;

public class SignalRDisabledWebApplicationFactory : WebApplicationFactory<EventStoreProgram> {
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.UseEnvironment("Development");

        _ = builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["EventStore:SignalR:Enabled"] = "false",
        }));
    }
}
