using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Tests.Configuration;

public class AdminServerOptionsValidatorTests {
    private readonly AdminServerOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidOptions_ReturnsSuccess() {
        var options = new AdminServerOptions();
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptyStateStoreName_ReturnsFail(string value) {
        var options = new AdminServerOptions { StateStoreName = value };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AdminServerOptions.StateStoreName));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptyEventStoreAppId_ReturnsFail(string value) {
        var options = new AdminServerOptions { EventStoreAppId = value };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AdminServerOptions.EventStoreAppId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptyTenantServiceAppId_ReturnsFail(string value) {
        var options = new AdminServerOptions { TenantServiceAppId = value };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AdminServerOptions.TenantServiceAppId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidMaxTimelineEvents_ReturnsFail(int value) {
        var options = new AdminServerOptions { MaxTimelineEvents = value };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AdminServerOptions.MaxTimelineEvents));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidServiceInvocationTimeout_ReturnsFail(int value) {
        var options = new AdminServerOptions { ServiceInvocationTimeoutSeconds = value };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AdminServerOptions.ServiceInvocationTimeoutSeconds));
    }

    [Fact]
    public void Validate_MultipleFailures_ReportsAll() {
        var options = new AdminServerOptions {
            StateStoreName = "",
            EventStoreAppId = "",
            MaxTimelineEvents = 0,
        };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        _ = result.Failures.ShouldNotBeNull();
        result.Failures!.Count().ShouldBeGreaterThanOrEqualTo(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidMaxBlameEvents_ReturnsFail(int value) {
        var options = new AdminServerOptions { MaxBlameEvents = value };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AdminServerOptions.MaxBlameEvents));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidMaxBlameFields_ReturnsFail(int value) {
        var options = new AdminServerOptions { MaxBlameFields = value };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AdminServerOptions.MaxBlameFields));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidMaxBisectSteps_ReturnsFail(int value) {
        var options = new AdminServerOptions { MaxBisectSteps = value };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AdminServerOptions.MaxBisectSteps));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidMaxBisectFields_ReturnsFail(int value) {
        var options = new AdminServerOptions { MaxBisectFields = value };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AdminServerOptions.MaxBisectFields));
    }

    [Fact]
    public void Validate_NullOptions_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
}
