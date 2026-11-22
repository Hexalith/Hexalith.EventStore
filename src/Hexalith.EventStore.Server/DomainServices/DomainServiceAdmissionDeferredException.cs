namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>The authenticated owner refused admission before invoking its aggregate.</summary>
internal sealed class DomainServiceAdmissionDeferredException() : Exception("Command admission temporarily unavailable.")
{
    internal const string Reason = "owner_admission_deferred";
    internal const string Header = "X-Hexalith-Command-Admission";
}
