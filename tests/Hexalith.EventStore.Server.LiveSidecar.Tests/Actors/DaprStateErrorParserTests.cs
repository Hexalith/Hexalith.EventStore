using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Actors;

/// <summary>Deterministic malformed-body coverage for the Story 4.5 generic ETag control.</summary>
public sealed class DaprStateErrorParserTests
{
    /// <summary>Verifies valid Dapr state errors are captured without a diagnostic.</summary>
    [Fact]
    public void Parse_CompleteError_ReturnsExactFields()
    {
        DaprStateErrorParser.Capture capture = DaprStateErrorParser.Parse(
            "{\"errorCode\":\"ERR_STATE_SAVE\",\"message\":\"possible etag mismatch\"}");

        capture.ErrorCode.ShouldBe("ERR_STATE_SAVE");
        capture.Message.ShouldBe("possible etag mismatch");
        capture.ParseError.ShouldBeNull();
    }

    /// <summary>Verifies malformed or incomplete bodies become evidence diagnostics rather than exceptions.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"errorCode\":17,\"message\":null}")]
    public void Parse_MalformedOrIncompleteBody_ReturnsDiagnostic(string responseBody)
    {
        DaprStateErrorParser.Capture capture = DaprStateErrorParser.Parse(responseBody);

        capture.ParseError.ShouldNotBeNullOrWhiteSpace();
    }
}
