using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>Supplies only the configuration budget before explicit options callbacks, without enrolling a process runtime.</summary>
internal sealed class DomainServiceTerminalRefusalOptionsSetup(IConfiguration configuration) : IConfigureOptions<EventStoreProcessOptions>
{
    /// <inheritdoc/>
    public void Configure(EventStoreProcessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MaxCommandBytesPerEnvelope = configuration.GetValue<int?>(
            EventStoreProcessOptions.SectionName + ":" + nameof(EventStoreProcessOptions.MaxCommandBytesPerEnvelope))
            ?? options.MaxCommandBytesPerEnvelope;
    }
}
