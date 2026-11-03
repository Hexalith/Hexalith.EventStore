using System.Text.Json;

namespace Hexalith.EventStore.Testing.CatalogInvocation;

/// <summary>One bounded private request; transport coordinates and catalog bindings are never caller inputs.</summary>
/// <param name="Id">One-shot random request identity.</param>
/// <param name="ExpiresAt">Absolute caller deadline.</param>
/// <param name="AppId">An explicitly selected owner.</param>
/// <param name="Method">One of the three finite SDK callback paths.</param>
/// <param name="Credential">A finite positive or negative authentication control.</param>
/// <param name="Token">Only an explicit negative human/public-issuer credential.</param>
/// <param name="Payload">The original callback body.</param>
public sealed record CatalogInvocationRequest(string Id, DateTimeOffset ExpiresAt, string AppId,
    string Method, CatalogInvocationCredential Credential, string? Token, JsonElement Payload);
