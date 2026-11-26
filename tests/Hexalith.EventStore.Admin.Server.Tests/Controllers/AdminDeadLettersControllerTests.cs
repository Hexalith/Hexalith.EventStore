using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Admin.Server.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminDeadLettersControllerTests {
    private readonly IDeadLetterQueryService _queryService = Substitute.For<IDeadLetterQueryService>();
    private readonly IDeadLetterCommandService _commandService = Substitute.For<IDeadLetterCommandService>();
    private readonly AdminDeadLettersController _sut;

    public AdminDeadLettersControllerTests() => _sut = new AdminDeadLettersController(_queryService, _commandService, NullLogger<AdminDeadLettersController>.Instance) {
        ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext {
                User = CreatePrincipal("Operator", "tenant-a"),
            },
        }
    };

    [Fact]
    public async Task ListDeadLetters_DelegatesToQueryService() {
        var expected = new PagedResult<DeadLetterEntry>([], 0, null);
        _ = _queryService.ListDeadLettersAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ListDeadLetters("tenant-a");

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ListDeadLetters_RejectsInvalidCursorWithBoundedProblem() {
        _ = _queryService.ListDeadLettersAsync("tenant-a", 100, "stale", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PagedResult<DeadLetterEntry>>(
                new HttpRequestException("private cursor information", null, HttpStatusCode.BadRequest)));

        IActionResult result = await _sut.ListDeadLetters("tenant-a", continuationToken: "stale");

        ObjectResult invalid = result.ShouldBeOfType<ObjectResult>();
        invalid.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        invalid.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldNotContain("private cursor information");
    }

    [Fact]
    public async Task RetryDeadLetters_DelegatesToCommandService() {
        var expected = new AdminOperationResult(true, "op-1", "Retried", null);
        _ = _commandService.RetryDeadLettersAsync("tenant-a", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.RetryDeadLetters("tenant-a", new DeadLetterActionRequest { MessageIds = ["msg-1"] });

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task SkipDeadLetters_DelegatesToCommandService() {
        var expected = new AdminOperationResult(true, "op-2", "Skipped", null);
        _ = _commandService.SkipDeadLettersAsync("tenant-a", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.SkipDeadLetters("tenant-a", new DeadLetterActionRequest { MessageIds = ["msg-1"] });

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ArchiveDeadLetters_DelegatesToCommandService() {
        var expected = new AdminOperationResult(true, "op-3", "Archived", null);
        _ = _commandService.ArchiveDeadLettersAsync("tenant-a", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ArchiveDeadLetters("tenant-a", new DeadLetterActionRequest { MessageIds = ["msg-1"] });

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("retry")]
    [InlineData("skip")]
    [InlineData("archive")]
    public async Task RecoveryActions_ForwardStableActionIdentityAndDistinctMessages(string action) {
        var persisted = new AdminOperationResult(true, "operation-7", "The recovery action state was persisted.", null) { State = "completed" };
        _ = _commandService.RetryDeadLettersAsync("tenant-a", Arg.Any<IReadOnlyList<string>>(), "action-7", Arg.Any<CancellationToken>())
            .Returns(persisted);
        _ = _commandService.SkipDeadLettersAsync("tenant-a", Arg.Any<IReadOnlyList<string>>(), "action-7", Arg.Any<CancellationToken>())
            .Returns(persisted);
        _ = _commandService.ArchiveDeadLettersAsync("tenant-a", Arg.Any<IReadOnlyList<string>>(), "action-7", Arg.Any<CancellationToken>())
            .Returns(persisted);
        var request = new DeadLetterActionRequest { MessageIds = ["msg-2", "msg-1", "msg-2"], ActionId = "action-7" };

        IActionResult result = await InvokeActionAsync(action, request);

        result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(persisted);
        string[] expectedIds = ["msg-2", "msg-1"];
        _ = action switch {
            "retry" => await _commandService.Received(1).RetryDeadLettersAsync(
                "tenant-a", Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(expectedIds)), "action-7", Arg.Any<CancellationToken>()),
            "skip" => await _commandService.Received(1).SkipDeadLettersAsync(
                "tenant-a", Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(expectedIds)), "action-7", Arg.Any<CancellationToken>()),
            _ => await _commandService.Received(1).ArchiveDeadLettersAsync(
                "tenant-a", Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(expectedIds)), "action-7", Arg.Any<CancellationToken>()),
        };
        _ = await _commandService.DidNotReceive().RetryDeadLettersAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        _ = await _commandService.DidNotReceive().SkipDeadLettersAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        _ = await _commandService.DidNotReceive().ArchiveDeadLettersAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("pending", false, StatusCodes.Status202Accepted)]
    [InlineData("completed", true, StatusCodes.Status200OK)]
    [InlineData("failed", false, StatusCodes.Status422UnprocessableEntity)]
    public async Task RetryDeadLetters_ReportsPersistedActionStateWithoutFabricatedCompletion(string state, bool success, int expectedStatus) {
        var persisted = new AdminOperationResult(success, "operation-8", "The recovery action state was persisted.", success ? null : "InvalidOperation") {
            State = state,
        };
        _ = _commandService.RetryDeadLettersAsync("tenant-a", Arg.Any<IReadOnlyList<string>>(), "action-8", Arg.Any<CancellationToken>())
            .Returns(persisted);

        IActionResult result = await _sut.RetryDeadLetters("tenant-a", new DeadLetterActionRequest { MessageIds = ["msg-1"], ActionId = "action-8" });

        ObjectResult objectResult = result.ShouldBeAssignableTo<ObjectResult>().ShouldNotBeNull();
        (objectResult.StatusCode ?? StatusCodes.Status200OK).ShouldBe(expectedStatus);
        AdminOperationResult reported = objectResult.Value.ShouldBeOfType<AdminOperationResult>();
        reported.State.ShouldBe(state);
        reported.OperationId.ShouldBe("operation-8");
        reported.Success.ShouldBe(state == "completed");
    }

    [Fact]
    public async Task ArchiveDeadLetters_ReportsUnavailableRecoveryProfileAsServiceUnavailable() {
        _ = _commandService.ArchiveDeadLettersAsync("tenant-a", Arg.Any<IReadOnlyList<string>>(), "action-9", Arg.Any<CancellationToken>())
            .Returns(new AdminOperationResult(false, "error-no-operation", "The recovery action is unavailable.", "Unavailable"));

        IActionResult result = await _sut.ArchiveDeadLetters("tenant-a", new DeadLetterActionRequest { MessageIds = ["msg-1"], ActionId = "action-9" });

        ObjectResult problem = result.ShouldBeOfType<ObjectResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        _ = problem.Value.ShouldBeOfType<ProblemDetails>();
    }

    [Theory]
    [InlineData(" padded")]
    [InlineData("padded ")]
    [InlineData("control\u0001")]
    [InlineData("   ")]
    public void ActionRequest_RejectsActionIdsOperationsWouldRefuse(string actionId) {
        var request = new DeadLetterActionRequest { MessageIds = ["msg-1"], ActionId = actionId };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true).ShouldBeFalse();

        results.ShouldContain(result => result.MemberNames.Contains(nameof(DeadLetterActionRequest.ActionId)));
    }

    [Fact]
    public void ActionRequest_AcceptsBoundedActionIdAndRejectsOverLengthValue() {
        var valid = new DeadLetterActionRequest { MessageIds = ["msg-1"], ActionId = "01JDELIVERY7ACTION0000000A" };
        var overLength = new DeadLetterActionRequest { MessageIds = ["msg-1"], ActionId = new string('a', 129) };

        Validator.TryValidateObject(valid, new ValidationContext(valid), [], validateAllProperties: true).ShouldBeTrue();
        Validator.TryValidateObject(overLength, new ValidationContext(overLength), [], validateAllProperties: true).ShouldBeFalse();
    }

    private Task<IActionResult> InvokeActionAsync(string action, DeadLetterActionRequest request)
        => action switch {
            "retry" => _sut.RetryDeadLetters("tenant-a", request),
            "skip" => _sut.SkipDeadLetters("tenant-a", request),
            _ => _sut.ArchiveDeadLetters("tenant-a", request),
        };

    private static ClaimsPrincipal CreatePrincipal(string adminRole, params string[] tenants) {
        var claims = new List<Claim> { new(AdminClaimTypes.AdminRole, adminRole) };
        foreach (string tenant in tenants) {
            claims.Add(new Claim(AdminClaimTypes.Tenant, tenant));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
