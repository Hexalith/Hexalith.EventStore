using Hexalith.EventStore.Admin.Abstractions.Security;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Security;

public class UnsafeMarkerDetectionTests
{
    [Theory]
    [InlineData("https://example.test/callback?access%5Ftoken=secret-value")]
    [InlineData("https://example.test/callback?%61ccess_token=secret-value")]
    [InlineData("https://example.test/download?sig=secret-value")]
    [InlineData("(Bearer secret-token)")]
    [InlineData("[Bearer secret-token]")]
    [InlineData("eyJhbGciOiJub25lIn0.cGF5bG9hZA.")]
    public void ContainsUnsafeMarker_DetectsCredentialShapes(string value)
    {
        UnsafeMarkerDetection.ContainsUnsafeMarker(value).ShouldBeTrue();
    }
}
