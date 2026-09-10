
using System.Security.Cryptography;

using Hexalith.EventStore.Authentication;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authentication;

public class EventStoreAuthenticationOptionsTests {
    private static readonly string s_signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private readonly ValidateEventStoreAuthenticationOptions _validator = new(CreateEnvironment(Environments.Development));

    private static IHostEnvironment CreateEnvironment(string environmentName) {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName = environmentName;
        return environment;
    }

    [Fact]
    public void DefaultValues_AreEmpty() {
        // Arrange (5.4.1 — verify default property values)
        var options = new EventStoreAuthenticationOptions();

        // Assert
        options.Authority.ShouldBeNull();
        options.SigningKey.ShouldBeNull();
        options.Issuer.ShouldBe(string.Empty);
        options.Audience.ShouldBe(string.Empty);
        options.AllowedAlgorithms.ShouldBeEmpty();
        options.RequireHttpsMetadata.ShouldBeTrue();
        options.AllowInsecureSymmetricKey.ShouldBeFalse();
    }

    [Fact]
    public void Validate_MissingBothAuthorityAndSigningKey_Fails() {
        // Arrange (5.4.2 — rejects missing both Authority and SigningKey)
        var options = new EventStoreAuthenticationOptions {
            Issuer = "test-issuer",
            Audience = "test-audience",
            AllowedAlgorithms = ["RS256"],
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("exactly one");
    }

    [Fact]
    public void Validate_ShortSigningKey_Fails() {
        // Arrange (5.4.3 — rejects SigningKey < 32 bytes)
        var options = new EventStoreAuthenticationOptions {
            SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(8)),
            Issuer = "test-issuer",
            Audience = "test-audience",
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("32 bytes");
    }

    [Fact]
    public void Validate_AuthorityOnly_Succeeds() {
        // Arrange (5.4.4 — accepts Authority-only config)
        var options = new EventStoreAuthenticationOptions {
            Authority = "https://login.example.com",
            Issuer = "test-issuer",
            Audience = "test-audience",
            AllowedAlgorithms = ["RS256"],
        };

        // Act
        ValidateOptionsResult result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_SigningKeyOnly_Succeeds() {
        // Arrange (5.4.5 — accepts SigningKey-only config with valid length in Development)
        var options = new EventStoreAuthenticationOptions {
            SigningKey = s_signingKey,
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
    public void Validate_SigningKeyOnly_OutsideDevelopment_Fails() {
        // Arrange — the symmetric dev-key path must be rejected outside Development
        var validator = new ValidateEventStoreAuthenticationOptions(CreateEnvironment(Environments.Production));
        var options = new EventStoreAuthenticationOptions {
            SigningKey = s_signingKey,
            Issuer = "test-issuer",
            Audience = "test-audience",
        };

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("forbidden in Production");
    }

    [Fact]
    public void Validate_SigningKeyOnly_ProductionWithOverride_Fails() {
        // Arrange — Production never permits the legacy symmetric override
        var validator = new ValidateEventStoreAuthenticationOptions(CreateEnvironment(Environments.Production));
        var options = new EventStoreAuthenticationOptions {
            SigningKey = s_signingKey,
            Issuer = "test-issuer",
            Audience = "test-audience",
            AllowInsecureSymmetricKey = true,
        };

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("forbidden in Production");
    }

    [Fact]
    public void Validate_SigningKeyOnly_StagingWithOverride_Succeeds() {
        var logger = new RecordingLogger<ValidateEventStoreAuthenticationOptions>();
        var validator = new ValidateEventStoreAuthenticationOptions(CreateEnvironment(Environments.Staging), logger);
        var options = new EventStoreAuthenticationOptions {
            SigningKey = s_signingKey,
            Issuer = "test-issuer",
            Audience = "test-audience",
            AllowInsecureSymmetricKey = true,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
        logger.Messages.ShouldHaveSingleItem().ShouldContain("non-Production symmetric JWT exception");
        logger.Messages[0].ShouldNotContain(s_signingKey);
    }

    [Fact]
    public void Validate_SigningKeyOnly_StagingWithoutOverride_Fails() {
        var validator = new ValidateEventStoreAuthenticationOptions(CreateEnvironment(Environments.Staging));
        var options = new EventStoreAuthenticationOptions {
            SigningKey = s_signingKey,
            Issuer = "test-issuer",
            Audience = "test-audience",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Development-only");
        result.FailureMessage.ShouldNotContain(s_signingKey);
    }

    [Fact]
    public void Validate_AuthorityAndSigningKey_Fails() {
        var options = new EventStoreAuthenticationOptions {
            Authority = "https://login.example.com",
            SigningKey = s_signingKey,
            Issuer = "test-issuer",
            Audience = "test-audience",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("exactly one");
    }

    [Theory]
    [InlineData("login.example.com")]
    [InlineData("https://user" + "@example.com")]
    [InlineData("https://login.example.com?tenant=x")]
    [InlineData("https://login.example.com#realm")]
    public void Validate_InvalidAuthority_FailsWithoutEchoingValue(string authority) {
        var options = new EventStoreAuthenticationOptions {
            Authority = authority,
            Issuer = "test-issuer",
            Audience = "test-audience",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Authority");
        result.FailureMessage.ShouldNotContain(authority);
    }

    [Fact]
    public void Validate_HttpAuthorityOutsideDevelopment_Fails() {
        var validator = new ValidateEventStoreAuthenticationOptions(CreateEnvironment(Environments.Staging));
        var options = new EventStoreAuthenticationOptions {
            Authority = "http://login.example.com",
            Issuer = "test-issuer",
            Audience = "test-audience",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("HTTPS");
    }

    [Fact]
    public void Validate_DevelopmentHttpAuthorityWithHttpsMetadataRequired_Fails() {
        var options = new EventStoreAuthenticationOptions {
            Authority = "http://login.example.com",
            Issuer = "test-issuer",
            Audience = "test-audience",
            AllowedAlgorithms = ["RS256"],
            RequireHttpsMetadata = true,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("RequireHttpsMetadata");
    }

    [Fact]
    public void Validate_DevelopmentHttpAuthorityWithHttpsMetadataDisabled_Succeeds() {
        var options = new EventStoreAuthenticationOptions {
            Authority = "http://login.example.com",
            Issuer = "test-issuer",
            Audience = "test-audience",
            AllowedAlgorithms = ["RS256"],
            RequireHttpsMetadata = false,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_DisabledHttpsMetadataOutsideDevelopment_Fails() {
        var validator = new ValidateEventStoreAuthenticationOptions(CreateEnvironment(Environments.Staging));
        var options = new EventStoreAuthenticationOptions {
            Authority = "https://login.example.com",
            Issuer = "test-issuer",
            Audience = "test-audience",
            RequireHttpsMetadata = false,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("RequireHttpsMetadata");
    }

    [Fact]
    public void Validate_BlankAdditionalAudience_Fails() {
        var options = new EventStoreAuthenticationOptions {
            Authority = "https://login.example.com",
            Issuer = "test-issuer",
            ValidAudiences = ["test-audience", "   "],
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("non-blank");
    }

    [Fact]
    public void Validate_ValidAudiencesWithoutPrimaryAudience_Succeeds() {
        var options = new EventStoreAuthenticationOptions {
            Authority = "https://login.example.com",
            Issuer = "test-issuer",
            ValidAudiences = ["audience-a", "audience-b"],
            AllowedAlgorithms = ["RS256"],
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_AuthorityOnly_OutsideDevelopment_Succeeds() {
        // Arrange — OIDC Authority is the required production configuration
        var validator = new ValidateEventStoreAuthenticationOptions(CreateEnvironment(Environments.Production));
        var options = new EventStoreAuthenticationOptions {
            Authority = "https://login.example.com",
            Issuer = "test-issuer",
            Audience = "test-audience",
            AllowedAlgorithms = ["RS256"],
        };

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(InvalidAuthorityAlgorithms))]
    public void Validate_AuthorityWithInvalidAlgorithms_Fails(string[] algorithms)
    {
        var options = new EventStoreAuthenticationOptions
        {
            Authority = "https://login.example.com",
            Issuer = "test-issuer",
            Audience = "test-audience",
            AllowedAlgorithms = algorithms,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AllowedAlgorithms");
    }

    public static TheoryData<string[]> InvalidAuthorityAlgorithms
    {
        get
        {
            var data = new TheoryData<string[]>();
            data.Add(Array.Empty<string>());
            data.Add([""]);
            data.Add(["   "]);
            data.Add(["HS256"]);
            data.Add(["unknown"]);
            return data;
        }
    }

    [Fact]
    public void Validate_SymmetricModeWithAsymmetricAlgorithm_Fails() {
        var options = new EventStoreAuthenticationOptions {
            SigningKey = s_signingKey,
            Issuer = "test-issuer",
            Audience = "test-audience",
            AllowedAlgorithms = ["RS256"],
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("HS256");
    }

    [Fact]
    public void Validate_NullOptions_ThrowsArgumentNullException() =>
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));

    private sealed class RecordingLogger<T> : ILogger<T> {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
