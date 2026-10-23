namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Purpose of current authorization before any protected observation or effect.</summary>
public enum ContentOperation
{
    /// <summary>Ingest actual bytes.</summary>
    Upload,
    /// <summary>Prepare an authoritative command's bindings.</summary>
    Prepare,
    /// <summary>Resolve publication against retained command state.</summary>
    Recover,
    /// <summary>Read an exact version.</summary>
    Read,
    /// <summary>Apply governed body disposition.</summary>
    Disposition,
    /// <summary>Inspect actual upload bytes for a trusted internal mutation preparation; independent of ordinary reads.</summary>
    InspectUpload,
}
