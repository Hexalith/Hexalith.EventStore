namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>A closed, metadata-only authorization denial from a trusted domain service.</summary>
public sealed class DomainServiceSafeDenialException : InvalidOperationException
{
    private const string Prefix = "domain_service_safe_denial_";

    public DomainServiceSafeDenialException(int statusCode)
        : base("The domain service denied authorization.")
    {
        if (statusCode is not (401 or 403 or 404 or 503))
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode));
        }

        StatusCode = statusCode;
    }

    public int StatusCode { get; }

    public string SafeResultCode => Prefix + StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static bool TryParse(string? value, out DomainServiceSafeDenialException? denial)
    {
        denial = value switch
        {
            Prefix + "401" => new(401),
            Prefix + "403" => new(403),
            Prefix + "404" => new(404),
            Prefix + "503" => new(503),
            _ => null,
        };
        return denial is not null;
    }
}
