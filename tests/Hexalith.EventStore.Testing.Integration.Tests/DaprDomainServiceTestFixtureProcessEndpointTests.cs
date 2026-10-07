using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

namespace Hexalith.EventStore.Testing.Integration.Tests;

/// <summary>
/// Story 5.5 (FR28): the shipped harness <c>/process</c> endpoint treats a wire <c>actor:globalAdmin</c> flag as
/// untrusted exactly like the SDK route — removed unless every registered verifier confirms the acting user, and a
/// verifier failure is a bounded 503 before any domain work.
/// </summary>
public sealed class DaprDomainServiceTestFixtureProcessEndpointTests {
    private const string Domain = "widget";

    /// <summary>A forged administrator flag reaches the processor removed unless a verifier confirms it.</summary>
    /// <param name="verifier">The registered verifier: none, allow, or deny.</param>
    /// <param name="expectedFlag">Whether the processor must observe the flag.</param>
    [Theory]
    [InlineData("none", false)]
    [InlineData("deny", false)]
    [InlineData("allow", true)]
    public async Task ProcessEndpoint_RemovesForgedAdministratorFlagUnlessVerified(string verifier, bool expectedFlag) {
        var processor = new CapturingProcessor();
        await using WebApplication app = await StartAsync(processor, services => {
            if (verifier != "none") {
                _ = services.AddSingleton<IDomainServiceAdministratorVerifier>(new DelegateVerifier(_ => verifier == "allow"));
            }
        });

        using HttpResponseMessage response = await app.GetTestClient()
            .PostAsJsonAsync("/process", ForgedAdministratorRequest(), JsonSerializerOptions.Web, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CommandEnvelope command = processor.Commands.ShouldHaveSingleItem();
        (command.Extensions?.TryGetValue(DomainServiceAdministratorAssertions.GlobalAdminExtensionKey, out string? value) == true && value == "true")
            .ShouldBe(expectedFlag, verifier);
        command.Extensions!["keep"].ShouldBe("me");
    }

    /// <summary>An unavailable verifier refuses the request with 503 and the processor never runs.</summary>
    [Fact]
    public async Task ProcessEndpoint_VerifierFailure_Returns503WithoutDomainWork() {
        var processor = new CapturingProcessor();
        await using WebApplication app = await StartAsync(processor, services =>
            services.AddSingleton<IDomainServiceAdministratorVerifier>(
                new DelegateVerifier(_ => throw new InvalidOperationException("verifier state unavailable"))));

        using HttpResponseMessage response = await app.GetTestClient()
            .PostAsJsonAsync("/process", ForgedAdministratorRequest(), JsonSerializerOptions.Web, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        processor.Commands.ShouldBeEmpty();
    }

    private static async Task<WebApplication> StartAsync(CapturingProcessor processor, Action<IServiceCollection> configureServices) {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        _ = builder.Services.AddKeyedSingleton<IDomainProcessor>(Domain, processor);
        configureServices(builder.Services);
        WebApplication app = builder.Build();
        new HarnessFixture().MapProcessEndpoint(app);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static DomainServiceRequest ForgedAdministratorRequest()
        => new(
            new CommandEnvelope(
                MessageId: "01JHARNESS0000000000000000",
                TenantId: "tenant-a",
                Domain: Domain,
                AggregateId: "widget-1",
                CommandType: "ProbeWidget",
                Payload: [123, 125],
                CorrelationId: "corr-harness",
                CausationId: null,
                UserId: "acting-user",
                Extensions: new Dictionary<string, string>(StringComparer.Ordinal) {
                    [DomainServiceAdministratorAssertions.GlobalAdminExtensionKey] = "true",
                    ["keep"] = "me",
                }),
            CurrentState: null);

    private sealed class HarnessFixture : DaprDomainServiceTestFixtureBase {
        protected override string AppId => "harness";

        protected override void ConfigureDomain(WebApplicationBuilder builder) {
        }
    }

    private sealed class CapturingProcessor : IDomainProcessor {
        private readonly ConcurrentQueue<CommandEnvelope> _commands = new();

        public IReadOnlyCollection<CommandEnvelope> Commands => [.. _commands];

        public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState) {
            ArgumentNullException.ThrowIfNull(command);
            _commands.Enqueue(command);
            return Task.FromResult(DomainResult.Success(new IEventPayload[] { new WidgetProbed() }));
        }
    }

    private sealed record WidgetProbed : IEventPayload;

    private sealed class DelegateVerifier(Func<DomainServiceAdministratorClaim, bool> verify) : IDomainServiceAdministratorVerifier {
        public Task<bool> IsCurrentGlobalAdministratorAsync(DomainServiceAdministratorClaim claim, CancellationToken cancellationToken)
            => Task.FromResult(verify(claim));
    }
}
