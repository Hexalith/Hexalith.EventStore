using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ServiceDefaults;

/// <summary>Runs the privacy guard after every caller post-configuration and before standard validation.</summary>
/// <param name="configurations">The registered logging configurations.</param>
/// <param name="postConfigurations">The registered logging post-configurations.</param>
/// <param name="validations">The registered logging validations.</param>
internal sealed class RequestLogPrivacyOptionsFactory(
    IEnumerable<IConfigureOptions<LoggerFilterOptions>> configurations,
    IEnumerable<IPostConfigureOptions<LoggerFilterOptions>> postConfigurations,
    IEnumerable<IValidateOptions<LoggerFilterOptions>> validations) : IOptionsFactory<LoggerFilterOptions>
{
    private readonly OptionsFactory<LoggerFilterOptions> _factory = new(configurations,
        postConfigurations.Append(new PostConfigureOptions<LoggerFilterOptions>(null, Extensions.GuardRequestDiagnostics)),
        validations);

    /// <inheritdoc/>
    public LoggerFilterOptions Create(string name) => _factory.Create(name);
}
