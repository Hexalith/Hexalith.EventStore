using Hexalith.EventStore.DomainService.Tests.Fixtures;
using Hexalith.EventStore.DomainService.Tests.Processes;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Refusal budgets preserve process/configuration precedence and validate only the required input bound.</summary>
public sealed class DomainServiceTerminalRefusalOptionsTests
{
    [Theory]
    [InlineData("PDR", false)]
    [InlineData("PDR", true)]
    [InlineData("PRD", false)]
    [InlineData("PRD", true)]
    [InlineData("DPR", false)]
    [InlineData("DPR", true)]
    [InlineData("DRP", false)]
    [InlineData("DRP", true)]
    [InlineData("RPD", false)]
    [InlineData("RPD", true)]
    [InlineData("RDP", false)]
    [InlineData("RDP", true)]
    public void ProcessExplicitConfigurationKeepsItsExistingPrecedenceInEveryRegistrationOrder(string order, bool configureAfter)
    {
        ArgumentNullException.ThrowIfNull(order);
        var builder = WebApplication.CreateBuilder();
        ConfigureSection(builder.Configuration);
        void Explicit(EventStoreProcessOptions options)
        {
            options.MaxCommandBytesPerEnvelope = 64;
            options.OperationTimeout = TimeSpan.FromMilliseconds(350);
            options.LeaseDuration = TimeSpan.FromSeconds(100);
        }
        if (!configureAfter) builder.Services.Configure<EventStoreProcessOptions>(Explicit);
        foreach (char registration in order)
            switch (registration)
            {
                case 'P': builder.Services.AddEventStoreProcesses<EmptyCatalog, EmptyTranslator>(options =>
                    {
                        options.MaxCommandBytesPerEnvelope = 96;
                        options.OperationTimeout = TimeSpan.FromMilliseconds(400);
                        options.LeaseDuration = TimeSpan.FromSeconds(90);
                    }); break;
                case 'D': builder.AddEventStoreDomainService(typeof(WidgetAggregate).Assembly); break;
                case 'R': Register(builder.Services, false); break;
            }
        if (configureAfter) builder.Services.Configure<EventStoreProcessOptions>(Explicit);
        using var provider = builder.Services.BuildServiceProvider();
        var actual = provider.GetRequiredService<IOptions<EventStoreProcessOptions>>().Value;
        actual.MaxCommandBytesPerEnvelope.ShouldBe(configureAfter ? 64 : 96);
        actual.OperationTimeout.ShouldBe(TimeSpan.FromMilliseconds(configureAfter ? 350 : 400));
        actual.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(configureAfter ? 100 : 90));
        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Theory]
    [InlineData("DR", false)]
    [InlineData("DR", true)]
    [InlineData("RD", false)]
    [InlineData("RD", true)]
    public void WithoutProcessesExplicitOptionsWinBeforeOrAfterDomainAndRefusalRegistration(string order, bool configureAfter)
    {
        ArgumentNullException.ThrowIfNull(order);
        var builder = WebApplication.CreateBuilder();
        ConfigureSection(builder.Configuration);
        void Explicit(EventStoreProcessOptions options)
        {
            options.MaxCommandBytesPerEnvelope = 64;
            options.OperationTimeout = TimeSpan.FromMilliseconds(350);
            options.LeaseDuration = TimeSpan.FromSeconds(100);
        }
        if (!configureAfter) builder.Services.Configure<EventStoreProcessOptions>(Explicit);
        foreach (char registration in order)
            if (registration == 'D') builder.AddEventStoreDomainService(typeof(WidgetAggregate).Assembly);
            else Register(builder.Services, true);
        if (configureAfter) builder.Services.Configure<EventStoreProcessOptions>(Explicit);
        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<EventStoreProcessOptions>>().Value;
        options.MaxCommandBytesPerEnvelope.ShouldBe(64);
        options.OperationTimeout.ShouldBe(TimeSpan.FromMilliseconds(350));
        options.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(100));
        provider.GetService<CheckpointedProcessRunner>().ShouldBeNull();
        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1048577, false)]
    [InlineData(1, false)]
    [InlineData(128, false)]
    [InlineData(1048576, false)]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(1048577, true)]
    [InlineData(1, true)]
    [InlineData(128, true)]
    [InlineData(1048576, true)]
    public async Task RefusalRulesFailAtStartupOnlyForTheirRequiredBudget(int budget, bool scoped)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["EventStore:Processes:MaxCommandBytesPerEnvelope"] = budget.ToString(System.Globalization.CultureInfo.InvariantCulture);
        // These unrelated process settings are deliberately invalid and must never enroll process validation.
        builder.Configuration["EventStore:Processes:PageSize"] = "0";
        builder.Configuration["EventStore:Processes:OperationTimeout"] = "00:00:00";
        Register(builder.Services, scoped);
        using var host = builder.Build();
        if (budget is < 1 or > 1024 * 1024)
        {
            var failure = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
            failure.Failures.ShouldHaveSingleItem().ShouldBe("Terminal refusal MaxCommandBytesPerEnvelope must be 1..1048576.");
        }
        else
        {
            await host.StartAsync(TestContext.Current.CancellationToken);
            var options = host.Services.GetRequiredService<IOptions<EventStoreProcessOptions>>().Value;
            options.MaxCommandBytesPerEnvelope.ShouldBe(budget);
            options.PageSize.ShouldBe(new EventStoreProcessOptions().PageSize);
            options.OperationTimeout.ShouldBe(new EventStoreProcessOptions().OperationTimeout);
            host.Services.GetService<CheckpointedProcessRunner>().ShouldBeNull();
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void OrdinaryDomainWithoutRulesBindsOnlyBudgetAndDoesNotAddRefusalValidation()
    {
        var builder = WebApplication.CreateBuilder();
        ConfigureSection(builder.Configuration);
        builder.Configuration["EventStore:Processes:MaxCommandBytesPerEnvelope"] = "0";
        builder.AddEventStoreDomainService(typeof(WidgetAggregate).Assembly);
        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<EventStoreProcessOptions>>().Value;
        options.MaxCommandBytesPerEnvelope.ShouldBe(0);
        options.OperationTimeout.ShouldBe(new EventStoreProcessOptions().OperationTimeout);
        options.LeaseDuration.ShouldBe(new EventStoreProcessOptions().LeaseDuration);
        provider.GetServices<IValidateOptions<EventStoreProcessOptions>>().ShouldBeEmpty();
        provider.GetService<CheckpointedProcessRunner>().ShouldBeNull();
    }

    [Fact]
    public void FailedSerializerRegistrationDoesNotLeaveRefusalStartupValidationOnAnOrdinaryHost()
    {
        var services = new ServiceCollection();
        services.Configure<EventStoreProcessOptions>(options => options.MaxCommandBytesPerEnvelope = 0);
        Should.Throw<ArgumentException>(() => services.AddEventStoreDomainServiceTerminalRefusal("widget", "unsupported",
            (_, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), profile =>
                profile.Add<Hexalith.EventStore.Contracts.Events.IRejectionEvent>("rejected", "json", 0, (_, _, _) => Task.CompletedTask)));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<EventStoreProcessOptions>>().Value.MaxCommandBytesPerEnvelope.ShouldBe(0);
        provider.GetServices<DomainServiceTerminalRefusalRule>().ShouldBeEmpty();
        provider.GetService<IStartupValidator>().ShouldBeNull();
    }

    private static void ConfigureSection(IConfiguration configuration)
    {
        configuration["EventStore:Processes:MaxCommandBytesPerEnvelope"] = "128";
        configuration["EventStore:Processes:OperationTimeout"] = "00:00:00.450";
        configuration["EventStore:Processes:LeaseDuration"] = "00:01:40";
    }

    private static void Register(IServiceCollection services, bool scoped)
    {
        if (scoped) services.AddEventStoreDomainServiceTerminalRefusal<object>("widget", "unsupported",
            (_, _, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), _ => { });
        else services.AddEventStoreDomainServiceTerminalRefusal("widget", "unsupported",
            (_, _) => Task.FromResult(DomainServiceTerminalRefusal.NoOpinion), _ => { });
    }
}
