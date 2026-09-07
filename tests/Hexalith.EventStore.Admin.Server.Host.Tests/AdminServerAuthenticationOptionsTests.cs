using System.Security.Cryptography;

using Hexalith.EventStore.Admin.Server.Host.Authentication;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Host.Tests;

public class AdminServerAuthenticationOptionsTests {
    private static readonly string s_signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
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
            SigningKey = s_signingKey,
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
            SigningKey = s_signingKey,
            Issuer = "test-issuer",
            Audience = "test-audience",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("forbidden in Production");
    }

    [Fact]
    public void Validate_SigningKeyOnlyInProductionWithOverride_Fails() {
        var validator = new ValidateAdminServerAuthenticationOptions(CreateEnvironment(Environments.Production));
        var options = new AdminServerAuthenticationOptions {
            SigningKey = s_signingKey,
            Issuer = "test-issuer",
            Audience = "test-audience",
            AllowInsecureSymmetricKey = true,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("forbidden in Production");
    }

    [Fact]
    public void Validate_AuthorityOnlyOutsideDevelopment_Succeeds() {
        var validator = new ValidateAdminServerAuthenticationOptions(CreateEnvironment(Environments.Production));
        var options = new AdminServerAuthenticationOptions {
            Authority = "https://login.example.com",
            Issuer = "test-issuer",
            Audience = "test-audience",
            AllowedAlgorithms = ["RS256"],
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_AuthorityAlgorithmFailures_AreRejected()
    {
        var validator = new ValidateAdminServerAuthenticationOptions(CreateEnvironment(Environments.Production));
        string[][] invalidAlgorithms = [[], [""], ["   "], ["HS256"], ["unknown"]];

        foreach (string[] algorithms in invalidAlgorithms)
        {
            var options = new AdminServerAuthenticationOptions
            {
                Authority = "https://login.example.com",
                Issuer = "test-issuer",
                Audience = "test-audience",
                AllowedAlgorithms = algorithms,
            };

            ValidateOptionsResult result = validator.Validate(null, options);

            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldContain("AllowedAlgorithms");
        }
    }
}
