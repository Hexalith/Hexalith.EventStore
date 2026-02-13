namespace Hexalith.EventStore.Testing.Fakes;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;

/// <summary>
/// Fake implementation of <see cref="IDomainServiceInvoker"/> for unit testing.
/// Allows configuring canned responses for specific command types or tenant+domain combinations.
/// Tracks all invocations for test assertions.
/// </summary>
public sealed class FakeDomainServiceInvoker : IDomainServiceInvoker
{
    private readonly List<CommandEnvelope> _invocations = [];
    private readonly Dictionary<string, DomainResult> _commandTypeResponses = [];
    private readonly Dictionary<string, DomainResult> _tenantDomainResponses = [];
    private DomainResult? _defaultResponse;

    /// <summary>Gets the list of all commands that were passed to <see cref="InvokeAsync"/>.</summary>
    public IReadOnlyList<CommandEnvelope> Invocations => _invocations;

    /// <summary>
    /// Configures a canned response for a specific command type.
    /// </summary>
    /// <param name="commandType">The command type to match.</param>
    /// <param name="result">The result to return.</param>
    public void SetupResponse(string commandType, DomainResult result)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        ArgumentNullException.ThrowIfNull(result);
        _commandTypeResponses[commandType] = result;
    }

    /// <summary>
    /// Configures a canned response for a specific tenant and domain combination.
    /// </summary>
    /// <param name="tenantId">The tenant identifier to match.</param>
    /// <param name="domain">The domain name to match.</param>
    /// <param name="result">The result to return.</param>
    public void SetupResponse(string tenantId, string domain, DomainResult result)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(result);
        _tenantDomainResponses[$"{tenantId}:{domain}"] = result;
    }

    /// <summary>
    /// Configures a default response for any command that does not match a specific setup.
    /// </summary>
    /// <param name="result">The default result to return.</param>
    public void SetupDefaultResponse(DomainResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _defaultResponse = result;
    }

    /// <inheritdoc/>
    public Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState)
    {
        ArgumentNullException.ThrowIfNull(command);
        _invocations.Add(command);

        if (_commandTypeResponses.TryGetValue(command.CommandType, out DomainResult? commandTypeResult))
        {
            return Task.FromResult(commandTypeResult);
        }

        string tenantDomainKey = $"{command.AggregateIdentity.TenantId}:{command.AggregateIdentity.Domain}";
        if (_tenantDomainResponses.TryGetValue(tenantDomainKey, out DomainResult? tenantDomainResult))
        {
            return Task.FromResult(tenantDomainResult);
        }

        if (_defaultResponse is not null)
        {
            return Task.FromResult(_defaultResponse);
        }

        throw new InvalidOperationException(
            $"No response configured for command type '{command.CommandType}'. " +
            $"Use SetupResponse or SetupDefaultResponse to configure a response.");
    }
}
