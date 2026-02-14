namespace Hexalith.EventStore.Server.Tests.DomainServices;

using System.Net;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

public class DaprDomainServiceInvokerTests
{
    private readonly IDomainServiceResolver _resolver = Substitute.For<IDomainServiceResolver>();
    private readonly ILogger<DaprDomainServiceInvoker> _logger = NullLogger<DaprDomainServiceInvoker>.Instance;

    private static readonly DomainServiceRegistration TestRegistration = new("test-app", "process-command", "test-tenant", "test-domain", null);

    private static CommandEnvelope CreateTestEnvelope(
        string tenantId = "test-tenant",
        string domain = "test-domain") => new(
        TenantId: tenantId,
        Domain: domain,
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: Guid.NewGuid().ToString(),
        CausationId: null,
        UserId: "system",
        Extensions: null);

    [Fact]
    public async Task InvokeAsync_ServiceNotFound_ThrowsDomainServiceNotFoundException()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        _resolver.ResolveAsync("test-tenant", "test-domain", Arg.Any<CancellationToken>())
            .Returns((DomainServiceRegistration?)null);
        var invoker = new DaprDomainServiceInvoker(daprClient, _resolver, _logger);
        var envelope = CreateTestEnvelope();

        // Act & Assert
        var ex = await Should.ThrowAsync<DomainServiceNotFoundException>(() => invoker.InvokeAsync(envelope, null));
        ex.TenantId.ShouldBe("test-tenant");
        ex.Domain.ShouldBe("test-domain");
    }

    [Fact]
    public async Task InvokeAsync_NullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        var invoker = new DaprDomainServiceInvoker(daprClient, _resolver, _logger);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() => invoker.InvokeAsync(null!, null));
    }

    [Fact]
    public async Task InvokeAsync_ValidCommand_CallsResolver()
    {
        // Arrange -- resolver returns null to short-circuit (we only care about the resolver call)
        var daprClient = Substitute.For<DaprClient>();
        _resolver.ResolveAsync("my-tenant", "my-domain", Arg.Any<CancellationToken>())
            .Returns((DomainServiceRegistration?)null);
        var invoker = new DaprDomainServiceInvoker(daprClient, _resolver, _logger);
        var envelope = CreateTestEnvelope(tenantId: "my-tenant", domain: "my-domain");

        // Act & Assert -- expect exception since null registration, but verify resolver was called
        await Should.ThrowAsync<DomainServiceNotFoundException>(() => invoker.InvokeAsync(envelope, null));
        await _resolver.Received(1).ResolveAsync("my-tenant", "my-domain", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DomainServiceNotFoundException_ContainsRecoveryInfo()
    {
        // Act
        var ex = new DomainServiceNotFoundException("tenant-a", "payments");

        // Assert
        ex.TenantId.ShouldBe("tenant-a");
        ex.Domain.ShouldBe("payments");
        ex.Message.ShouldContain("tenant-a:payments:service");
    }

    [Fact]
    public void DomainServiceRegistration_PropertiesPreserved()
    {
        // Act
        var reg = new DomainServiceRegistration("app-1", "process", "t1", "d1", "v1");

        // Assert
        reg.AppId.ShouldBe("app-1");
        reg.MethodName.ShouldBe("process");
        reg.TenantId.ShouldBe("t1");
        reg.Domain.ShouldBe("d1");
        reg.Version.ShouldBe("v1");
    }

    [Fact]
    public void DomainServiceOptions_HasDefaults()
    {
        // Act
        var options = new DomainServiceOptions();

        // Assert
        options.ConfigStoreName.ShouldBe("configstore");
        options.InvocationTimeoutSeconds.ShouldBe(5);
    }

    [Fact]
    public void DomainServiceRequest_ContainsCommandAndState()
    {
        // Arrange
        var envelope = CreateTestEnvelope();
        object state = new { Version = 1 };

        // Act
        var request = new DomainServiceRequest(envelope, state);

        // Assert
        request.Command.ShouldBe(envelope);
        request.CurrentState.ShouldBe(state);
    }

    [Fact]
    public void DomainServiceRequest_NullStateAllowed()
    {
        // Arrange
        var envelope = CreateTestEnvelope();

        // Act
        var request = new DomainServiceRequest(envelope, null);

        // Assert
        request.CurrentState.ShouldBeNull();
    }

    // Test event types
    private sealed record TestEvent : IEventPayload;

    private sealed record TestRejectionEvent : IRejectionEvent;
}
