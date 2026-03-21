
using Hexalith.EventStore.CommandApi.Authentication;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authentication;

public class EventStoreAuthenticationOptionsTests {
    private readonly ValidateEventStoreAuthenticationOptions _validator = new();

    [Fact]
    public void DefaultValues_AreEmpty() {
        // Arrange (5.4.1 — verify default property values)
        var options = new EventStoreAuthenticationOptions();

        // Assert
        options.Authority.ShouldBeNull();
        options.SigningKey.ShouldBeNull();
        options.Issuer.ShouldBe(string.Empty);
        options.Audience.ShouldBe(string.Empty);
        options.RequireHttpsMetadata.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MissingBothAuthorityAndSigningKey_Fails() {
        // Arrange (5.4.2 — rejects missing both Authority and SigningKey)
        var options = new EventStoreAuthenticationOptions {
            Issuer = "test-issuer",
            Audience = "test-audience",
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Authority");
        result.FailureMessage.ShouldContain("SigningKey");
    }

    [Fact]
    public void Validate_ShortSigningKey_Fails() {
        // Arrange (5.4.3 — rejects SigningKey < 32 chars)
        var options = new EventStoreAuthenticationOptions {
            SigningKey = "too-short",
            Issuer = "test-issuer",
            Audience = "test-audience",
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("32 characters");
    }

    [Fact]
    public void Validate_AuthorityOnly_Succeeds() {
        // Arrange (5.4.4 — accepts Authority-only config)
        var options = new EventStoreAuthenticationOptions {
            Authority = "https://login.example.com",
            Issuer = "test-issuer",
            Audience = "test-audience",
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_SigningKeyOnly_Succeeds() {
        // Arrange (5.4.5 — accepts SigningKey-only config with valid length)
        var options = new EventStoreAuthenticationOptions {
            SigningKey = "this-is-a-valid-signing-key-at-least-32-characters!!",
            Issuer = "test-issuer",
            Audience = "test-audience",
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MissingIssuer_Fails() {
        // Arrange — Issuer is required
        var options = new EventStoreAuthenticationOptions {
            Authority = "https://login.example.com",
            Audience = "test-audience",
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Issuer");
    }

    [Fact]
    public void Validate_MissingAudience_Fails() {
        // Arrange — Audience is required
        var options = new EventStoreAuthenticationOptions {
            Authority = "https://login.example.com",
            Issuer = "test-issuer",
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Audience");
    }

    [Fact]
    public void Validate_NullOptions_ThrowsArgumentNullException() =>
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
}
