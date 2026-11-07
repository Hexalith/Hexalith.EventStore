// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// Base authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e, sections 5.2, 15.
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Constructive, bounded diagnostics for disabled registration.</summary>
internal static partial class PayloadProtectionRegistrationLog
{
    /// <summary>Reports unused configuration without formatting its contents.</summary>
    [LoggerMessage(EventId = 8770, Level = LogLevel.Information, Message = "Payload protection is disabled; backend configuration is unused.")]
    internal static partial void DisabledConfiguration(ILogger logger);
}
