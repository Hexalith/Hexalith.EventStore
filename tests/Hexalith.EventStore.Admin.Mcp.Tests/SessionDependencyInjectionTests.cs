
using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tools;
using Hexalith.EventStore.Testing.Http;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class SessionDependencyInjectionTests {
    [Fact]
    public async Task SessionTools_UseSingletonSessionResolvedFromContainer() {
        ServiceCollection services = [];

        using HttpClient httpClient = MockHttpMessageHandler.CreateJsonClient(System.Net.HttpStatusCode.OK, "[]");
        _ = services.AddSingleton(httpClient);
        _ = services.AddSingleton<AdminApiClient>(provider => new AdminApiClient(provider.GetRequiredService<HttpClient>()));
        _ = services.AddSingleton<InvestigationSession>();

        _ = services
            .AddMcpServer(options => options.ServerInfo = new() {
                Name = "test-admin-mcp",
                Version = "1.0.0",
                Description = "test",
            })
            .WithToolsFromAssembly();

        using ServiceProvider provider = services.BuildServiceProvider();

        InvestigationSession sessionFromContainer = provider.GetRequiredService<InvestigationSession>();
        string setResult = await SessionTools.SetContext(sessionFromContainer, tenantId: "acme-corp", domain: "Orders");
        _ = Should.NotThrow(() => JsonDocument.Parse(setResult));

        InvestigationSession sameSession = provider.GetRequiredService<InvestigationSession>();
        sameSession.ShouldBeSameAs(sessionFromContainer);

        string getResult = await SessionTools.GetContext(sameSession);
        using var doc = JsonDocument.Parse(getResult);
        doc.RootElement.GetProperty("tenantId").GetString().ShouldBe("acme-corp");
        doc.RootElement.GetProperty("domain").GetString().ShouldBe("Orders");
        doc.RootElement.GetProperty("hasContext").GetBoolean().ShouldBeTrue();
    }
}
