
using System.Collections.Concurrent;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// Fake implementation of <see cref="IDomainServiceInvoker"/> for unit testing.
/// Allows configuring canned responses for specific command types or tenant+domain combinations.
/// Tracks all invocations for test assertions.
/// </summary>
public sealed class FakeDomainServiceInvoker : IDomainServiceInvoker {
    private readonly ConcurrentQueue<(CommandEnvelope Command, object? CurrentState)> _invocations = new();
    private readonly ConcurrentDictionary<string, DomainResult> _commandTypeResponses = new();
    private readonly ConcurrentDictionary<string, Func<CommandEnvelope, object?, Task<DomainResult>>> _commandTypeHandlers = new();
    private readonly ConcurrentDictionary<string, DomainResult> _tenantDomainResponses = new();
    private DomainResult? _defaultResponse;

    /// <summary>Gets the list of all commands that were passed to <see cref="InvokeAsync"/>.</summary>
    public IReadOnlyList<CommandEnvelope> Invocations => [.. _invocations.Select(i => i.Command)];

    /// <summary>Gets the list of all (command, currentState) pairs passed to <see cref="InvokeAsync"/>.
    /// Use this to inspect the exact state the AggregateActor passes to the domain service.</summary>
    public IReadOnlyList<(CommandEnvelope Command, object? CurrentState)> InvocationsWithState => [.. _invocations];

    /// <summary>
    /// Configures a canned response for a specific command type.
    /// </summary>
    /// <remarks>
    /// Per ADR R1A7-01, this surface and <see cref="SetupHandler"/> are mutually exclusive per
    /// <paramref name="commandType"/>: registering a static response for a command type that already
    /// has a handler — or vice versa — throws <see cref="InvalidOperationException"/>.
    /// </remarks>
    /// <param name="commandType">The command type to match.</param>
    /// <param name="result">The result to return.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A handler is already registered for <paramref name="commandType"/> via <see cref="SetupHandler"/>.</exception>
    public void SetupResponse(string commandType, DomainResult result) {
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentNullException.ThrowIfNull(result);
        if (_commandTypeHandlers.ContainsKey(commandType)) {
            throw new InvalidOperationException(
                $"A handler is already registered for command type '{commandType}'. " +
                $"SetupResponse and SetupHandler are mutually exclusive per command type — clear one before registering the other.");
        }

        _commandTypeResponses[commandType] = result;
    }

    /// <summary>
    /// Configures a delegating handler for a specific command type. Unlike <see cref="SetupResponse(string, DomainResult)"/>,
    /// the handler computes its <see cref="DomainResult"/> from the live <c>(command, currentState)</c> pair —
    /// enabling tests to drive the actor pipeline with a real aggregate (e.g.
    /// <c>(cmd, state) =&gt; new CounterAggregate().ProcessAsync(cmd, state)</c>).
    /// </summary>
    /// <remarks>
    /// Per ADR R1A7-01, this surface and <see cref="SetupResponse(string, DomainResult)"/> are mutually
    /// exclusive per <paramref name="commandType"/>. Registering a handler for a command type that already
    /// has a static response — or vice versa — throws <see cref="InvalidOperationException"/>. Use
    /// <see cref="ClearAll"/> to reset both surfaces between test classes.
    /// </remarks>
    /// <param name="commandType">The command type to match.</param>
    /// <param name="handler">The asynchronous handler invoked with the command and the current state passed to <see cref="InvokeAsync"/>.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A static response is already registered for <paramref name="commandType"/> via <see cref="SetupResponse(string, DomainResult)"/>.</exception>
    public void SetupHandler(string commandType, Func<CommandEnvelope, object?, Task<DomainResult>> handler) {
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentNullException.ThrowIfNull(handler);
        if (_commandTypeResponses.ContainsKey(commandType)) {
            throw new InvalidOperationException(
                $"A static SetupResponse is already registered for command type '{commandType}'. " +
                $"SetupResponse and SetupHandler are mutually exclusive per command type — clear one before registering the other.");
        }

        _commandTypeHandlers[commandType] = handler;
    }

    /// <summary>
    /// Configures a canned response for a specific tenant and domain combination.
    /// </summary>
    /// <param name="tenantId">The tenant identifier to match.</param>
    /// <param name="domain">The domain name to match.</param>
    /// <param name="result">The result to return.</param>
    public void SetupResponse(string tenantId, string domain, DomainResult result) {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(result);
        _tenantDomainResponses[$"{tenantId}:{domain}"] = result;
    }

    /// <summary>
    /// Configures a default response for any command that does not match a specific setup.
    /// </summary>
    /// <param name="result">The default result to return.</param>
    public void SetupDefaultResponse(DomainResult result) {
        ArgumentNullException.ThrowIfNull(result);
        _defaultResponse = result;
    }

    /// <summary>
    /// Resets every registry to its initial empty state: per-command-type responses, per-command-type
    /// handlers, per-tenant+domain responses, the default response, AND the captured invocations queue.
    /// </summary>
    /// <remarks>
    /// Required by ADR R1A7-01 because <see cref="DaprTestContainerFixture"/> shares one
    /// <see cref="FakeDomainServiceInvoker"/> instance across the entire <c>[Collection("DaprTestContainer")]</c>
    /// suite. Test classes that register handlers must call <see cref="ClearAll"/> in their constructor
    /// AND on disposal so sibling test classes see a clean fixture.
    /// </remarks>
    public void ClearAll() {
        _commandTypeResponses.Clear();
        _commandTypeHandlers.Clear();
        _tenantDomainResponses.Clear();
        _invocations.Clear();
        _defaultResponse = null;
    }

    /// <inheritdoc/>
    public async Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        _invocations.Enqueue((command, currentState));

        if (_commandTypeHandlers.TryGetValue(command.CommandType, out Func<CommandEnvelope, object?, Task<DomainResult>>? handler)) {
            return await handler(command, currentState).ConfigureAwait(false);
        }

        if (_commandTypeResponses.TryGetValue(command.CommandType, out DomainResult? commandTypeResult)) {
            return commandTypeResult;
        }

        string tenantDomainKey = $"{command.TenantId}:{command.Domain}";
        if (_tenantDomainResponses.TryGetValue(tenantDomainKey, out DomainResult? tenantDomainResult)) {
            return tenantDomainResult;
        }

        if (_defaultResponse is not null) {
            return _defaultResponse;
        }

        throw new InvalidOperationException(
            $"No response configured for command type '{command.CommandType}'. " +
            $"Use SetupResponse or SetupDefaultResponse to configure a response.");
    }
}
