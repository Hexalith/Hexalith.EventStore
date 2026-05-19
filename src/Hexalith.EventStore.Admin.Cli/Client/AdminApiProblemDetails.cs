namespace Hexalith.EventStore.Admin.Cli.Client;

/// <summary>
/// Safe subset of RFC 7807 ProblemDetails preserved for CLI output and automation.
/// </summary>
/// <param name="Type">Problem type URI.</param>
/// <param name="Title">Safe problem title.</param>
/// <param name="Status">HTTP status code.</param>
/// <param name="Detail">Safe detail text, when available.</param>
/// <param name="Extensions">Allow-listed scalar extension values.</param>
public sealed record AdminApiProblemDetails(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    IReadOnlyDictionary<string, string> Extensions);
