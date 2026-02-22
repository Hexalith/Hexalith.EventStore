
using System.Text;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Testing.Builders;
/// <summary>
/// Fluent builder for creating <see cref="CommandEnvelope"/> instances with sensible defaults for testing.
/// </summary>
public sealed class CommandEnvelopeBuilder {
    private string _tenantId = "test-tenant";
    private string _domain = "test-domain";
    private string _aggregateId = "test-agg-001";
    private string _commandType = "TestCommand";
    private byte[] _payload = Encoding.UTF8.GetBytes("{}");
    private string _correlationId = Guid.NewGuid().ToString();
    private string? _causationId;
    private string _userId = "test-user";
    private Dictionary<string, string>? _extensions;

    /// <summary>Sets the tenant identifier.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>This builder instance.</returns>
    public CommandEnvelopeBuilder WithTenantId(string tenantId) { _tenantId = tenantId; return this; }

    /// <summary>Sets the domain name.</summary>
    /// <param name="domain">The domain name.</param>
    /// <returns>This builder instance.</returns>
    public CommandEnvelopeBuilder WithDomain(string domain) { _domain = domain; return this; }

    /// <summary>Sets the aggregate identifier.</summary>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <returns>This builder instance.</returns>
    public CommandEnvelopeBuilder WithAggregateId(string aggregateId) { _aggregateId = aggregateId; return this; }

    /// <summary>Sets the command type name.</summary>
    /// <param name="commandType">The command type name.</param>
    /// <returns>This builder instance.</returns>
    public CommandEnvelopeBuilder WithCommandType(string commandType) { _commandType = commandType; return this; }

    /// <summary>Sets the serialized payload.</summary>
    /// <param name="payload">The payload bytes.</param>
    /// <returns>This builder instance.</returns>
    public CommandEnvelopeBuilder WithPayload(byte[] payload) { _payload = payload; return this; }

    /// <summary>Sets the correlation identifier.</summary>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <returns>This builder instance.</returns>
    public CommandEnvelopeBuilder WithCorrelationId(string correlationId) { _correlationId = correlationId; return this; }

    /// <summary>Sets the causation identifier.</summary>
    /// <param name="causationId">The causation identifier.</param>
    /// <returns>This builder instance.</returns>
    public CommandEnvelopeBuilder WithCausationId(string? causationId) { _causationId = causationId; return this; }

    /// <summary>Sets the user identifier.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>This builder instance.</returns>
    public CommandEnvelopeBuilder WithUserId(string userId) { _userId = userId; return this; }

    /// <summary>Sets the extension metadata.</summary>
    /// <param name="extensions">The extension metadata.</param>
    /// <returns>This builder instance.</returns>
    public CommandEnvelopeBuilder WithExtensions(Dictionary<string, string>? extensions) { _extensions = extensions; return this; }

    /// <summary>Builds the <see cref="CommandEnvelope"/> instance.</summary>
    /// <returns>A new <see cref="CommandEnvelope"/> with the configured values.</returns>
    public CommandEnvelope Build() => new(
        _tenantId, _domain, _aggregateId, _commandType,
        _payload, _correlationId, _causationId, _userId, _extensions);
}
