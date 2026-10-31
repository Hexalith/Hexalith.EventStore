using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Typed compiled owner input for the model-only composition proof.</summary>
public sealed record DevelopmentCatalogProbeCommand(string AggregateId) : ICommandContract
{
    /// <summary>The owner's declared operation discriminator.</summary>
    public static string CommandType => "record-probe";
    /// <summary>The owner's declared domain.</summary>
    public static string Domain => "catalog-probe";
}
