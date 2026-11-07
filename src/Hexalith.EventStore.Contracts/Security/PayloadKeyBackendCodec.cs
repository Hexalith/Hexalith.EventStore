// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// On-premises authority: 835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180; approved supplement sections 2, 4-5.
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Validates the closed neutral wire grammar before consuming output; encoded buffers belong to the caller.</summary>
public static class PayloadKeyBackendCodec
{
    /// <summary>The maximum decoded UTF-8 JSON request or response size.</summary>
    public const int MaximumMessageBytes = 16 * 1024;
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] RequestFields = ["protocolVersion", "operation", "operationId", "requestId", "installationId", "profileSha256"];
    private static readonly string[] ResponseFields = [.. RequestFields, "requestSha256", "outcome"];
    private static readonly string[] OptionalRequestFields = ["providerKeyVersionId", "bytes"];
    private static readonly string[] OptionalResponseFields = [.. OptionalRequestFields, "profile", "retryAfterMilliseconds"];
    private static readonly string[] ProfileFields = ["keyType", "wrapAlgorithm", "exportable", "allowPlaintextBackup", "imported", "derived", "convergent", "currentVersionId", "supportedOperations"];

    /// <summary>Validates and encodes a request with source-generated metadata; the caller must clear the result.</summary>
    /// <param name="request">The exact owned request.</param>
    /// <returns>The exact bounded encoded request bytes.</returns>
    public static byte[] EncodeRequest(PayloadKeyBackendRequest request)
    {
        ValidateRequest(request);
        return Encode(request, PayloadKeyBackendJsonContext.Default.PayloadKeyBackendRequest);
    }

    /// <summary>Validates and encodes a response; the caller must clear the result.</summary>
    /// <param name="response">The exact owned response.</param>
    /// <returns>The exact bounded encoded response bytes.</returns>
    public static byte[] EncodeResponse(PayloadKeyBackendResponse response)
    {
        ValidateResponse(response);
        return Encode(response, PayloadKeyBackendJsonContext.Default.PayloadKeyBackendResponse);
    }

    /// <summary>Rejects unknown, missing and duplicate fields before deserializing a bounded owned request.</summary>
    /// <param name="bytes">The exact encoded request, owned by the caller.</param>
    /// <returns>A request owning its decoded byte array.</returns>
    public static PayloadKeyBackendRequest DecodeRequest(ReadOnlySpan<byte> bytes)
    {
        PayloadKeyBackendRequest? request = null;
        try
        {
            ValidateShape(bytes, response: false);
            request = JsonSerializer.Deserialize(bytes, PayloadKeyBackendJsonContext.Default.PayloadKeyBackendRequest) ?? throw Invalid();
            ValidateRequest(request);
            return request;
        }
        catch
        {
            request?.Dispose();
            throw Invalid();
        }
    }

    /// <summary>Rejects unknown, missing and duplicate fields before deserializing a bounded owned response.</summary>
    /// <param name="bytes">The exact encoded response, owned by the caller.</param>
    /// <returns>A response owning its decoded byte array.</returns>
    public static PayloadKeyBackendResponse DecodeResponse(ReadOnlySpan<byte> bytes)
    {
        PayloadKeyBackendResponse? response = null;
        try
        {
            ValidateShape(bytes, response: true);
            response = JsonSerializer.Deserialize(bytes, PayloadKeyBackendJsonContext.Default.PayloadKeyBackendResponse) ?? throw Invalid();
            ValidateResponse(response);
            return response;
        }
        catch
        {
            response?.Dispose();
            throw Invalid();
        }
    }

    /// <summary>Requires the response to bind every request identity and SHA-256 of its exact wire bytes.</summary>
    /// <param name="request">The original request.</param>
    /// <param name="encodedRequest">The exact originally submitted bytes.</param>
    /// <param name="response">The validated response.</param>
    public static void ValidateBinding(PayloadKeyBackendRequest request, ReadOnlySpan<byte> encodedRequest, PayloadKeyBackendResponse response)
    {
        ValidateRequest(request);
        ValidateResponse(response);
        if (response.ProtocolVersion != request.ProtocolVersion || response.Operation != request.Operation
            || response.OperationId != request.OperationId || response.RequestId != request.RequestId
            || response.InstallationId != request.InstallationId || response.ProfileSha256 != request.ProfileSha256
            || response.RequestSha256 != Convert.ToHexStringLower(SHA256.HashData(encodedRequest))
            || (response.Outcome == PayloadKeyBackendOutcome.Succeeded && request.ProviderKeyVersionId is not null
                && response.ProviderKeyVersionId != request.ProviderKeyVersionId)) { throw Invalid(); }
    }

    /// <summary>Validates the closed software key profile. This does not authenticate or qualify the installation.</summary>
    /// <param name="profile">A fresh authenticated profile observation.</param>
    public static void ValidateProfile(PayloadKeyBackendProfile profile)
    {
        if (profile is null || profile.KeyType != "rsa-3072" || profile.WrapAlgorithm != "RSA-OAEP-256"
            || profile.Exportable || profile.AllowPlaintextBackup || profile.Imported || profile.Derived || profile.Convergent
            || profile.SupportedOperations is not { Length: 5 }
            || !profile.SupportedOperations.Order().SequenceEqual(Enum.GetValues<PayloadKeyBackendOperation>())) { throw Invalid(); }
        ValidateVersion(profile.CurrentVersionId);
    }

    /// <summary>Validates one request without taking ownership of its bytes.</summary>
    /// <param name="request">The request to validate.</param>
    public static void ValidateRequest(PayloadKeyBackendRequest request)
    {
        if (request is null) { throw Invalid(); }
        ValidateCommon(request.ProtocolVersion, request.Operation, request.OperationId, request.RequestId, request.InstallationId, request.ProfileSha256);
        if (request.Operation is PayloadKeyBackendOperation.Profile or PayloadKeyBackendOperation.Lookup)
        {
            if (request.ProviderKeyVersionId is not null || request.Bytes is not null) { throw Invalid(); }
        }
        else
        {
            ValidateVersion(request.ProviderKeyVersionId);
            int length = request.Operation == PayloadKeyBackendOperation.Unwrap ? 384 : 32;
            if (request.Bytes?.Length != length) { throw Invalid(); }
        }
    }

    private static void ValidateResponse(PayloadKeyBackendResponse response)
    {
        if (response is null) { throw Invalid(); }
        ValidateCommon(response.ProtocolVersion, response.Operation, response.OperationId, response.RequestId, response.InstallationId, response.ProfileSha256);
        if (!Digest(response.RequestSha256) || !Enum.IsDefined(response.Outcome)
            || response.RetryAfterMilliseconds is < 0 or > 2000
            || (response.RetryAfterMilliseconds is not null && response.Outcome != PayloadKeyBackendOutcome.Unavailable)) { throw Invalid(); }
        if (response.Outcome != PayloadKeyBackendOutcome.Succeeded)
        {
            if (response.Bytes is not null || response.Profile is not null || response.ProviderKeyVersionId is not null) { throw Invalid(); }
        }
        else if (response.Operation == PayloadKeyBackendOperation.Profile)
        {
            if (response.Profile is null || response.Bytes is not null || response.ProviderKeyVersionId is not null) { throw Invalid(); }
            ValidateProfile(response.Profile);
        }
        else if (response.Operation == PayloadKeyBackendOperation.Lookup)
        {
            if (response.Bytes is not null || response.Profile is not null || response.ProviderKeyVersionId is not null) { throw Invalid(); }
        }
        else
        {
            ValidateVersion(response.ProviderKeyVersionId);
            if (response.Profile is not null || response.Bytes?.Length != (response.Operation == PayloadKeyBackendOperation.Unwrap ? 32 : 384)) { throw Invalid(); }
        }
    }

    private static void ValidateCommon(int version, PayloadKeyBackendOperation operation, string operationId, string requestId, string installation, string profile)
    {
        if (version != 1 || !Enum.IsDefined(operation) || !Ulid(operationId) || !Ulid(requestId)
            || installation is not { Length: >= 1 and <= 128 }
            || installation.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-')) || !Digest(profile)) { throw Invalid(); }
    }

    private static void ValidateVersion(string? value)
    {
        try
        {
            if (value is null || value.Length is < 1 or > 1024 || !value.IsNormalized(NormalizationForm.FormC)
                || value.Any(c => c < 32 || c == 127) || StrictUtf8.GetByteCount(value) > 1024) { throw Invalid(); }
        }
        catch { throw Invalid(); }
    }

    private static bool Ulid(string? value) => value is { Length: 26 } && value[0] <= '7' && value.All(Alphabet.Contains);
    private static bool Digest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static PayloadKeyBackendProtocolException Invalid() => new();

    private static byte[] Encode<T>(T value, JsonTypeInfo<T> metadata)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, metadata);
        if (bytes.Length > MaximumMessageBytes)
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw Invalid();
        }
        return bytes;
    }

    private static void ValidateShape(ReadOnlySpan<byte> bytes, bool response)
    {
        if (bytes.Length is < 2 or > MaximumMessageBytes) { throw Invalid(); }
        _ = StrictUtf8.GetCharCount(bytes);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 4 });
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) { throw Invalid(); }
        ValidateObject(ref reader, response ? ResponseFields : RequestFields, response ? OptionalResponseFields : OptionalRequestFields);
        if (reader.Read()) { throw Invalid(); }
    }

    private static void ValidateObject(ref Utf8JsonReader reader, string[] required, string[] optional)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) { throw Invalid(); }
            string name = reader.GetString() ?? throw Invalid();
            if (!seen.Add(name) || (!required.Contains(name, StringComparer.Ordinal) && !optional.Contains(name, StringComparer.Ordinal)) || !reader.Read()) { throw Invalid(); }
            if (name == "profile")
            {
                if (reader.TokenType != JsonTokenType.StartObject) { throw Invalid(); }
                ValidateObject(ref reader, ProfileFields, []);
            }
            else if (name == "supportedOperations")
            {
                if (reader.TokenType != JsonTokenType.StartArray) { throw Invalid(); }
                int count = 0;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    ValidateEnum<PayloadKeyBackendOperation>(ref reader);
                    if (++count > 5) { throw Invalid(); }
                }
                if (reader.TokenType != JsonTokenType.EndArray) { throw Invalid(); }
            }
            else if (name == "operation") { ValidateEnum<PayloadKeyBackendOperation>(ref reader); }
            else if (name == "outcome") { ValidateEnum<PayloadKeyBackendOutcome>(ref reader); }
            else if (name is "protocolVersion" or "retryAfterMilliseconds")
            {
                if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out _)) { throw Invalid(); }
            }
            else if (name is "exportable" or "allowPlaintextBackup" or "imported" or "derived" or "convergent")
            {
                if (reader.TokenType is not (JsonTokenType.True or JsonTokenType.False)) { throw Invalid(); }
            }
            else if (name == "bytes") { ValidateBytes(ref reader); }
            else if (reader.TokenType != JsonTokenType.String) { throw Invalid(); }
        }
        if (reader.TokenType != JsonTokenType.EndObject || required.Any(field => !seen.Contains(field))) { throw Invalid(); }
    }

    private static void ValidateBytes(ref Utf8JsonReader reader)
    {
        // Validate all byte spellings before Deserialize can allocate an owner that a later scalar error would strand.
        if (reader.TokenType != JsonTokenType.String || reader.ValueIsEscaped || reader.ValueSpan.Length > 512
            || !reader.TryGetBytesFromBase64(out byte[]? bytes)) { throw Invalid(); }
        Span<byte> canonical = stackalloc byte[512];
        try
        {
            if (bytes.Length is not (32 or 384)) { throw Invalid(); }
            Base64.EncodeToUtf8(bytes, canonical, out _, out int written);
            if (!reader.ValueSpan.SequenceEqual(canonical[..written])) { throw Invalid(); }
        }
        finally { CryptographicOperations.ZeroMemory(bytes); CryptographicOperations.ZeroMemory(canonical); }
    }

    private static void ValidateEnum<T>(ref Utf8JsonReader reader) where T : struct, Enum
    {
        if (reader.TokenType != JsonTokenType.String || reader.GetString() is not string value
            || !Enum.TryParse(value, ignoreCase: false, out T parsed) || !Enum.IsDefined(parsed) || parsed.ToString() != value) { throw Invalid(); }
    }
}
