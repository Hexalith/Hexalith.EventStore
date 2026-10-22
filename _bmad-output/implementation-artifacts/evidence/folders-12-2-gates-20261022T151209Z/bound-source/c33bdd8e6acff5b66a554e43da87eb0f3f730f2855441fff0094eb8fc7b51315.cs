namespace Hexalith.EventStore.RestApi.Generators;

internal static class RestApiMetadataNames
{
    public const string CommandContract = "Hexalith.EventStore.Contracts.Commands.ICommandContract";
    public const string QueryContract = "Hexalith.EventStore.Contracts.Queries.IQueryContract";
    public const string RestApiAttribute = "Hexalith.EventStore.Contracts.Rest.RestApiAttribute";
    public const string RestQueryBindingAttribute = "Hexalith.EventStore.Contracts.Rest.RestQueryBindingAttribute";
    public const string RestRouteAttribute = "Hexalith.EventStore.Contracts.Rest.RestRouteAttribute";
    public const string JsonPropertyNameAttribute = "System.Text.Json.Serialization.JsonPropertyNameAttribute";
}
