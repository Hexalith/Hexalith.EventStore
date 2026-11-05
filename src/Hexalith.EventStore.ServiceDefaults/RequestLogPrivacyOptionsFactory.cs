using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.ServiceDefaults;

/// <summary>Runs the privacy guard after every caller post-configuration and before standard validation.</summary>
/// <param name="configurations">The registered logging configurations.</param>
/// <param name="postConfigurations">The registered logging post-configurations.</param>
/// <param name="validations">The registered logging validations.</param>
/// <param name="accessor">The current request used by the source-side read privacy policy.</param>
internal sealed class RequestLogPrivacyOptionsFactory(
    IEnumerable<IConfigureOptions<LoggerFilterOptions>> configurations,
    IEnumerable<IPostConfigureOptions<LoggerFilterOptions>> postConfigurations,
    IEnumerable<IValidateOptions<LoggerFilterOptions>> validations,
    IHttpContextAccessor accessor) : IOptionsFactory<LoggerFilterOptions>
{
    private readonly OptionsFactory<LoggerFilterOptions> _factory = new(configurations,
        postConfigurations.Append(new PostConfigureOptions<LoggerFilterOptions>(null, options => Extensions.GuardRequestDiagnostics(options, accessor))),
        validations);

    /// <inheritdoc/>
    public LoggerFilterOptions Create(string name) => _factory.Create(name);
}
