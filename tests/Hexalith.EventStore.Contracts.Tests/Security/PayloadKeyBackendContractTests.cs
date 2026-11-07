using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Contracts.Tests.Security;

/// <summary>Approved on-premises supplement sections 2, 4-5: bounded neutral wire and owned-buffer contracts.</summary>
public sealed class PayloadKeyBackendContractTests
{
    private const string OperationId = "01J00000000000000000000001";
    private const string RequestId = "01J00000000000000000000002";
    private const string Installation = "fixture.installation_1";
    private static readonly string ProfileDigest = new('a', 64);

    [Theory]
    [InlineData(PayloadKeyBackendOperation.Profile)]
    [InlineData(PayloadKeyBackendOperation.Wrap)]
    [InlineData(PayloadKeyBackendOperation.WrapExact)]
    [InlineData(PayloadKeyBackendOperation.Unwrap)]
    [InlineData(PayloadKeyBackendOperation.Lookup)]
    public void ClosedOperationsRoundtripAndBindExactEncodedRequest(PayloadKeyBackendOperation operation)
    {
        using PayloadKeyBackendRequest request = Request(operation);
        byte[] encoded = PayloadKeyBackendCodec.EncodeRequest(request);
        using PayloadKeyBackendRequest decoded = PayloadKeyBackendCodec.DecodeRequest(encoded);
        decoded.Operation.ShouldBe(operation);
        decoded.Bytes.ShouldBe(request.Bytes);
        using PayloadKeyBackendResponse response = Response(request, encoded);
        byte[] output = PayloadKeyBackendCodec.EncodeResponse(response);
        using PayloadKeyBackendResponse read = PayloadKeyBackendCodec.DecodeResponse(output);
        PayloadKeyBackendCodec.ValidateBinding(request, encoded, read);
        encoded.Length.ShouldBeLessThanOrEqualTo(PayloadKeyBackendCodec.MaximumMessageBytes);
        output.Length.ShouldBeLessThanOrEqualTo(PayloadKeyBackendCodec.MaximumMessageBytes);
        CryptographicOperations.ZeroMemory(encoded); CryptographicOperations.ZeroMemory(output);
    }

    [Theory]
    [InlineData("{0},\"unexpected\":true}}")]
    [InlineData("{0},\"protocolVersion\":1}}")]
    [InlineData("{0},\"bytes\":null}}")]
    public void DuplicateUnknownAndUnexpectedFieldsFailClosed(string template)
    {
        ArgumentNullException.ThrowIfNull(template);
        using var request = Request(PayloadKeyBackendOperation.Profile);
        string valid = Encoding.UTF8.GetString(PayloadKeyBackendCodec.EncodeRequest(request));
        string malformed = template.Replace("{0}", valid[..^1], StringComparison.Ordinal).Replace("}}", "}", StringComparison.Ordinal);
        Should.Throw<PayloadKeyBackendProtocolException>(() => PayloadKeyBackendCodec.DecodeRequest(Encoding.UTF8.GetBytes(malformed)));
    }

    [Theory]
    [InlineData("\"Wrap\"", "0")]
    [InlineData("\"Wrap\"", "\"wrap\"")]
    [InlineData("\"Wrap\"", "\"Sign\"")]
    [InlineData("\"protocolVersion\":1", "\"protocolVersion\":2")]
    [InlineData("\"protocolVersion\":1", "\"protocolVersion\":\"1\"")]
    [InlineData(OperationId, "01j00000000000000000000001")]
    public void InvalidVersionOperationAndCanonicalIdentityAreRefused(string before, string after)
    {
        using var request = Request(PayloadKeyBackendOperation.Wrap);
        byte[] encoded = PayloadKeyBackendCodec.EncodeRequest(request);
        string malformed = Encoding.UTF8.GetString(encoded).Replace(before, after, StringComparison.Ordinal);
        Should.Throw<PayloadKeyBackendProtocolException>(() => PayloadKeyBackendCodec.DecodeRequest(Encoding.UTF8.GetBytes(malformed)));
        CryptographicOperations.ZeroMemory(encoded);
    }

    [Fact]
    public void LateScalarErrorsAreRefusedBeforeSensitiveDeserialization()
    {
        string material = Convert.ToBase64String(Enumerable.Repeat((byte)0xc7, 32).ToArray());
        string malformed = "{\"bytes\":\"" + material + "\",\"protocolVersion\":\"wrong-type\",\"operation\":\"Wrap\",\"operationId\":\"" + OperationId
            + "\",\"requestId\":\"" + RequestId + "\",\"installationId\":\"" + Installation + "\",\"profileSha256\":\"" + ProfileDigest + "\",\"providerKeyVersionId\":\"version-1\"}";
        Should.Throw<PayloadKeyBackendProtocolException>(() => PayloadKeyBackendCodec.DecodeRequest(Encoding.UTF8.GetBytes(malformed)));
        string response = malformed.Replace("\"protocolVersion\":\"wrong-type\"", "\"protocolVersion\":1", StringComparison.Ordinal)[..^1]
            + ",\"requestSha256\":\"" + new string('b', 64) + "\",\"outcome\":\"Succeeded\",\"retryAfterMilliseconds\":\"wrong-type\"}";
        Should.Throw<PayloadKeyBackendProtocolException>(() => PayloadKeyBackendCodec.DecodeResponse(Encoding.UTF8.GetBytes(response)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(384)]
    [InlineData(513)]
    public void WrapInputsHaveExactly32Bytes(int length)
    {
        using var request = Request(PayloadKeyBackendOperation.Wrap) with { Bytes = new byte[length] };
        Should.Throw<PayloadKeyBackendProtocolException>(() => PayloadKeyBackendCodec.EncodeRequest(request));
    }

    [Fact]
    public void ExactVersionUtf8BoundsAndMessageLimitAreAppliedBeforeOutputConsumption()
    {
        using var request = Request(PayloadKeyBackendOperation.Wrap) with { ProviderKeyVersionId = new string('a', 1024) };
        using var decoded = PayloadKeyBackendCodec.DecodeRequest(PayloadKeyBackendCodec.EncodeRequest(request));
        decoded.ProviderKeyVersionId!.Length.ShouldBe(1024);
        Should.Throw<PayloadKeyBackendProtocolException>(() => PayloadKeyBackendCodec.EncodeRequest(request with { ProviderKeyVersionId = new string('é', 513) }));
        Should.Throw<PayloadKeyBackendProtocolException>(() => PayloadKeyBackendCodec.DecodeRequest(new byte[16385]));
        Should.Throw<PayloadKeyBackendProtocolException>(() => PayloadKeyBackendCodec.DecodeResponse(new byte[16385]));
    }

    [Fact]
    public void BindingRejectsEveryResponseIdentityAndExactByteHashSubstitution()
    {
        using var request = Request(PayloadKeyBackendOperation.Wrap);
        byte[] encoded = PayloadKeyBackendCodec.EncodeRequest(request);
        using var response = Response(request, encoded);
        PayloadKeyBackendResponse[] changed = [response with { OperationId = RequestId }, response with { RequestId = OperationId },
            response with { InstallationId = "other" }, response with { ProfileSha256 = new('b', 64) },
            response with { RequestSha256 = new('b', 64) }, response with { ProviderKeyVersionId = "version-other" },
            response with { Operation = PayloadKeyBackendOperation.WrapExact }];
        foreach (var value in changed) { Should.Throw<PayloadKeyBackendProtocolException>(() => PayloadKeyBackendCodec.ValidateBinding(request, encoded, value)); }
        Should.Throw<PayloadKeyBackendProtocolException>(() => PayloadKeyBackendCodec.ValidateBinding(request, [.. encoded, (byte)' '], response));
        CryptographicOperations.ZeroMemory(encoded);
    }

    [Theory]
    [InlineData(PayloadKeyBackendOutcome.Unavailable)]
    [InlineData(PayloadKeyBackendOutcome.Denied)]
    [InlineData(PayloadKeyBackendOutcome.Missing)]
    [InlineData(PayloadKeyBackendOutcome.Invalidated)]
    [InlineData(PayloadKeyBackendOutcome.ConsistencyMismatch)]
    [InlineData(PayloadKeyBackendOutcome.Ambiguous)]
    public void FailuresCannotCarryBytesProfilesOrProviderVersions(PayloadKeyBackendOutcome outcome)
    {
        using var request = Request(PayloadKeyBackendOperation.Wrap);
        byte[] encoded = PayloadKeyBackendCodec.EncodeRequest(request);
        using var response = Response(request, encoded) with { Outcome = outcome, Bytes = null, ProviderKeyVersionId = null };
        using var read = PayloadKeyBackendCodec.DecodeResponse(PayloadKeyBackendCodec.EncodeResponse(response));
        read.Outcome.ShouldBe(outcome);
        Should.Throw<PayloadKeyBackendProtocolException>(() => PayloadKeyBackendCodec.EncodeResponse(response with { Bytes = new byte[384] }));
        Should.Throw<PayloadKeyBackendProtocolException>(() => PayloadKeyBackendCodec.EncodeResponse(response with { ProviderKeyVersionId = "private-version" }));
        CryptographicOperations.ZeroMemory(encoded);
    }

    [Fact]
    public void ProfileRequiresSoftwareRsaAndEveryClosedFlagAndOperation()
    {
        var profile = Profile();
        PayloadKeyBackendCodec.ValidateProfile(profile);
        PayloadKeyBackendProfile[] invalid = [profile with { Exportable = true }, profile with { AllowPlaintextBackup = true },
            profile with { Imported = true }, profile with { Derived = true }, profile with { Convergent = true },
            profile with { KeyType = "rsa-2048" }, profile with { WrapAlgorithm = "RSA-OAEP" },
            profile with { SupportedOperations = [PayloadKeyBackendOperation.Profile] },
            profile with { SupportedOperations = [PayloadKeyBackendOperation.Profile, PayloadKeyBackendOperation.Profile, PayloadKeyBackendOperation.Wrap, PayloadKeyBackendOperation.Unwrap, PayloadKeyBackendOperation.Lookup] }];
        foreach (var value in invalid) { Should.Throw<PayloadKeyBackendProtocolException>(() => PayloadKeyBackendCodec.ValidateProfile(value)); }
    }

    [Fact]
    public void DisposalClearsDecodedOwnersAndFormattingNeverIncludesBytesOrProviderIdentity()
    {
        using var original = Request(PayloadKeyBackendOperation.Unwrap);
        byte[] encoded = PayloadKeyBackendCodec.EncodeRequest(original);
        var decoded = PayloadKeyBackendCodec.DecodeRequest(encoded);
        byte[] input = decoded.Bytes!;
        decoded.Dispose(); input.ShouldAllBe(value => value == 0);
        var output = Response(original, encoded);
        byte[] returned = output.Bytes!;
        output.ToString().ShouldBe(nameof(PayloadKeyBackendResponse));
        original.ToString().ShouldBe(nameof(PayloadKeyBackendRequest));
        Profile().ToString().ShouldBe(nameof(PayloadKeyBackendProfile));
        output.Dispose(); returned.ShouldAllBe(value => value == 0);
        CryptographicOperations.ZeroMemory(encoded);
    }

    private static PayloadKeyBackendRequest Request(PayloadKeyBackendOperation operation)
        => new(1, operation, OperationId, RequestId, Installation, ProfileDigest,
            operation is PayloadKeyBackendOperation.Profile or PayloadKeyBackendOperation.Lookup ? null : "fixture:key:version-1",
            operation is PayloadKeyBackendOperation.Profile or PayloadKeyBackendOperation.Lookup ? null : Enumerable.Repeat((byte)0xc7, operation == PayloadKeyBackendOperation.Unwrap ? 384 : 32).ToArray());
    private static PayloadKeyBackendProfile Profile() => new("rsa-3072", "RSA-OAEP-256", false, false, false, false, false,
        "fixture:key:version-1", Enum.GetValues<PayloadKeyBackendOperation>());
    private static PayloadKeyBackendResponse Response(PayloadKeyBackendRequest request, byte[] encoded)
        => new(1, request.Operation, request.OperationId, request.RequestId, Installation, ProfileDigest, Convert.ToHexStringLower(SHA256.HashData(encoded)),
            PayloadKeyBackendOutcome.Succeeded, request.ProviderKeyVersionId,
            request.Operation is PayloadKeyBackendOperation.Profile or PayloadKeyBackendOperation.Lookup ? null : Enumerable.Repeat((byte)0xd9, request.Operation == PayloadKeyBackendOperation.Unwrap ? 32 : 384).ToArray(),
            request.Operation == PayloadKeyBackendOperation.Profile ? Profile() : null);
}
