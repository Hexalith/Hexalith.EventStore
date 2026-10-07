using System.Security.Cryptography;

using Dapr.Client;

using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

public class ProjectionChangeNotifierOptionsTests {
    private readonly ValidateProjectionChangeNotifierOptions _validator = new();

    [Fact]
    public void DefaultValues_AreCorrect() {
        var options = new ProjectionChangeNotifierOptions();

        options.PubSubName.ShouldBe(ProjectionChangeNotifierOptions.DefaultPubSubName);
        options.Transport.ShouldBe(ProjectionChangeTransport.Direct);
        options.MaxDetailMetadataEntries.ShouldBe(ProjectionChangeNotifierOptions.DefaultMaxDetailMetadataEntries);
        options.MaxDetailMetadataBytes.ShouldBe(ProjectionChangeNotifierOptions.DefaultMaxDetailMetadataBytes);
    }

    [Fact]
    public void Validation_DefaultDirectTransport_Succeeds() {
        var options = new ProjectionChangeNotifierOptions();

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    /// <summary>Story 5.5: pub/sub is valid only with an issuer that binds provenance to tenant, projection, and topic.</summary>
    [Fact]
    public void Validation_PubSubTransportWithBindingIssuer_Succeeds() {
        var validator = new ValidateProjectionChangeNotifierOptions(Issuer(canBind: true));

        ValidateOptionsResult result = validator.Validate(null, new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.PubSub });

        result.Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// Story 5.5: without a binding issuer (no issuer, or an OIDC authority's client-credentials issuer) every pub/sub
    /// notification would be denied as unbound, so the transport is refused.
    /// </summary>
    /// <param name="hasIssuer">Whether a non-binding issuer is registered.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Validation_PubSubTransportWithoutBindingIssuer_Fails(bool hasIssuer) {
        var validator = new ValidateProjectionChangeNotifierOptions(hasIssuer ? Issuer(canBind: false) : null);

        ValidateOptionsResult result = validator.Validate(null, new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.PubSub });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Transport=Direct");
    }

    /// <summary>
    /// Story 5.5, through the real <c>AddEventStoreServer</c> wiring: an OIDC-authority EventStore refuses a configured
    /// pub/sub transport at startup, while the symmetric Development issuer can bind and is accepted.
    /// </summary>
    /// <param name="authorityMode">Whether the shared JWT contract names an OIDC authority instead of a signing key.</param>
    /// <param name="expectValid">Whether the pub/sub configuration must validate.</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AddEventStoreServer_PubSubTransport_RequiresABindingIssuer(bool authorityMode, bool expectValid) {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["EventStore:ProjectionChanges:Transport"] = nameof(ProjectionChangeTransport.PubSub),
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
        };
        if (authorityMode) {
            settings["Authentication:JwtBearer:Authority"] = "https://identity.example.test/realms/hexalith";
            settings["Authentication:JwtBearer:Issuer"] = "https://identity.example.test/realms/hexalith";
            settings["Authentication:JwtBearer:AllowedAlgorithms:0"] = "RS256";
            settings["Authentication:JwtBearer:RequireHttpsMetadata"] = "true";
            settings["Authentication:WorkloadIssuer:ClientId"] = "eventstore";
            settings["Authentication:WorkloadIssuer:ClientSecret"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        }
        else {
            settings["Authentication:JwtBearer:Issuer"] = "hexalith-dev";
            settings["Authentication:JwtBearer:SigningKey"] = AuthenticationTestEnvironment.SigningKey;
            settings["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256";
            settings["Authentication:JwtBearer:RequireHttpsMetadata"] = "false";
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(configuration);
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(authorityMode ? Environments.Production : Environments.Development);
        environment.ApplicationName.Returns("Hexalith.EventStore");
        _ = services.AddSingleton(environment);
        _ = services.AddSingleton(Substitute.For<DaprClient>());
        _ = services.AddEventStoreServer(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        if (expectValid) {
            provider.GetRequiredService<IOptions<ProjectionChangeNotifierOptions>>().Value.Transport.ShouldBe(ProjectionChangeTransport.PubSub);
            return;
        }

        OptionsValidationException failure = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ProjectionChangeNotifierOptions>>().Value);
        failure.Message.ShouldContain("Transport=Direct");
    }

    private static IWorkloadAssertionIssuer Issuer(bool canBind) {
        IWorkloadAssertionIssuer issuer = Substitute.For<IWorkloadAssertionIssuer>();
        _ = issuer.CanBindResources.Returns(canBind);
        return issuer;
    }

    [Fact]
    public void Validation_PubSubTransportWithCustomComponent_Fails() {
        var options = new ProjectionChangeNotifierOptions {
            PubSubName = "custom-pubsub",
            Transport = ProjectionChangeTransport.PubSub,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(ProjectionChangeNotifierOptions.DefaultPubSubName);
    }

    [Fact]
    public void Validation_DirectTransportWithCustomComponent_Succeeds() {
        var options = new ProjectionChangeNotifierOptions {
            PubSubName = "custom-pubsub",
            Transport = ProjectionChangeTransport.Direct,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validation_UndefinedTransport_Fails() {
        var options = new ProjectionChangeNotifierOptions {
            Transport = (ProjectionChangeTransport)999,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("transport");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validation_InvalidMetadataEntryLimit_Fails(int maxEntries) {
        var options = new ProjectionChangeNotifierOptions {
            MaxDetailMetadataEntries = maxEntries,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("MaxDetailMetadataEntries");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validation_InvalidMetadataByteLimit_Fails(int maxBytes) {
        var options = new ProjectionChangeNotifierOptions {
            MaxDetailMetadataBytes = maxBytes,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("MaxDetailMetadataBytes");
    }
}
