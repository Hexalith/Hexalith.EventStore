using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using System.Text;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed partial class CompleteCommandSourceTests
{
    [Theory]
    [InlineData("Conversation")]
    [InlineData("bad_domain")]
    [InlineData("bad.domain")]
    [InlineData("")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void MandatoryIntakeRejectsNoncanonicalOrInvalidDomainIdentifiers(string domain)
        => Should.Throw<ArgumentException>(() => new ServiceCollection().AddEventStoreCompleteCommandSourceVerification("eventstore", [domain]));
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulKeyedProcessorReceivesAdmittedSourceAfterAwaitAndContextAlwaysCleansUp(bool fail)
    {
        var request = Request(2); var processor = Substitute.For<IDomainProcessor>();
        int invoked = 0;
        processor.ProcessAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(async call =>
        {
            await Task.Yield(); invoked++;
            var source = CompleteCommandSourceContext.Current.ShouldNotBeNull();
            source.Identity.ShouldBe(request.CompleteSource!.Identity); source.Proof.ShouldBe(request.CompleteSource.Proof);
            source.Events.SelectMany(item => item.Payload).ShouldBe(request.CompleteSource.Events.SelectMany(item => item.Payload));
            call.Arg<CommandEnvelope>().Payload.ShouldBe(request.Command.Payload);
            if (fail) throw new InvalidOperationException("Controlled processor failure.");
            return DomainResult.NoOp();
        });
        using var services = new ServiceCollection().AddEventStoreCompleteCommandSourceVerification("eventstore", ["conversation"])
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = Principal(request) } })
            .AddKeyedSingleton("conversation", processor).BuildServiceProvider();
        if (fail) await Should.ThrowAsync<InvalidOperationException>(() => DomainServiceRequestRouter.ProcessAsync(services, request, TestContext.Current.CancellationToken));
        else (await DomainServiceRequestRouter.ProcessAsync(services, request, TestContext.Current.CancellationToken)).Events.ShouldBeEmpty();
        invoked.ShouldBe(1); CompleteCommandSourceContext.Current.ShouldBeNull();
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("case")]
    [InlineData("reordered")]
    public async Task ActualProcessEndpointAuthenticatesOriginalBeforeAdministratorNormalization(string change)
    {
        var request = Request(2);
        request = request with { Command = request.Command with { Extensions = new Dictionary<string, string>
        { [change == "case" ? "ACTOR:GLOBALADMIN" : "actor:globalAdmin"] = "true", ["keep"] = "value" } } };
        var processor = Substitute.For<IDomainProcessor>(); bool observed = false;
        processor.ProcessAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(async call =>
        {
            await Task.Yield(); observed = true;
            var source = CompleteCommandSourceContext.Current.ShouldNotBeNull(); source.Proof.ShouldBe(request.CompleteSource!.Proof);
            source.Events[0].Payload.ShouldBe(request.CompleteSource.Events[0].Payload);
            var command = call.Arg<CommandEnvelope>(); command.Payload.ShouldBe(request.Command.Payload);
            command.Extensions!.ContainsKey("ACTOR:GLOBALADMIN").ShouldBeFalse();
            command.Extensions.ContainsKey("actor:globalAdmin").ShouldBe(change != "removed");
            if (change != "removed") command.Extensions.Keys.ShouldBe(new[] { "keep", "actor:globalAdmin" });
            return DomainResult.NoOp();
        });
        const string key = "component-process-authentication-key-at-least-thirty-two-bytes";
        string appChannelToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development }); builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:JwtBearer:Authority"] = "", ["Authentication:JwtBearer:Issuer"] = "component-issuer",
            ["Authentication:JwtBearer:Audience"] = "receiver", ["Authentication:JwtBearer:SigningKey"] = key,
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false", ["Authentication:Workload:Audience"] = "receiver",
            ["Authentication:Workload:AllowedCallers:0"] = "eventstore", ["APP_API_TOKEN"] = appChannelToken,
        });
        builder.AddEventStoreDomainService(typeof(CommandEnvelope).Assembly);
        // Preserve this fixture's signed-source authentication focus independently of catalog lifecycle.
        builder.Services.AddScoped<IDomainRouteCatalogGuard, DomainRouteCatalogIsolatedGuard>();
        builder.Services.AddEventStoreCompleteCommandSourceVerification("eventstore", ["conversation"]);
        builder.Services.AddKeyedSingleton("conversation", processor);
        var administrator = Substitute.For<IDomainServiceAdministratorVerifier>();
        administrator.IsCurrentGlobalAdministratorAsync(Arg.Any<DomainServiceAdministratorClaim>(), Arg.Any<CancellationToken>()).Returns(change != "removed");
        builder.Services.AddSingleton(administrator);
        await using var app = builder.Build();
        app.UseEventStoreDomainService(); await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        var claims = CommandSourceAuthorityBinding.Bind(request).ToDictionary(pair => pair.Key, pair => (object)pair.Value, StringComparer.Ordinal);
        claims[EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = "eventstore";
        claims[EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = EventStoreWorkloadOperations.DomainServiceProcess;
        string assertion = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "component-issuer", Audience = "receiver", IssuedAt = DateTime.UtcNow, NotBefore = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMinutes(2), Claims = claims,
            SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256),
        });
        client.DefaultRequestHeaders.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, appChannelToken);
        using var response = await client.PostAsJsonAsync("/process", request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK); observed.ShouldBeTrue();
        await administrator.Received(1).IsCurrentGlobalAdministratorAsync(Arg.Is<DomainServiceAdministratorClaim>(claim => claim.UserId == request.Command.UserId), Arg.Any<CancellationToken>());
        CompleteCommandSourceContext.Current.ShouldBeNull();
    }
}
