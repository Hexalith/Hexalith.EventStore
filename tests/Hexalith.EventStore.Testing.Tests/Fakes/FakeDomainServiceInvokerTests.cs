namespace Hexalith.EventStore.Testing.Tests.Fakes;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Testing.Builders;
using Hexalith.EventStore.Testing.Fakes;

public class FakeDomainServiceInvokerTests
{
    [Fact]
    public async Task InvokeAsync_returns_command_type_response()
    {
        var sut = new FakeDomainServiceInvoker();
        var expected = DomainResult.NoOp();
        sut.SetupResponse("TestCommand", expected);

        var command = new CommandEnvelopeBuilder().WithCommandType("TestCommand").Build();
        DomainResult result = await sut.InvokeAsync(command, null);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_returns_tenant_domain_response()
    {
        var sut = new FakeDomainServiceInvoker();
        var expected = DomainResult.NoOp();
        sut.SetupResponse("test-tenant", "test-domain", expected);

        var command = new CommandEnvelopeBuilder().Build();
        DomainResult result = await sut.InvokeAsync(command, null);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_returns_default_response_when_no_specific_match()
    {
        var sut = new FakeDomainServiceInvoker();
        var expected = DomainResult.NoOp();
        sut.SetupDefaultResponse(expected);

        var command = new CommandEnvelopeBuilder().WithCommandType("Unknown").Build();
        DomainResult result = await sut.InvokeAsync(command, null);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_throws_when_no_response_configured()
    {
        var sut = new FakeDomainServiceInvoker();
        var command = new CommandEnvelopeBuilder().WithCommandType("Unconfigured").Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InvokeAsync(command, null));
    }

    [Fact]
    public async Task InvokeAsync_tracks_invocations()
    {
        var sut = new FakeDomainServiceInvoker();
        sut.SetupDefaultResponse(DomainResult.NoOp());

        var cmd1 = new CommandEnvelopeBuilder().WithCommandType("Cmd1").Build();
        var cmd2 = new CommandEnvelopeBuilder().WithCommandType("Cmd2").Build();

        await sut.InvokeAsync(cmd1, null);
        await sut.InvokeAsync(cmd2, null);

        Assert.Equal(2, sut.Invocations.Count);
        Assert.Same(cmd1, sut.Invocations[0]);
        Assert.Same(cmd2, sut.Invocations[1]);
    }

    [Fact]
    public async Task InvokeAsync_prefers_command_type_over_tenant_domain()
    {
        var sut = new FakeDomainServiceInvoker();
        var cmdTypeResult = DomainResult.NoOp();
        var tenantResult = DomainResult.Success(new IEventPayload[] { new TestEvent() });

        sut.SetupResponse("TestCommand", cmdTypeResult);
        sut.SetupResponse("test-tenant", "test-domain", tenantResult);

        var command = new CommandEnvelopeBuilder().Build();
        DomainResult result = await sut.InvokeAsync(command, null);

        Assert.Same(cmdTypeResult, result);
    }

    private sealed record TestEvent : IEventPayload;
}
