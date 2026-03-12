
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

    private static (ActorTenantValidator validator, IActorProxyFactory factory) CreateValidator(
        int retryAfterSeconds = 5) {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IOptions<EventStoreAuthorizationOptions> options = Options.Create(
            new EventStoreAuthorizationOptions {
                TenantValidatorActorName = ActorType,
                RetryAfterSeconds = retryAfterSeconds,
            });
        var logger = NullLoggerFactory.Instance.CreateLogger<ActorTenantValidator>();
        var validator = new ActorTenantValidator(factory, options, logger);
        return (validator, factory);
    }

    private static void SetupActorProxy(
        IActorProxyFactory factory,
        ActorValidationResponse? response) {
        ITenantValidatorActor actor = Substitute.For<ITenantValidatorActor>();
        actor.ValidateTenantAccessAsync(Arg.Any<TenantValidationRequest>())
            .Returns(Task.FromResult(response)!);

        factory.CreateActorProxy<ITenantValidatorActor>(
            Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);
    }

    private static void SetupActorProxyThrows(
        IActorProxyFactory factory,
        Exception exception) {
        ITenantValidatorActor actor = Substitute.For<ITenantValidatorActor>();
        actor.ValidateTenantAccessAsync(Arg.Any<TenantValidationRequest>())
            .Throws(exception);

        factory.CreateActorProxy<ITenantValidatorActor>(
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
        ex.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task ValidateAsync_ExtractsUserIdFromNameIdentifierClaim() {
        // Arrange
        (ActorTenantValidator validator, IActorProxyFactory factory) = CreateValidator();
        ITenantValidatorActor actor = Substitute.For<ITenantValidatorActor>();
        TenantValidationRequest? capturedRequest = null;
        actor.ValidateTenantAccessAsync(Arg.Do<TenantValidationRequest>(r => capturedRequest = r))
            .Returns(Task.FromResult(new ActorValidationResponse(true)));
        factory.CreateActorProxy<ITenantValidatorActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(actor);
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier("specific-user-id");

        // Act
        _ = await validator.ValidateAsync(user, TenantId, CancellationToken.None);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest.UserId.ShouldBe("specific-user-id");
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
        factory.Received(1).CreateActorProxy<ITenantValidatorActor>(
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
        factory.Received(1).CreateActorProxy<ITenantValidatorActor>(
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
    public async Task ValidateAsync_ServiceUnavailableExceptionContainsRetryAfterFromOptions() {
        // Arrange
        (ActorTenantValidator validator, IActorProxyFactory factory) = CreateValidator(retryAfterSeconds: 30);
        SetupActorProxyThrows(factory, new TimeoutException("Timed out"));
        ClaimsPrincipal user = CreatePrincipalWithNameIdentifier(UserId);

        // Act & Assert
        AuthorizationServiceUnavailableException ex = await Should.ThrowAsync<AuthorizationServiceUnavailableException>(
            () => validator.ValidateAsync(user, TenantId, CancellationToken.None));

        ex.RetryAfterSeconds.ShouldBe(30);
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
