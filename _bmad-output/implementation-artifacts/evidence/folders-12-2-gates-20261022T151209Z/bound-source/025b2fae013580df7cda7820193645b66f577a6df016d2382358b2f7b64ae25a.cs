using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.TestUtilities;

/// <summary>
/// A Keycloak-shaped client-credentials token endpoint: it reads the requested audience and operation scopes and
/// mints an (unsigned) access token for exactly that pair, or for a deliberately broader or different pair.
/// </summary>
/// <param name="shape">The token shape: <c>exact</c> or one of the deviation shapes.</param>
internal sealed class ScopedWorkloadTokenEndpoint(string shape = "exact") : HttpMessageHandler
{
    /// <summary>Gets the authority issuer written into minted tokens.</summary>
    public const string Authority = "https://identity.example.test/realms/hexalith";

    private static readonly string[] KnownOperations =
    [
        EventStoreWorkloadOperations.DomainServiceProcess,
        EventStoreWorkloadOperations.DomainServiceReplayState,
        EventStoreWorkloadOperations.DomainServiceQuery,
        EventStoreWorkloadOperations.DomainServiceProject,
        EventStoreWorkloadOperations.DomainServiceMetadata,
        EventStoreWorkloadOperations.TrustedEffect,
    ];

    private readonly List<string> _scopes = [];
    private readonly List<string> _bodies = [];
    private readonly List<Uri> _uris = [];

    /// <summary>Gets the clock that stamps minted tokens; defaults to the system clock.</summary>
    public Func<DateTimeOffset> Clock { get; init; } = static () => DateTimeOffset.UtcNow;

    /// <summary>Gets the lifetime written into each minted token's <c>exp</c>.</summary>
    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets the <c>expires_in</c> value of the token response: a number, a string, or anything else.</summary>
    public object ExpiresIn { get; init; } = 300;

    /// <summary>Gets the requested scope parameters, in request order.</summary>
    public IReadOnlyList<string> Scopes => _scopes;

    /// <summary>Gets the raw form bodies, in request order.</summary>
    public IReadOnlyList<string> Bodies => _bodies;

    /// <summary>Gets the request URIs, in request order.</summary>
    public IReadOnlyList<Uri> Uris => _uris;

    /// <summary>
    /// Asserts that an assertion names exactly one audience and one operation, and EventStore as its caller.
    /// </summary>
    /// <param name="assertion">The assertion.</param>
    /// <param name="audience">The expected audience.</param>
    /// <param name="operation">The expected operation.</param>
    /// <param name="caller">The expected caller.</param>
    public static void AssertExactlyScoped(string? assertion, string audience, string operation, string caller = "eventstore")
    {
        assertion.ShouldNotBeNull();
        var token = new JsonWebToken(assertion);
        token.Audiences.ShouldBe([audience]);
        token.Claims.Where(static claim => claim.Type == EventStoreWorkloadAuthenticationDefaults.OperationClaimType)
            .Select(static claim => claim.Value)
            .ShouldBe([operation]);
        token.GetClaim("azp").Value.ShouldBe(caller);
    }

    /// <summary>
    /// Creates an unsigned token with the given audiences and operations.
    /// </summary>
    /// <param name="audiences">The audiences.</param>
    /// <param name="operations">The operation claim: a string, an array, or <see langword="null"/>.</param>
    /// <param name="caller">The <c>azp</c> caller.</param>
    /// <returns>The serialized token.</returns>
    public static string UnsignedToken(IReadOnlyCollection<string> audiences, object? operations, string caller = "eventstore")
        => UnsignedToken(audiences, operations, DateTime.UtcNow, TimeSpan.FromMinutes(5), caller);

    /// <summary>
    /// Creates an unsigned token with the given audiences, operations, issue time, and lifetime.
    /// </summary>
    /// <param name="audiences">The audiences.</param>
    /// <param name="operations">The operation claim: a string, an array, or <see langword="null"/>.</param>
    /// <param name="issuedAt">The issue time.</param>
    /// <param name="lifetime">The lifetime from issue to expiry.</param>
    /// <param name="caller">The <c>azp</c> caller.</param>
    /// <returns>The serialized token.</returns>
    public static string UnsignedToken(
        IReadOnlyCollection<string> audiences,
        object? operations,
        DateTime issuedAt,
        TimeSpan lifetime,
        string caller = "eventstore")
    {
        ArgumentNullException.ThrowIfNull(audiences);
        DateTime now = issuedAt;
        var claims = new Dictionary<string, object>(StringComparer.Ordinal) { ["azp"] = caller };
        if (audiences.Count > 0)
        {
            claims["aud"] = audiences.Count == 1 ? audiences.Single() : audiences.ToArray();
        }

        if (operations is not null)
        {
            claims[EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = operations;
        }

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Authority,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(lifetime),
            Claims = claims,
        });
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> form = body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(static pair => pair.Split('=', 2))
            .ToDictionary(
                static pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
                static pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : string.Empty,
                StringComparer.Ordinal);
        string scope = form.GetValueOrDefault("scope") ?? string.Empty;
        lock (_scopes)
        {
            _scopes.Add(scope);
            _bodies.Add(body);
            _uris.Add(request.RequestUri!);
        }

        string[] parts = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string audience = parts.Single(static part => part.StartsWith(WorkloadAssertionIssuerOptions.DefaultAudienceScopePrefix, StringComparison.Ordinal))
            [WorkloadAssertionIssuerOptions.DefaultAudienceScopePrefix.Length..];
        string operationScope = parts.Single(static part => part.StartsWith(WorkloadAssertionIssuerOptions.DefaultOperationScopePrefix, StringComparison.Ordinal))
            [WorkloadAssertionIssuerOptions.DefaultOperationScopePrefix.Length..];
        string operation = KnownOperations.Single(candidate => candidate.Replace(':', '.') == operationScope);
        string minted = shape switch
        {
            "extra-audience" => UnsignedToken([audience, audience == "tenants" ? "sample" : "tenants"], operation),
            "extra-operation" => UnsignedToken([audience], new[] { operation, operation == EventStoreWorkloadOperations.DomainServiceQuery ? EventStoreWorkloadOperations.DomainServiceProcess : EventStoreWorkloadOperations.DomainServiceQuery }),
            "all-operations" => UnsignedToken([audience], string.Join(' ', KnownOperations)),
            "wrong-audience" => UnsignedToken([audience == "tenants" ? "sample" : "tenants"], operation),
            "wrong-operation" => UnsignedToken([audience], operation == EventStoreWorkloadOperations.DomainServiceMetadata ? EventStoreWorkloadOperations.DomainServiceProcess : EventStoreWorkloadOperations.DomainServiceMetadata),
            "no-audience" => UnsignedToken([], operation),
            "no-operation" => UnsignedToken([audience], null),
            "opaque" => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)),
            _ => UnsignedToken([audience], operation, Clock().UtcDateTime, TokenLifetime),
        };
        string json = JsonSerializer.Serialize(new Dictionary<string, object> { ["access_token"] = minted, ["expires_in"] = ExpiresIn });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
