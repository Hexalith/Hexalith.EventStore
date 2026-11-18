using System.Reflection;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

/// <summary>
/// Runs the emitted command action against every MessageId-only status identity row under both grammars.
/// Hosting validation's published version is simulated in the execution items, exactly where the real filter writes it.
/// </summary>
public sealed partial class RestApiGeneratedControllerErrorSemanticsTests
{
    private const string StatusBase = "https://gateway.example";
    private const string StatusPrefix = StatusBase + "/api/v1/commands/status/";
    private const string CanonicalUlid = "01ARZ3NDEKTSV4RRFFQ69G5FAV";
    private const string OtherCanonicalUlid = "01BX5ZZKBKACTAV9WEVGEMMVRZ";
    private const string LegacyMessageId = "legacy-status-1";

    // One compilation serves every status row; each row still gets a fresh controller, gateway and HTTP context.
    private static readonly Lazy<Assembly> StatusIdentityAssembly = new(() => CreateController().Assembly);

    public static TheoryData<int, string> MessageIdsValidForVersion => new()
    {
        { 1, LegacyMessageId },
        { 1, "a" },
        { 1, "A-b-9" },
        { 1, "Mixed-Case-Legacy-42" },
        { 1, CanonicalUlid },
        { 1, "01arz3ndektsv4rrffq69g5fav" },
        { 1, new string('a', 128) },
        { 1, "3f2504e0-4f89-11d3-9a0c-0305e82c3301" },
        { 2, CanonicalUlid },
        { 2, OtherCanonicalUlid },
        { 2, "00000000000000000000000000" },
        { 2, "7ZZZZZZZZZZZZZZZZZZZZZZZZZ" },
    };

    public static TheoryData<int, string> MessageIdsInvalidForVersion => new()
    {
        // v2: lowercase, mixed case, non-Crockford letters, non-canonical overflow, wrong length, legacy shapes.
        { 2, "01arz3ndektsv4rrffq69g5fav" },
        { 2, "01ARZ3NDEKTSV4RRFFQ69G5FAv" },
        { 2, "01ARZ3NDEKTSV4RRFFQ69G5FAI" },
        { 2, "01ARZ3NDEKTSV4RRFFQ69G5FAL" },
        { 2, "01ARZ3NDEKTSV4RRFFQ69G5FAO" },
        { 2, "01ARZ3NDEKTSV4RRFFQ69G5FAU" },
        { 2, "8ZZZZZZZZZZZZZZZZZZZZZZZZZ" },
        { 2, "ZZZZZZZZZZZZZZZZZZZZZZZZZZ" },
        { 2, "01ARZ3NDEKTSV4RRFFQ69G5FA" },
        { 2, "01ARZ3NDEKTSV4RRFFQ69G5FAVX" },
        { 2, " 01ARZ3NDEKTSV4RRFFQ69G5FA" },
        { 2, "01ARZ3NDEKTSV4RRFFQ69G5FA " },
        { 2, "01ARZ3NDEK-SV4RRFFQ69G5FAV" },
        { 2, LegacyMessageId },
        { 2, "3f2504e0-4f89-11d3-9a0c-0305e82c3301" },
        // v1: edge hyphens, over-long, characters outside ASCII alphanumerics and hyphen.
        { 1, "-legacy" },
        { 1, "legacy-" },
        { 1, "-" },
        { 1, new string('a', 129) },
        { 1, "legacy status" },
        { 1, " legacy" },
        { 1, "legacy_status" },
        { 1, "legacy.status" },
        { 1, "legacy/status" },
        { 1, "legacy%2Fstatus" },
        { 1, "l\u00E9gacy" },
        { 1, "\uFF41bc" },
        { 1, "legacy\n" },
    };

    public static TheoryData<int, string?> MissingMessageIds => new()
    {
        { 1, null },
        { 1, string.Empty },
        { 1, " " },
        { 2, null },
        { 2, string.Empty },
        { 2, "   " },
    };

    public static TheoryData<bool, object?> UndeclaredOrAmbiguousVersions => new()
    {
        { false, null },
        { true, null },
        { true, "1" },
        { true, "2" },
        { true, 1L },
        { true, 2L },
        { true, 1.0 },
        { true, (short)1 },
        { true, (byte)2 },
        { true, true },
        { true, 0 },
        { true, 3 },
        { true, -1 },
        { true, int.MaxValue },
    };

    [Theory]
    [MemberData(nameof(MessageIdsValidForVersion))]
    public async Task CommandStatus_MessageIdValidForDeclaredVersion_EmitsAbsoluteLocationFromMessageIdOnly(int version, string messageId)
    {
        IHeaderDictionary headers = await SubmitForStatusAsync(published: true, version, messageId, OtherCanonicalUlid);

        // Valid ids need no escaping, so the Location carries the response MessageId byte-for-byte.
        headers["Location"].ToString().ShouldBe(StatusPrefix + messageId);
        headers["Location"].ToString().ShouldNotStartWith("/");
    }

    [Theory]
    [MemberData(nameof(MessageIdsInvalidForVersion))]
    public async Task CommandStatus_MessageIdInvalidForDeclaredVersion_ReturnsAcceptedWithoutLocation(int version, string messageId)
    {
        // The CorrelationId is valid under both grammars; an invalid MessageId is never rescued by it.
        IHeaderDictionary headers = await SubmitForStatusAsync(published: true, version, messageId, CanonicalUlid);

        headers.ContainsKey("Location").ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(MissingMessageIds))]
    public async Task CommandStatus_MessageIdMissingWithValidCorrelationIdOnly_ReturnsAcceptedWithoutLocation(int version, string? messageId)
    {
        // The CorrelationId is valid under both grammars, so only a fallback could have produced a Location.
        IHeaderDictionary headers = await SubmitForStatusAsync(published: true, version, messageId, CanonicalUlid);

        headers.ContainsKey("Location").ShouldBeFalse();
    }

    [Theory]
    [InlineData(1, LegacyMessageId, CanonicalUlid)]
    [InlineData(1, CanonicalUlid, LegacyMessageId)]
    [InlineData(2, CanonicalUlid, OtherCanonicalUlid)]
    [InlineData(2, OtherCanonicalUlid, CanonicalUlid)]
    public async Task CommandStatus_ValidMessageIdDiffersFromCorrelationId_LocationUsesMessageIdOnly(int version, string messageId, string correlationId)
    {
        IHeaderDictionary headers = await SubmitForStatusAsync(published: true, version, messageId, correlationId);

        string location = headers["Location"].ToString();
        location.ShouldBe(StatusPrefix + messageId);
        location.ShouldNotContain(correlationId);
    }

    [Theory]
    [MemberData(nameof(UndeclaredOrAmbiguousVersions))]
    public async Task CommandStatus_VersionUndeclaredOrAmbiguous_ReturnsAcceptedWithoutLocation(bool published, object? version)
    {
        // The MessageId is valid under both grammars, so only the published version can refuse it.
        IHeaderDictionary headers = await SubmitForStatusAsync(published, version, CanonicalUlid, CanonicalUlid);

        headers.ContainsKey("Location").ShouldBeFalse();
    }

    [Theory]
    [InlineData(1, LegacyMessageId, "2", true)]
    [InlineData(2, LegacyMessageId, "1", false)]
    [InlineData(2, "01arz3ndektsv4rrffq69g5fav", "1", false)]
    public async Task CommandStatus_CallerClaimsAnotherVersion_DeclaredVersionDecides(int declared, string messageId, string claimed, bool expectLocation)
    {
        IHeaderDictionary headers = await SubmitForStatusAsync(published: true, declared, messageId, CanonicalUlid, request =>
        {
            request.Headers["X-Hexalith-MessageId-Version"] = claimed;
            request.Headers[RouteCatalogGeneratedAction.MessageIdVersionKey] = claimed;
            request.QueryString = new QueryString("?messageIdVersion=" + claimed + "&MessageIdVersion=" + claimed);
        });

        if (expectLocation)
        {
            headers["Location"].ToString().ShouldBe(StatusPrefix + messageId);
        }
        else
        {
            headers.ContainsKey("Location").ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData(1, LegacyMessageId)]
    [InlineData(2, CanonicalUlid)]
    public async Task CommandStatus_ValidMessageIdWithoutConfiguredStatusBase_ReturnsAcceptedWithoutLocation(int version, string messageId)
    {
        IHeaderDictionary headers = await SubmitForStatusAsync(published: true, version, messageId, CanonicalUlid, statusBase: null);

        headers.ContainsKey("Location").ShouldBeFalse();
    }

    [Theory]
    [InlineData(1, LegacyMessageId)]
    [InlineData(2, CanonicalUlid)]
    public async Task CommandStatus_GatewayFailureWithDeclaredVersion_ReturnsProblemWithoutLocation(int version, string messageId)
    {
        RestApiGeneratedController controller = CreateStatusController(StatusBase, published: true, version);
        controller.Gateway.CommandHandler = (_, _) =>
            throw new EventStoreGatewayException(
                StatusCodes.Status503ServiceUnavailable,
                "Service Unavailable",
                detail: "The command gateway is unavailable.",
                correlationId: messageId,
                reason: "gateway-unavailable",
                retryAfter: "5",
                reasonCode: "gateway-unavailable");

        IActionResult result = await InvokeCommandAsync(controller, "counter-1", CreateIncrementBody(controller));

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        controller.HttpContext.Response.Headers.ContainsKey("Location").ShouldBeFalse();
    }

    private static async Task<IHeaderDictionary> SubmitForStatusAsync(
        bool published,
        object? version,
        string? messageId,
        string correlationId,
        Action<HttpRequest>? configureRequest = null,
        string? statusBase = StatusBase)
    {
        RestApiGeneratedController controller = CreateStatusController(statusBase, published, version);
        configureRequest?.Invoke(controller.HttpContext.Request);
        var submitted = new SubmitCommandResponse(correlationId, MessageId: messageId);
        controller.Gateway.CommandHandler = (_, _) => Task.FromResult(submitted);

        IActionResult result = await InvokeCommandAsync(controller, "counter-1", CreateIncrementBody(controller));

        AcceptedResult accepted = result.ShouldBeOfType<AcceptedResult>();
        accepted.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        accepted.Value.ShouldBeSameAs(submitted);
        controller.Gateway.CommandCallCount.ShouldBe(1);
        IHeaderDictionary headers = controller.HttpContext.Response.Headers;
        headers["Retry-After"].ToString().ShouldBe("1");
        return headers;
    }

    private static RestApiGeneratedController CreateStatusController(string? statusBase, bool published, object? version)
    {
        Assembly assembly = StatusIdentityAssembly.Value;
        Type controllerType = assembly.GetTypes()
            .Single(static type => string.Equals(type.Name, "CounterRestController", StringComparison.Ordinal));
        var gateway = new FakeEventStoreGatewayClient();
        var controller = (ControllerBase)Activator.CreateInstance(
            controllerType,
            gateway,
            new FakeCommandStatusLocationBuilder(statusBase))!;
        DefaultHttpContext context = CreateCatalogIsolatedContext();
        if (published)
        {
            context.Items[RouteCatalogGeneratedAction.MessageIdVersionKey] = version;
        }
        else
        {
            _ = context.Items.Remove(RouteCatalogGeneratedAction.MessageIdVersionKey);
        }

        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return new RestApiGeneratedController(assembly, gateway, controller);
    }

    private static object CreateIncrementBody(RestApiGeneratedController controller)
        => Activator.CreateInstance(
            controller.Assembly.GetType("Smoke.IncrementCounter", throwOnError: true)!,
            "counter-1",
            5)!;
}
