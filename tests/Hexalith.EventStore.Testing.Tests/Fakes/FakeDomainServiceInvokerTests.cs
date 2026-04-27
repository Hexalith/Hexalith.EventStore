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

    /// <summary>
    /// Story R1-A7 / AC #6: SetupHandler delegates to a function that receives the
    /// (command, currentState) pair the actor passed to InvokeAsync. The invocation is
    /// recorded BEFORE the handler runs so Invocations stays accurate even if the handler throws.
    /// </summary>
    [Fact]
    public async Task SetupHandler_HandlerInvokedWithCommandAndCurrentState() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new FakeDomainServiceInvoker();
        var sentinel = new object();
        CommandEnvelope? capturedCommand = null;
        object? capturedState = null;
        var expected = DomainResult.NoOp();

        sut.SetupHandler(
            commandType: "Probe",
            handler: (cmd, state) => {
                capturedCommand = cmd;
                capturedState = state;
                return Task.FromResult(expected);
            });

        CommandEnvelope command = new CommandEnvelopeBuilder().WithCommandType("Probe").Build();
        DomainResult result = await sut.InvokeAsync(command, sentinel, ct);

        Assert.Same(expected, result);
        Assert.Same(command, capturedCommand);
        Assert.Same(sentinel, capturedState);
        CommandEnvelope only = Assert.Single(sut.Invocations);
        Assert.Same(command, only);
    }

    /// <summary>
    /// Story R1-A7 / ADR R1A7-01: SetupHandler throws when a static SetupResponse is already
    /// registered for the same command type. Pins the mutual-exclusion contract — direction A.
    /// </summary>
    [Fact]
    public void SetupHandler_ThrowsWhenSetupResponseAlreadyRegistered() {
        var sut = new FakeDomainServiceInvoker();
        sut.SetupResponse("Probe", DomainResult.NoOp());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => sut.SetupHandler("Probe", (_, _) => Task.FromResult(DomainResult.NoOp())));

        Assert.Contains("Probe", ex.Message);
    }

    /// <summary>
    /// Story R1-A7 / ADR R1A7-01: SetupResponse throws when a SetupHandler is already
    /// registered for the same command type. Pins the mutual-exclusion contract — direction B.
    /// Together with the previous test, the bidirectional contract is fully pinned.
    /// </summary>
    [Fact]
    public void SetupResponse_ThrowsWhenSetupHandlerAlreadyRegistered() {
        var sut = new FakeDomainServiceInvoker();
        sut.SetupHandler("Probe", (_, _) => Task.FromResult(DomainResult.NoOp()));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => sut.SetupResponse("Probe", DomainResult.NoOp()));

        Assert.Contains("Probe", ex.Message);
    }

    /// <summary>
    /// Story R1-A7 / AC #6: SetupHandler null guards mirror SetupResponse's existing guards.
    /// </summary>
    [Fact]
    public void SetupHandler_ThrowsArgumentNullExceptionForNullArguments() {
        var sut = new FakeDomainServiceInvoker();

        ArgumentNullException nullCommandType = Assert.Throws<ArgumentNullException>(
            () => sut.SetupHandler(null!, (_, _) => Task.FromResult(DomainResult.NoOp())));
        Assert.Equal("commandType", nullCommandType.ParamName);

        ArgumentNullException nullHandler = Assert.Throws<ArgumentNullException>(
            () => sut.SetupHandler("Probe", null!));
        Assert.Equal("handler", nullHandler.ParamName);
    }

    /// <summary>
    /// Story R1-A7 / AC #2: ClearAll() resets every registry. Re-registering the previously
    /// conflicting SetupHandler key succeeds — proving the response-side was actually cleared,
    /// not just shadowed by mutual-exclusion bookkeeping.
    /// </summary>
    [Fact]
    public async Task ClearAll_RemovesAllRegistrationsAndInvocations() {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var sut = new FakeDomainServiceInvoker();

        sut.SetupResponse("A", DomainResult.NoOp());
        sut.SetupHandler("B", (_, _) => Task.FromResult(DomainResult.NoOp()));
        sut.SetupResponse("tenant", "domain", DomainResult.NoOp());
        sut.SetupDefaultResponse(DomainResult.NoOp());

        CommandEnvelope cmd = new CommandEnvelopeBuilder().WithCommandType("A").Build();
        _ = await sut.InvokeAsync(cmd, null, ct);
        _ = Assert.Single(sut.Invocations);

        sut.ClearAll();

        // All four registries are empty.
        Assert.Empty(sut.Invocations);
        CommandEnvelope unconfigured = new CommandEnvelopeBuilder().WithCommandType("Unconfigured").Build();
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.InvokeAsync(unconfigured, null, ct));

        // Re-registering the previously response-occupied key with a handler now succeeds —
        // proves _commandTypeResponses was actually cleared, not just shadowed.
        sut.SetupHandler("A", (_, _) => Task.FromResult(DomainResult.NoOp()));
    }

    private sealed record TestEvent : IEventPayload;
}
