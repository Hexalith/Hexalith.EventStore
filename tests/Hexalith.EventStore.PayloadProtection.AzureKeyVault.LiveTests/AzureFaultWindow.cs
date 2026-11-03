// Approved Azure exception section 7: separately authorized externally prepared fault observations.
namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests;
/// <summary>One exact operation/fault interval prepared by the separately authorized isolated-environment operator.</summary>
internal sealed record AzureFaultWindow(string Fault, DateTimeOffset NotBeforeUtc, DateTimeOffset NotAfterUtc);
