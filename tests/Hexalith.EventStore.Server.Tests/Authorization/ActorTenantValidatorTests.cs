
using System.Security.Claims;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Configuration;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Actors.Authorization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authorization;

public class ActorTenantValidatorTests {
    private const string ActorType = "TestTenantValidatorActor";
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

    private static (ActorTenantValidator validator, IActorProxyFactory factory) CreateValidator() {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IOptions<EventStoreAuthorizationOptions> options = Options.Create(
            new EventStoreAuthorizationOptions {
                TenantValidatorActorName = ActorType,
            });
        ILogger<ActorTenantValidator> logger = NullLoggerFactory.Instance.CreateLogger<ActorTenantValidator>();
        var validator = new ActorTenantValidator(factory, options, logger);
        return (validator, factory);
    }

    private static void SetupActorProxy(
        IActorProxyFactory factory,
        ActorValidationResponse? response) {
        ITenantValidatorActor actor = Substitute.For<ITenantValidatorActor>();
        _ = actor.ValidateTenantAccessAsync(Arg.Any<TenantValidationRequest>())
            .Returns(Task.FromResult(response)!);

        _ = factory.CreateActorProxy<ITenantValidatorActor>(
            Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);
    }

    private static void SetupActorProxyThrows(
        IActorProxyFactory factory,
        Exception exception) {
        ITenantValidatorActor actor = Substitute.For<ITenantValidatorActor>();
        _ = actor.ValidateTenantAccessAsync(Arg.Any<TenantValidationRequest>())
            .Throws(exception);

        _ = factory.CreateActorProxy<ITenantValidatorActor>(
            Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);
    }

    [Fact]
    public async Task ValidateAsync_ActorAllows_ReturnsAllowed() {
        // Arrange
        (ActorTenantValidator validator, IActorProxyFactory factory) = CreateValidator();
        SetupActorProxy(factory, new ActorValidationResponse(true));
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act
        TenantValidationResult result = await validator.ValidateAsync(user, TenantId, CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ActorDenies_ReturnsDeniedWithReason() {
        // Arrange
        (ActorTenantValidator validator, IActorProxyFactory factory) = CreateValidator();
        SetupActorProxy(factory, new ActorValidationResponse(false, "Tenant membership revoked"));
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act
        TenantValidationResult result = await validator.ValidateAsync(user, TenantId, CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
        result.Reason.ShouldBe("Tenant membership revoked");
    }

    [Fact]
    public async Task ValidateAsync_ActorReturnsNull_ThrowsServiceUnavailable() {
        // Arrange
        (ActorTenantValidator validator, IActorProxyFactory factory) = CreateValidator();
        SetupActorProxy(factory, null);
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act & Assert
        AuthorizationServiceUnavailableException ex = await Should.ThrowAsync<AuthorizationServiceUnavailableException>(
            () => validator.ValidateAsync(user, TenantId, CancellationToken.None));

        ex.ActorTypeName.ShouldBe(ActorType);
        ex.ActorId.ShouldBe(TenantId);
        ex.Reason.ShouldContain("null");
    }

    [Fact]
    public async Task ValidateAsync_ActorUnreachable_ThrowsServiceUnavailable() {
        // Arrange
        (ActorTenantValidator validator, IActorProxyFactory factory) = CreateValidator();
        SetupActorProxyThrows(factory, new HttpRequestException("Connection refused"));
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act & Assert
        AuthorizationServiceUnavailableException ex = await Should.ThrowAsync<AuthorizationServiceUnavailableException>(
            () => validator.ValidateAsync(user, TenantId, CancellationToken.None));

        ex.ActorTypeName.ShouldBe(ActorType);
        ex.ActorId.ShouldBe(TenantId);
        _ = ex.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task ValidateAsync_ExtractsUserIdFromNameIdentifierClaim() {
        // Arrange
        (ActorTenantValidator validator, IActorProxyFactory factory) = CreateValidator();
        ITenantValidatorActor actor = Substitute.For<ITenantValidatorActor>();
        TenantValidationRequest? capturedRequest = null;
        _ = actor.ValidateTenantAccessAsync(Arg.Do<TenantValidationRequest>(r => capturedRequest = r))
            .Returns(Task.FromResult(new ActorValidationResponse(true)));
        _ = factory.CreateActorProxy<ITenantValidatorActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier("specific-user-id");

        // Act
        _ = await validator.ValidateAsync(user, TenantId, CancellationToken.None);

        // Assert
        _ = capturedRequest.ShouldNotBeNull();
        capturedRequest.UserId.ShouldBe("specific-user-id");
    }

    [Fact]
    public async Task ValidateAsync_ForwardsAggregateIdToActorRequest() {
        // Arrange
        (ActorTenantValidator validator, IActorProxyFactory factory) = CreateValidator();
        ITenantValidatorActor actor = Substitute.For<ITenantValidatorActor>();
        TenantValidationRequest? capturedRequest = null;
        _ = actor.ValidateTenantAccessAsync(Arg.Do<TenantValidationRequest>(r => capturedRequest = r))
            .Returns(Task.FromResult(new ActorValidationResponse(true)));
        _ = factory.CreateActorProxy<ITenantValidatorActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act
        _ = await validator.ValidateAsync(user, TenantId, CancellationToken.None, "order-123");

        // Assert
        _ = capturedRequest.ShouldNotBeNull();
        capturedRequest.AggregateId.ShouldBe("order-123");
    }

    [Fact]
    public async Task ValidateAsync_NoNameIdentifierClaim_ThrowsInvalidOperation() {
        // Arrange
        (ActorTenantValidator validator, _) = CreateValidator();
        ClaimsPrincipal user = CreatePrincipalWithoutNameIdentifier();

        // Act & Assert
        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => validator.ValidateAsync(user, TenantId, CancellationToken.None));

        ex.Message.ShouldContain("NameIdentifier");
    }

    [Fact]
    public async Task ValidateAsync_PassesTenantIdAsActorId() {
        // Arrange
        (ActorTenantValidator validator, IActorProxyFactory factory) = CreateValidator();
        SetupActorProxy(factory, new ActorValidationResponse(true));
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act
        _ = await validator.ValidateAsync(user, "my-tenant", CancellationToken.None);

        // Assert
        _ = factory.Received(1).CreateActorProxy<ITenantValidatorActor>(
            Arg.Is<ActorId>(id => id.GetId() == "my-tenant"),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ValidateAsync_UsesConfiguredActorTypeName() {
        // Arrange
        (ActorTenantValidator validator, IActorProxyFactory factory) = CreateValidator();
        SetupActorProxy(factory, new ActorValidationResponse(true));
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act
        _ = await validator.ValidateAsync(user, TenantId, CancellationToken.None);

        // Assert
        _ = factory.Received(1).CreateActorProxy<ITenantValidatorActor>(
            Arg.Any<ActorId>(),
            ActorType);
    }

    [Fact]
    public async Task ValidateAsync_ChecksCancellationBeforeActorCall() {
        // Arrange
        (ActorTenantValidator validator, _) = CreateValidator();
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => validator.ValidateAsync(user, TenantId, cts.Token));
    }

    [Fact]
    public async Task ValidateAsync_ServiceUnavailableExceptionPreservesDiagnostics() {
        // Arrange
        (ActorTenantValidator validator, IActorProxyFactory factory) = CreateValidator();
        SetupActorProxyThrows(factory, new TimeoutException("Timed out"));
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act & Assert
        AuthorizationServiceUnavailableException ex = await Should.ThrowAsync<AuthorizationServiceUnavailableException>(
            () => validator.ValidateAsync(user, TenantId, CancellationToken.None));

        ex.ActorTypeName.ShouldBe(ActorType);
        ex.ActorId.ShouldBe(TenantId);
        ex.Reason.ShouldBe("Timed out");
    }

    [Fact]
    public async Task ValidateAsync_ActorDeniesWithNullReason_ReturnsDefaultDenialMessage() {
        // Arrange
        (ActorTenantValidator validator, IActorProxyFactory factory) = CreateValidator();
        SetupActorProxy(factory, new ActorValidationResponse(false, null));
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act
        TenantValidationResult result = await validator.ValidateAsync(user, TenantId, CancellationToken.None);

        // Assert
        result.IsAuthorized.ShouldBeFalse();
        result.Reason.ShouldBe("Tenant access denied by actor.");
    }
}
