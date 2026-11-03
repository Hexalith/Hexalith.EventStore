// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3-7; base V079-V106, V127-V132.
using System.Text;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
public sealed class AzureKeyVaultTransportTests
{
    [Theory]
    [InlineData("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\n{}")]
    [InlineData("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n1\r\n{\r\n1\r\n}\r\n0\r\n\r\n")]
    [InlineData("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n\r\n{}")]
    public void V091_BoundedFramingAcceptsCompleteContentLengthChunkedAndClose(string wire)
    {
        using var response = AzureKeyVaultSafeTransport.ParseResponse(Encoding.ASCII.GetBytes(wire));
        response.Status.ShouldBe(200);
        using var reader = new StreamReader(response.ContentStream!);
        reader.ReadToEnd().ShouldBe("{}");
    }

    [Theory]
    [InlineData("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\n{}")]
    [InlineData("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nContent-Length: 2\r\n\r\n{}")]
    [InlineData("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nTransfer-Encoding: chunked\r\n\r\n{}")]
    [InlineData("HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip\r\n\r\n{}")]
    [InlineData("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n4\r\n{}\r\n0\r\n\r\n")]
    [InlineData("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nffffffff\r\n")]
    [InlineData("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n0\r\nprivate-trailer: canary\r\n\r\n")]
    [InlineData("HTTP/1.1 100 Continue\r\nContent-Length: 0\r\n\r\n")]
    public void V091_RejectsMalformedOrAmbiguousFraming(string wire) => Should.Throw<InvalidOperationException>(() => AzureKeyVaultSafeTransport.ParseResponse(Encoding.ASCII.GetBytes(wire)));
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void V091_ResponseBoundAcceptsMaximumAndRejectsOneByteOver(bool over)
    {
        string body = new('x', 65536 + (over ? 1 : 0));
        byte[] raw = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: " + body.Length + "\r\n\r\n" + body);
        if (over)
        {
            Should.Throw<InvalidOperationException>(() => AzureKeyVaultSafeTransport.ParseResponse(raw));
        }
        else
        {
            using var response = AzureKeyVaultSafeTransport.ParseResponse(raw);
            response.ContentStream!.Length.ShouldBe(65536);
        }
    }

    [Fact]
    public void V127_PrivateErrorBodiesAndRequestIdsNeverEnterSdkExceptions()
    {
        using var response = AzureKeyVaultSafeTransport.ParseResponse("HTTP/1.1 403 Forbidden\r\nContent-Length: 22\r\nx-ms-request-id: private-id-canary\r\n\r\nprivate-message-canary"u8);
        using var reader = new StreamReader(response.ContentStream!);
        reader.ReadToEnd().ShouldNotContain("canary");
        response.Headers.TryGetValue("x-ms-request-id", out _).ShouldBeFalse();
    }

    [Fact]
    public void V078_ResponseDisposalClearsTransferredBody()
    {
        byte[] body = "key-canary"u8.ToArray();
        var response = AdapterFixture.Response(200, body);
        response.Dispose();
        body.ShouldAllBe(value => value == 0);
    }

    [Theory]
    [InlineData("https://evil.invalid/tenant", "https://vault.azure.net")]
    [InlineData("https://login.microsoftonline.com/00000000-0000-0000-0000-000000000001", "https://foreign-resource.invalid")]
    public void V127_MetadataChallengeCannotRetargetAuthenticationOrRenderProviderValues(string authority, string resource)
    {
        string challenge = "Bearer authorization=\"" + authority + "\", resource=\"" + resource + "\"";
        Should.Throw<AzureKeyVaultTransportException>(() => AzureKeyVaultMetadataChallenge.Validate(challenge, AdapterFixture.Options)).Message.ShouldBe("The key transport failed.");
    }
}
