extern alias eventstore;

using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

        _ = builder.ConfigureTestServices(
            WebApplicationFactoryServiceOverrides.RemoveAdminOperationalIndexHostedService);
    }
}
