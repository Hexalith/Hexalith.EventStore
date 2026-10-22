using System.Net;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Proves routing diagnostics preserve responses and exclude protected content.</summary>
public sealed class Oq8InvocationDiagnosticHandlerTests
{
    /// <summary>Records only method, authority/path, status and a whitelisted Dapr error code.</summary>
    /// <param name="body">The simulated response content.</param>
    /// <param name="expectedCode">The support-safe code classification.</param>
    [Theory]
    [InlineData("{\"errorCode\":\"ERR_DIRECT_INVOKE\",\"message\":\"protected-response-marker\"}", "ERR_DIRECT_INVOKE")]
    [InlineData("{\"errorCode\":\"protected-response-marker\"}", "none")]
    [InlineData("protected-response-marker", "non-json")]
    [InlineData("[\"protected-response-marker\"]", "none")]
    public async Task FailureDiagnosticsPreserveResponseAndExcludeProtectedContent(string body, string expectedCode)
    {
        var logger = new Oq8DiagnosticRecordingLogger();
        using var handler = new Oq8InvocationDiagnosticHandler(logger)
        {
            InnerHandler = new Oq8DiagnosticResponseHandler(body),
        };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "http://127.0.0.1:1234/v1.0/invoke/sample/method/process?protected=protected-query-marker")
        {
            Content = new StringContent("protected-request-marker"),
        };

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldBe(body);
        string entry = logger.Entries.ShouldHaveSingleItem();
        entry.ShouldContain("Method=POST");
        entry.ShouldContain("Endpoint=http://127.0.0.1:1234/v1.0/invoke/sample/method/process");
        entry.ShouldContain("StatusCode=404");
        entry.ShouldContain("ErrorCode=" + expectedCode);
        entry.ShouldNotContain("protected-");
    }

}
