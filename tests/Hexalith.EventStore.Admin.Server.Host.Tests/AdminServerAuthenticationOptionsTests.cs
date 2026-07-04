using Hexalith.EventStore.Admin.Server.Host.Authentication;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Host.Tests;

public class AdminServerAuthenticationOptionsTests {
    private static IHostEnvironment CreateEnvironment(string environmentName) {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName = environmentName;
        return environment;
    }

    [Fact]
    public void DefaultValues_DisallowInsecureSymmetricKey() {
        var options = new AdminServerAuthenticationOptions();

        options.AllowInsecureSymmetricKey.ShouldBeFalse();
    }

    [Fact]
    public void Validate_SigningKeyOnlyInDevelopment_Succeeds() {
        var validator = new ValidateAdminServerAuthenticationOptions(CreateEnvironment(Environments.Development));
        var options = new AdminServerAuthenticationOptions {
            SigningKey = "this-is-a-valid-signing-key-at-least-32-characters!!",
            Issuer = "test-issuer",
            Audience = "test-audience",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_SigningKeyOnlyOutsideDevelopment_Fails() {
        var validator = new ValidateAdminServerAuthenticationOptions(CreateEnvironment(Environments.Production));
        var options = new AdminServerAuthenticationOptions {
            SigningKey = "this-is-a-valid-signing-key-at-least-32-characters!!",
            Issuer = "test-issuer",
            Audience = "test-audience",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Authority");
        result.FailureMessage.ShouldContain("AllowInsecureSymmetricKey");
    }

    [Fact]
    public void Validate_SigningKeyOnlyOutsideDevelopmentWithOverride_Succeeds() {
        var validator = new ValidateAdminServerAuthenticationOptions(CreateEnvironment(Environments.Production));
        var options = new AdminServerAuthenticationOptions {
            SigningKey = "this-is-a-valid-signing-key-at-least-32-characters!!",
            Issuer = "test-issuer",
            Audience = "test-audience",
            AllowInsecureSymmetricKey = true,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_AuthorityOnlyOutsideDevelopment_Succeeds() {
        var validator = new ValidateAdminServerAuthenticationOptions(CreateEnvironment(Environments.Production));
        var options = new AdminServerAuthenticationOptions {
            Authority = "https://login.example.com",
            Issuer = "test-issuer",
            Audience = "test-audience",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }
}
