
using System.Security.Claims;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.CommandApi.Authorization;
using Hexalith.EventStore.CommandApi.Configuration;
using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.Server.Actors.Authorization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authorization;

public class ActorRbacValidatorTests {
    private const string ActorType = "TestRbacValidatorActor";
    private const string Domain = "Sales";
    private const string MessageCategory = "command";
    private const string MessageType = "SubmitOrder";
    private const string TenantId = "tenant-123";
    private const string UserId = "user-456";

    private static ClaimsPrincipal CreatePrincipalWithNameIdentifier(string userId) {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static ClaimsPrincipal CreatePrincipalWithoutNameIdentifier() {
        var claims = new List<Claim> { new("name", "Display Name") };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static (ActorRbacValidator validator, IActorProxyFactory factory) CreateValidator(
        int retryAfterSeconds = 5) {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IOptions<EventStoreAuthorizationOptions> options = Options.Create(
            new EventStoreAuthorizationOptions {
                RbacValidatorActorName = ActorType,
                RetryAfterSeconds = retryAfterSeconds,
            });
        var logger = NullLoggerFactory.Instance.CreateLogger<ActorRbacValidator>();
        var validator = new ActorRbacValidator(factory, options, logger);
        return (validator, factory);
    }

    private static void SetupActorProxy(
        IActorProxyFactory factory,
        ActorValidationResponse? response) {
        IRbacValidatorActor actor = Substitute.For<IRbacValidatorActor>();
        actor.ValidatePermissionAsync(Arg.Any<RbacValidationRequest>())
            .Returns(Task.FromResult(response)!);

        factory.CreateActorProxy<IRbacValidatorActor>(
            Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);
    }

    private static void SetupActorProxyThrows(
        IActorProxyFactory factory,
        Exception exception) {
        IRbacValidatorActor actor = Substitute.For<IRbacValidatorActor>();
        actor.ValidatePermissionAsync(Arg.Any<RbacValidationRequest>())
            .Throws(exception);

        factory.CreateActorProxy<IRbacValidatorActor>(
            Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);
    }

    [Fact]
    public async Task ValidateAsync_ActorAllows_ReturnsAllowed() {
        // Arrange
        (ActorRbacValidator validator, IActorProxyFactory factory) = CreateValidator();
        SetupActorProxy(factory, new ActorValidationResponse(true));
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act
        RbacValidationResult result = await validator.ValidateAsync(user, TenantId, Domain, MessageType, MessageCategory, CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ActorDenies_ReturnsDeniedWithReason() {
        // Arrange
        (ActorRbacValidator validator, IActorProxyFactory factory) = CreateValidator();
        SetupActorProxy(factory, new ActorValidationResponse(false, "No write permission"));
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act
        RbacValidationResult result = await validator.ValidateAsync(user, TenantId, Domain, MessageType, MessageCategory, CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
        result.Reason.ShouldBe("No write permission");
    }

    [Fact]
    public async Task ValidateAsync_ActorReturnsNull_ThrowsServiceUnavailable() {
        // Arrange
        (ActorRbacValidator validator, IActorProxyFactory factory) = CreateValidator();
        SetupActorProxy(factory, null);
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act & Assert
        AuthorizationServiceUnavailableException ex = await Should.ThrowAsync<AuthorizationServiceUnavailableException>(
            () => validator.ValidateAsync(user, TenantId, Domain, MessageType, MessageCategory, CancellationToken.None));

        ex.ActorTypeName.ShouldBe(ActorType);
        ex.ActorId.ShouldBe(TenantId);
    }

    [Fact]
    public async Task ValidateAsync_ActorUnreachable_ThrowsServiceUnavailable() {
        // Arrange
        (ActorRbacValidator validator, IActorProxyFactory factory) = CreateValidator();
        SetupActorProxyThrows(factory, new HttpRequestException("Connection refused"));
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act & Assert
        AuthorizationServiceUnavailableException ex = await Should.ThrowAsync<AuthorizationServiceUnavailableException>(
            () => validator.ValidateAsync(user, TenantId, Domain, MessageType, MessageCategory, CancellationToken.None));

        ex.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task ValidateAsync_ExtractsUserIdFromNameIdentifierClaim() {
        // Arrange
        (ActorRbacValidator validator, IActorProxyFactory factory) = CreateValidator();
        IRbacValidatorActor actor = Substitute.For<IRbacValidatorActor>();
        RbacValidationRequest? capturedRequest = null;
        actor.ValidatePermissionAsync(Arg.Do<RbacValidationRequest>(r => capturedRequest = r))
            .Returns(Task.FromResult(new ActorValidationResponse(true)));
        factory.CreateActorProxy<IRbacValidatorActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier("specific-user-id");

        // Act
        _ = await validator.ValidateAsync(user, TenantId, Domain, MessageType, MessageCategory, CancellationToken.None);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.UserId.ShouldBe("specific-user-id");
    }

    [Fact]
    public async Task ValidateAsync_NoNameIdentifierClaim_ThrowsInvalidOperation() {
        // Arrange
        (ActorRbacValidator validator, _) = CreateValidator();
        ClaimsPrincipal user = CreatePrincipalWithoutNameIdentifier();

        // Act & Assert
        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => validator.ValidateAsync(user, TenantId, Domain, MessageType, MessageCategory, CancellationToken.None));

        ex.Message.ShouldContain("NameIdentifier");
    }

    [Fact]
    public async Task ValidateAsync_ForwardsMessageCategory() {
        // Arrange
        (ActorRbacValidator validator, IActorProxyFactory factory) = CreateValidator();
        IRbacValidatorActor actor = Substitute.For<IRbacValidatorActor>();
        RbacValidationRequest? capturedRequest = null;
        actor.ValidatePermissionAsync(Arg.Do<RbacValidationRequest>(r => capturedRequest = r))
            .Returns(Task.FromResult(new ActorValidationResponse(true)));
        factory.CreateActorProxy<IRbacValidatorActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act
        _ = await validator.ValidateAsync(user, TenantId, Domain, MessageType, "query", CancellationToken.None);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.MessageCategory.ShouldBe("query");
    }

    [Fact]
    public async Task ValidateAsync_ForwardsDomainAndMessageType() {
        // Arrange
        (ActorRbacValidator validator, IActorProxyFactory factory) = CreateValidator();
        IRbacValidatorActor actor = Substitute.For<IRbacValidatorActor>();
        RbacValidationRequest? capturedRequest = null;
        actor.ValidatePermissionAsync(Arg.Do<RbacValidationRequest>(r => capturedRequest = r))
            .Returns(Task.FromResult(new ActorValidationResponse(true)));
        factory.CreateActorProxy<IRbacValidatorActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act
        _ = await validator.ValidateAsync(user, TenantId, "Inventory", "AdjustStock", MessageCategory, CancellationToken.None);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.Domain.ShouldBe("Inventory");
        capturedRequest.MessageType.ShouldBe("AdjustStock");
    }

    [Fact]
    public async Task ValidateAsync_PassesThroughUnknownMessageCategory() {
        // Arrange — proxy does NOT validate messageCategory (actor's responsibility)
        (ActorRbacValidator validator, IActorProxyFactory factory) = CreateValidator();
        IRbacValidatorActor actor = Substitute.For<IRbacValidatorActor>();
        RbacValidationRequest? capturedRequest = null;
        actor.ValidatePermissionAsync(Arg.Do<RbacValidationRequest>(r => capturedRequest = r))
            .Returns(Task.FromResult(new ActorValidationResponse(true)));
        factory.CreateActorProxy<IRbacValidatorActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act
        _ = await validator.ValidateAsync(user, TenantId, Domain, MessageType, "notification", CancellationToken.None);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.MessageCategory.ShouldBe("notification");
    }

    [Fact]
    public async Task ValidateAsync_ChecksCancellationBeforeActorCall() {
        // Arrange
        (ActorRbacValidator validator, _) = CreateValidator();
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => validator.ValidateAsync(user, TenantId, Domain, MessageType, MessageCategory, cts.Token));
    }

    [Fact]
    public async Task ValidateAsync_ServiceUnavailableExceptionContainsRetryAfterFromOptions() {
        // Arrange
        (ActorRbacValidator validator, IActorProxyFactory factory) = CreateValidator(retryAfterSeconds: 60);
        SetupActorProxyThrows(factory, new TimeoutException("Timed out"));
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act & Assert
        AuthorizationServiceUnavailableException ex = await Should.ThrowAsync<AuthorizationServiceUnavailableException>(
            () => validator.ValidateAsync(user, TenantId, Domain, MessageType, MessageCategory, CancellationToken.None));

        ex.RetryAfterSeconds.ShouldBe(60);
    }

    [Fact]
    public async Task ValidateAsync_PassesTenantIdAsActorId() {
        // Arrange
        (ActorRbacValidator validator, IActorProxyFactory factory) = CreateValidator();
        SetupActorProxy(factory, new ActorValidationResponse(true));
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act
        _ = await validator.ValidateAsync(user, "my-tenant", Domain, MessageType, MessageCategory, CancellationToken.None);

        // Assert
        factory.Received(1).CreateActorProxy<IRbacValidatorActor>(
            Arg.Is<ActorId>(id => id.GetId() == "my-tenant"),
            Arg.Any<string>());
    }
}
