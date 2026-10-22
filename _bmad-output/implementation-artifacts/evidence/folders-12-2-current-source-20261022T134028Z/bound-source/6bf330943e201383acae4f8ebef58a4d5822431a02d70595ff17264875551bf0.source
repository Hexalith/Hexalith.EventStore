using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;

using Microsoft.AspNetCore.Mvc;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.IntegrationTests;

/// <summary>
/// HTTP-level integration tests that post real JSON through ASP.NET Core MVC model binding for
/// the dead-letter Retry / Skip / Archive endpoints. These tests catch the
/// <c>[property: Required]</c> on a positional record bug (Issue 9): direct controller unit tests
/// bypass the model-binding pipeline and would still pass even when the record metadata is broken.
/// </summary>
public class AdminDeadLettersHttpBindingTests : IDisposable {
    private static readonly string[] s_actions = ["retry", "skip", "archive"];

    private readonly AdminTestHost _host;
    private readonly HttpClient _client;

    public AdminDeadLettersHttpBindingTests() {
        _host = new AdminTestHost();
        _client = _host.CreateClient();
        SetClaims(
            new Claim(AdminClaimTypes.AdminRole, "Operator"),
            new Claim(AdminClaimTypes.Tenant, "tenant-a"));
    }

    public static IEnumerable<object[]> AllActions() => s_actions.Select(a => new object[] { a });

    [Theory]
    [MemberData(nameof(AllActions))]
    public async Task ValidBody_BindsAndReachesService(string action) {
        IDeadLetterCommandService commandService = _host.GetService<IDeadLetterCommandService>();
        ConfigureSuccess(commandService);

        using var content = new StringContent(
            """{"messageIds":["manual-dlq-tenant-a-001"]}""",
            Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/admin/dead-letters/tenant-a/{action}",
            content);

        // No model-binding 500 — the service was actually invoked (success-mocked) and returns 200.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        VerifyCommandServiceReceived(commandService, action, ["manual-dlq-tenant-a-001"]);
    }

    [Theory]
    [MemberData(nameof(AllActions))]
    public async Task MissingMessageIds_Returns400ProblemDetails(string action) {
        using var content = new StringContent(
            "{}",
            Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/admin/dead-letters/tenant-a/{action}",
            content);

        await AssertControlledBadRequestAsync(response);
    }

    [Theory]
    [MemberData(nameof(AllActions))]
    public async Task NullMessageIds_Returns400ProblemDetails(string action) {
        using var content = new StringContent(
            """{"messageIds":null}""",
            Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/admin/dead-letters/tenant-a/{action}",
            content);

        await AssertControlledBadRequestAsync(response);
    }

    [Theory]
    [MemberData(nameof(AllActions))]
    public async Task EmptyMessageIds_Returns400ProblemDetails(string action) {
        using var content = new StringContent(
            """{"messageIds":[]}""",
            Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/admin/dead-letters/tenant-a/{action}",
            content);

        await AssertControlledBadRequestAsync(response);
    }

    [Theory]
    [MemberData(nameof(AllActions))]
    public async Task WhitespaceMessageId_Returns400ProblemDetails(string action) {
        using var content = new StringContent(
            """{"messageIds":["   "]}""",
            Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/admin/dead-letters/tenant-a/{action}",
            content);

        await AssertControlledBadRequestAsync(response);
    }

    [Theory]
    [MemberData(nameof(AllActions))]
    public async Task EmptyStringMessageId_Returns400ProblemDetails(string action) {
        using var content = new StringContent(
            """{"messageIds":[""]}""",
            Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/admin/dead-letters/tenant-a/{action}",
            content);

        await AssertControlledBadRequestAsync(response);
    }

    [Theory]
    [MemberData(nameof(AllActions))]
    public async Task DuplicateMessageIds_AreDeDuplicated_BeforeServiceInvocation(string action) {
        IDeadLetterCommandService commandService = _host.GetService<IDeadLetterCommandService>();
        ConfigureSuccess(commandService);

        using var content = new StringContent(
            """{"messageIds":["dup-1","dup-2","dup-1","dup-2","dup-3"]}""",
            Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/admin/dead-letters/tenant-a/{action}",
            content);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        VerifyCommandServiceReceived(commandService, action, ["dup-1", "dup-2", "dup-3"]);
    }

    [Theory]
    [MemberData(nameof(AllActions))]
    public async Task VisualFixtureMissBackend404_MapsToRecoverable4xx(string action) {
        // AC4: when the manual fixture message doesn't exist in the backend, the response must be
        // a recoverable 4xx (404/422), not a model-binding 500 or 5xx from the service path.
        IDeadLetterCommandService commandService = _host.GetService<IDeadLetterCommandService>();
        var notFoundResult = new AdminOperationResult(false, "op-1", "Dead-letter message not found.", "NotFound");
        ConfigureAll(commandService, notFoundResult);

        using var content = new StringContent(
            """{"messageIds":["manual-dlq-tenant-a-001"]}""",
            Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/admin/dead-letters/tenant-a/{action}",
            content);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        _ = problem.ShouldNotBeNull();
        problem!.Status.ShouldBe((int)HttpStatusCode.NotFound);
        problem.Detail.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(AllActions))]
    public async Task VisualFixtureMissBackendInvalidOperation_MapsTo422(string action) {
        // AC4: alternate visual-fixture path — backend declines the operation as invalid;
        // the surface must be a recoverable 422 ProblemDetails, not a 500.
        IDeadLetterCommandService commandService = _host.GetService<IDeadLetterCommandService>();
        var invalidResult = new AdminOperationResult(false, "op-2", "Dead-letter is not actionable.", "InvalidOperation");
        ConfigureAll(commandService, invalidResult);

        using var content = new StringContent(
            """{"messageIds":["manual-dlq-tenant-a-001"]}""",
            Encoding.UTF8,
            "application/json");
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/admin/dead-letters/tenant-a/{action}",
            content);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        _ = problem.ShouldNotBeNull();
    }

    private static async Task AssertControlledBadRequestAsync(HttpResponseMessage response) {
        // Required: the body validation surfaces as a controlled ProblemDetails 400. Not a
        // 500 InvalidOperationException about record-property validation metadata, and not a 5xx.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("validation metadata", Case.Insensitive);
        body.ShouldNotContain("InvalidOperationException", Case.Insensitive);
        body.ShouldNotContain("System.Net.Http", Case.Insensitive);
    }

    private static void ConfigureSuccess(IDeadLetterCommandService commandService) {
        var ok = new AdminOperationResult(true, "op-1", "ok", null);
        ConfigureAll(commandService, ok);
    }

    private static void ConfigureAll(IDeadLetterCommandService commandService, AdminOperationResult result) {
        _ = commandService.RetryDeadLettersAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(result);
        _ = commandService.SkipDeadLettersAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(result);
        _ = commandService.ArchiveDeadLettersAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(result);
    }

    private static void VerifyCommandServiceReceived(
        IDeadLetterCommandService commandService,
        string action,
        IReadOnlyList<string> expectedIds) => _ = action switch {
            "retry" => commandService.Received(1).RetryDeadLettersAsync(
                                "tenant-a",
                                Arg.Is<IReadOnlyList<string>>(ids => Sequence(ids, expectedIds)),
                                Arg.Any<CancellationToken>()),
            "skip" => commandService.Received(1).SkipDeadLettersAsync(
                                "tenant-a",
                                Arg.Is<IReadOnlyList<string>>(ids => Sequence(ids, expectedIds)),
                                Arg.Any<CancellationToken>()),
            "archive" => commandService.Received(1).ArchiveDeadLettersAsync(
                                "tenant-a",
                                Arg.Is<IReadOnlyList<string>>(ids => Sequence(ids, expectedIds)),
                                Arg.Any<CancellationToken>()),
            _ => throw new InvalidOperationException($"Unknown action '{action}'."),
        };

    private static bool Sequence(IReadOnlyList<string> actual, IReadOnlyList<string> expected) {
        if (actual.Count != expected.Count) {
            return false;
        }

        for (int i = 0; i < actual.Count; i++) {
            if (!string.Equals(actual[i], expected[i], StringComparison.Ordinal)) {
                return false;
            }
        }

        return true;
    }

    private void SetClaims(params Claim[] claims) {
        var dtos = claims.Select(c => new { c.Type, c.Value }).ToArray();
        string json = JsonSerializer.Serialize(dtos);
        _ = _client.DefaultRequestHeaders.Remove(TestAuthHandler.ClaimsHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.ClaimsHeader, json);
    }

    public void Dispose() {
        _client?.Dispose();
        _host?.Dispose();
        GC.SuppressFinalize(this);
    }
}
