using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Tests.Configuration;

public class AdminServerOptionsValidatorTests
{
    private readonly AdminServerOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidOptions_ReturnsSuccess()
    {
        var options = new AdminServerOptions();
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptyStateStoreName_ReturnsFail(string value)
    {
        var options = new AdminServerOptions { StateStoreName = value };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AdminServerOptions.StateStoreName));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptyCommandApiAppId_ReturnsFail(string value)
    {
        var options = new AdminServerOptions { CommandApiAppId = value };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AdminServerOptions.CommandApiAppId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptyTenantServiceAppId_ReturnsFail(string value)
    {
        var options = new AdminServerOptions { TenantServiceAppId = value };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AdminServerOptions.TenantServiceAppId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidMaxTimelineEvents_ReturnsFail(int value)
    {
        var options = new AdminServerOptions { MaxTimelineEvents = value };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AdminServerOptions.MaxTimelineEvents));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidServiceInvocationTimeout_ReturnsFail(int value)
    {
        var options = new AdminServerOptions { ServiceInvocationTimeoutSeconds = value };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AdminServerOptions.ServiceInvocationTimeoutSeconds));
    }

    [Fact]
    public void Validate_MultipleFailures_ReportsAll()
    {
        var options = new AdminServerOptions
        {
            StateStoreName = "",
            CommandApiAppId = "",
            MaxTimelineEvents = 0,
        };
        ValidateOptionsResult result = _validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.Failures.ShouldNotBeNull();
        result.Failures!.Count().ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Validate_NullOptions_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
    }
}
