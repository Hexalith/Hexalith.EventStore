using System.Text;

using Hexalith.EventStore.Admin.Abstractions.Security;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Security;

public class UnsafeMarkerDetectionTests
{
    [Theory]
    [InlineData("https://example.test/callback?access%5Ftoken=secret-value")]
    [InlineData("https://example.test/callback?%61ccess_token=secret-value")]
    [InlineData("https://example.test/download?sig=secret-value")]
    [InlineData("(Bearer " + "secret-token)")]
    [InlineData("[Bearer secret-token]")]
    [InlineData("eyJhbGciOiJub25lIn0.cGF5bG9hZA.")]
    [InlineData("Bearer%20secret-token")]
    [InlineData("eyJhbGciOiJub25lIn0%2EcGF5bG9hZA%2Esig")]
    [InlineData("?password%00=secret")]
    [InlineData("%25253Fpassword%25253Dsecret-value")]
    public void ContainsUnsafeMarker_DetectsCredentialShapes(string value)
    {
        UnsafeMarkerDetection.ContainsUnsafeMarker(value).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(QuerySecretAliases))]
    public void ContainsUnsafeMarker_DetectsPercentEncodedSeparatorsAndDelimiters(string alias)
    {
        UnsafeMarkerDetection.ContainsUnsafeMarker($"https://example.test/health%3F{alias}%3Dsecret-value").ShouldBeTrue();
        UnsafeMarkerDetection.ContainsUnsafeMarker($"https://example.test/health?next=1%26{alias}%3Dsecret-value").ShouldBeTrue();
        UnsafeMarkerDetection.ContainsUnsafeMarker($"%253F{alias}%253Dsecret-value").ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(QuerySecretAliases))]
    public void ContainsUnsafeMarker_DetectsDoubleEncodedCredentialNames(string alias)
    {
        ArgumentNullException.ThrowIfNull(alias);
        string doubleEncodedName = "%25" + ((int)alias[0]).ToString("X2") + alias[1..];
        UnsafeMarkerDetection.ContainsUnsafeMarker($"https://example.test/health?{doubleEncodedName}=secret-value").ShouldBeTrue();
    }

    [Fact]
    public void ContainsUnsafeMarker_FailsClosedWhenPercentDecodingStillChangesAtPassLimit()
    {
        string encodedText = "?password" + "=secret-value";
        for (int pass = 0; pass < 9; pass++)
        {
            encodedText = Uri.EscapeDataString(encodedText);
        }

        UnsafeMarkerDetection.ContainsUnsafeMarker(encodedText).ShouldBeTrue();
    }

    [Fact]
    public void ContainsUnsafeMarker_DetectsFullyEncodedUserInfoUri()
    {
        const string encodedUri = "https%3A%2F%2Foperator%3Apassword%40example.test%2Fhealth";

        UnsafeMarkerDetection.ContainsUnsafeMarker(encodedUri).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(ValuelessQuerySecretAliases))]
    public void ContainsUnsafeMarker_IgnoresSecretKeysWithNoValue(string alias)
    {
        UnsafeMarkerDetection.ContainsUnsafeMarker($"https://example.test/health?{alias}=").ShouldBeFalse();
        UnsafeMarkerDetection.ContainsUnsafeMarker($"https://example.test/health?{alias}%3D").ShouldBeFalse();
        UnsafeMarkerDetection.ContainsUnsafeMarker($"{alias}:").ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(JsonSecretNames))]
    public void ContainsUnsafeMarker_DetectsUnicodeEscapedJsonSecretNames(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string escaped = "\\u00" + ((int)name[0]).ToString("x2") + name[1..];
        UnsafeMarkerDetection.ContainsUnsafeMarker($"{{\"{escaped}\":\"secret-value\"}}").ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(JsonSecretNames))]
    public void ContainsUnsafeMarker_DetectsJsonSecretsWithUnicodeEscapedQuotationMarks(string name)
    {
        UnsafeMarkerDetection.ContainsUnsafeMarker($"{{\\u0022{name}\\u0022:\\u0022secret-value\\u0022}}").ShouldBeTrue();
    }

    [Fact]
    public void ContainsUnsafeMarker_DetectsFormEncodedBearerCredential()
    {
        UnsafeMarkerDetection.ContainsUnsafeMarker("message=Bearer+secret-token").ShouldBeTrue();
    }

    [Fact]
    public void ContainsUnsafeMarker_DetectsCompactJwtWithTypAndNoStringAlgorithm()
    {
        string token = CompactToken("{\"typ\":\"JWT\"}", "{\"sub\":\"admin\"}");
        string nullAlgorithm = CompactToken("{\"alg\":null,\"typ\":\"JWT\"}", "{\"sub\":\"admin\"}");

        UnsafeMarkerDetection.ContainsUnsafeMarker(token).ShouldBeTrue();
        UnsafeMarkerDetection.ContainsUnsafeMarker(nullAlgorithm).ShouldBeTrue();
    }

    [Fact]
    public void ContainsUnsafeMarker_IgnoresCompactTokenWithoutAlgOrTyp()
    {
        string token = CompactToken("{\"kid\":\"abc\"}", "{\"sub\":\"admin\"}");

        UnsafeMarkerDetection.ContainsUnsafeMarker(token).ShouldBeFalse();
        UnsafeMarkerDetection.ContainsUnsafeMarker("Hexalith.EventStore.Administration").ShouldBeFalse();
    }

    public static TheoryData<string> QuerySecretAliases => new()
    {
        "access_token",
        "accessToken",
        "refresh_token",
        "refreshToken",
        "id_token",
        "idToken",
        "token",
        "password",
        "secret",
        "client_secret",
        "clientSecret",
        "api_key",
        "apiKey",
        "authorization",
        "sig",
    };

    public static TheoryData<string> ValuelessQuerySecretAliases => new()
    {
        "access_token",
        "accessToken",
        "refresh_token",
        "refreshToken",
        "id_token",
        "idToken",
        "token",
        "secret",
        "client_secret",
        "clientSecret",
        "api_key",
        "apiKey",
        "authorization",
        "sig",
    };

    public static TheoryData<string> JsonSecretNames => new()
    {
        "access_token",
        "accessToken",
        "refresh_token",
        "refreshToken",
        "id_token",
        "idToken",
        "token",
        "password",
        "secret",
        "client_secret",
        "clientSecret",
        "api_key",
        "apiKey",
        "authorization",
    };

    private static string CompactToken(string headerJson, string payloadJson)
        => $"{Base64Url(headerJson)}.{Base64Url(payloadJson)}.sig";

    private static string Base64Url(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
