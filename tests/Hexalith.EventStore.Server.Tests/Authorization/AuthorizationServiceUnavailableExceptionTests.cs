
using Hexalith.EventStore.CommandApi.ErrorHandling;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authorization;

public class AuthorizationServiceUnavailableExceptionTests {
    [Fact]
    public void Constructor_Full_SetsAllProperties() {
        // Arrange
        var inner = new HttpRequestException("Connection refused");

        // Act
        var ex = new AuthorizationServiceUnavailableException(
            "TenantValidatorActor", "tenant-123", "Actor unreachable", inner);

        // Assert
        ex.ActorTypeName.ShouldBe("TenantValidatorActor");
        ex.ActorId.ShouldBe("tenant-123");
        ex.Reason.ShouldBe("Actor unreachable");
        ex.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void Constructor_Full_MessageContainsActorTypeAndId() {
        // Arrange & Act
        var ex = new AuthorizationServiceUnavailableException(
            "MyActor", "id-1", "Timed out", new TimeoutException());

        // Assert
        ex.Message.ShouldContain("MyActor");
        ex.Message.ShouldContain("id-1");
        ex.Message.ShouldContain("Timed out");
    }

    [Fact]
    public void Constructor_Parameterless_SetsDefaults() {
        // Act
        var ex = new AuthorizationServiceUnavailableException();

        // Assert
        ex.ActorTypeName.ShouldBe(string.Empty);
        ex.ActorId.ShouldBe(string.Empty);
        ex.Reason.ShouldBe(string.Empty);
        ex.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_Message_SetsMessageAndReason() {
        // Act
        var ex = new AuthorizationServiceUnavailableException("Something went wrong");

        // Assert
        ex.Message.ShouldBe("Something went wrong");
        ex.Reason.ShouldBe("Something went wrong");
    }

    [Fact]
    public void Constructor_MessageAndInner_SetsProperties() {
        // Arrange
        var inner = new InvalidOperationException("Oops");

        // Act
        var ex = new AuthorizationServiceUnavailableException("Wrapper message", inner);

        // Assert
        ex.Message.ShouldBe("Wrapper message");
        ex.Reason.ShouldBe("Wrapper message");
        ex.InnerException.ShouldBe(inner);
    }
}
