using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Hexalith.EventStore.Aspire;

namespace Hexalith.EventStore.AppHost;

/// <summary>
/// Adds a token-only local smoke command whose signing material remains owned by the AppHost.
/// </summary>
internal static class LocalAuthenticationTokenCommand
{
    internal const string CommandName = "issue-smoke-token";

    /// <summary>
    /// Adds the local smoke-token command to the protected sample API resource.
    /// </summary>
    /// <param name="resource">The sample API resource that owns the command.</param>
    /// <param name="signingKey">The per-run secret parameter also consumed by validators.</param>
    /// <param name="subject">The per-run sample identity parameter.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<ProjectResource> WithLocalAuthenticationTokenCommand(
        this IResourceBuilder<ProjectResource> resource,
        IResourceBuilder<ParameterResource> signingKey,
        IResourceBuilder<ParameterResource> subject)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(signingKey);
        ArgumentNullException.ThrowIfNull(subject);

        return resource.WithCommand(
            CommandName,
            "Issue local smoke token",
            async context =>
            {
                string resolvedSigningKey = await signingKey.Resource
                    .GetValueAsync(context.CancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new InvalidOperationException(
                        "The AppHost local signing material is unavailable.");
                string resolvedSubject = await subject.Resource
                    .GetValueAsync(context.CancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new InvalidOperationException(
                        "The AppHost local smoke identity is unavailable.");
                string token = CreateToken(resolvedSigningKey, resolvedSubject, DateTimeOffset.UtcNow);
                return CommandResults.Success(
                    string.Empty,
                    token,
                    CommandResultFormat.Text,
                    displayImmediately: true);
            },
            new CommandOptions
            {
                Description = "Returns one short-lived bearer token without exposing AppHost signing material.",
            });
    }

    /// <summary>
    /// Creates one short-lived token for the local generated-API smoke identity.
    /// </summary>
    /// <param name="signingKey">The current AppHost run's signing material.</param>
    /// <param name="subject">The current AppHost run's sample identity.</param>
    /// <param name="now">The issuance instant.</param>
    /// <returns>A compact HS256 JWT.</returns>
    internal static string CreateToken(string signingKey, string subject, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new ArgumentException(
                "The local signing material must contain at least 32 UTF-8 bytes.",
                nameof(signingKey));
        }

        var header = new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT",
        };
        var payload = new Dictionary<string, object>
        {
            ["sub"] = subject,
            ["iss"] = "hexalith-dev",
            ["aud"] = HexalithEventStoreSecurityOptions.DefaultAudience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(15).ToUnixTimeSeconds(),
            ["tenants"] = JsonSerializer.Serialize(new[] { "tenant-a" }),
            ["domains"] = JsonSerializer.Serialize(new[] { "counter" }),
            ["permissions"] = JsonSerializer.Serialize(new[] { "command:submit", "query:read" }),
        };

        string encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        string encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        string unsignedToken = $"{encodedHeader}.{encodedPayload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        string signature = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedToken)));
        return $"{unsignedToken}.{signature}";
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
