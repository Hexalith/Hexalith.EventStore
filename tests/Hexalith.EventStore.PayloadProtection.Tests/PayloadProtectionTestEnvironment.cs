using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>An exact environment-name fixture for registration checks.</summary>
internal sealed class PayloadProtectionTestEnvironment(string environmentName) : IHostEnvironment
{
    /// <summary>Gets or sets the exact environment.</summary>
    public string EnvironmentName { get; set; } = environmentName;
    /// <summary>Gets or sets the application name.</summary>
    public string ApplicationName { get; set; } = "fixture";
    /// <summary>Gets or sets the content path.</summary>
    public string ContentRootPath { get; set; } = ".";
    /// <summary>Gets or sets the unused file provider.</summary>
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
