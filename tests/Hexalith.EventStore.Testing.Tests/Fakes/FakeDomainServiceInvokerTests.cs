using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Testing.Builders;
using Hexalith.EventStore.Testing.Fakes;

namespace Hexalith.EventStore.Testing.Tests.Fakes;

public class FakeDomainServiceInvokerTests {
    [Fact]
    public async Task InvokeAsync_returns_command_type_response() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new FakeDomainServiceInvoker();
        var expected = DomainResult.NoOp();
        sut.SetupResponse("TestCommand", expected);

        CommandEnvelope command = new CommandEnvelopeBuilder().WithCommandType("TestCommand").Build();
        DomainResult result = await sut.InvokeAsync(command, null, ct);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_returns_tenant_domain_response() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new FakeDomainServiceInvoker();
        var expected = DomainResult.NoOp();
        sut.SetupResponse(TestDataConstants.TenantId, TestDataConstants.Domain, expected);

        CommandEnvelope command = new CommandEnvelopeBuilder().Build();
        DomainResult result = await sut.InvokeAsync(command, null, ct);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_returns_default_response_when_no_specific_match() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new FakeDomainServiceInvoker();
        var expected = DomainResult.NoOp();
        sut.SetupDefaultResponse(expected);

        CommandEnvelope command = new CommandEnvelopeBuilder().WithCommandType("Unknown").Build();
        DomainResult result = await sut.InvokeAsync(command, null, ct);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_throws_when_no_response_configured() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new FakeDomainServiceInvoker();
        CommandEnvelope command = new CommandEnvelopeBuilder().WithCommandType("Unconfigured").Build();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InvokeAsync(command, null, ct));
    }

    [Fact]
    public async Task InvokeAsync_tracks_invocations() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new FakeDomainServiceInvoker();
        sut.SetupDefaultResponse(DomainResult.NoOp());

        CommandEnvelope cmd1 = new CommandEnvelopeBuilder().WithCommandType("Cmd1").Build();
        CommandEnvelope cmd2 = new CommandEnvelopeBuilder().WithCommandType("Cmd2").Build();

        _ = await sut.InvokeAsync(cmd1, null, ct);
        _ = await sut.InvokeAsync(cmd2, null, ct);

        Assert.Equal(2, sut.Invocations.Count);
        Assert.Same(cmd1, sut.Invocations[0]);
        Assert.Same(cmd2, sut.Invocations[1]);
    }

    [Fact]
    public async Task InvokeAsync_prefers_command_type_over_tenant_domain() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new FakeDomainServiceInvoker();
        var cmdTypeResult = DomainResult.NoOp();
        var tenantResult = DomainResult.Success(new IEventPayload[] { new TestEvent() });

        sut.SetupResponse("TestCommand", cmdTypeResult);
        sut.SetupResponse("test-tenant", "test-domain", tenantResult);

        CommandEnvelope command = new CommandEnvelopeBuilder().Build();
        DomainResult result = await sut.InvokeAsync(command, null, ct);

        Assert.Same(cmdTypeResult, result);
    }

    private sealed record TestEvent : IEventPayload;
}
