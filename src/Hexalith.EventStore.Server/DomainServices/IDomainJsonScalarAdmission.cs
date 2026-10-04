namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>Admits newly available bytes of one private cumulative JSON scalar before token-owner growth.</summary>
internal interface IDomainJsonScalarAdmission
{
    /// <summary>Checks only bytes not previously observed; no borrowed slice may be retained.</summary>
    void Observe(ReadOnlySpan<byte> cumulativeJsonScalar);
}
