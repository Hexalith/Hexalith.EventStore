namespace Hexalith.EventStore.Contracts.Effects;

/// <summary>Versioned, immutable names and ordinals for currently known effect families.</summary>
public static class EffectKindCatalog
{
    /// <summary>Gets the catalog version.</summary>
    public const int Version = 1;

    /// <summary>Resumes a parent after a child completes.</summary>
    public const string ChildCompletionResume = "works.child-completion-resume.v1";

    /// <summary>Resumes an item after its date reminder fires.</summary>
    public const string DateResume = "works.date-resume.v1";

    /// <summary>Cancels one descendant after a parent cancellation.</summary>
    public const string CascadeCancel = "works.cascade-cancel.v1";

    /// <summary>Expires one descendant after a parent expiration.</summary>
    public const string CascadeExpire = "works.cascade-expire.v1";

    /// <summary>Registers a child relationship.</summary>
    public const string Registry = "works.registry.v1";

    /// <summary>Expires an item after its expiry reminder fires.</summary>
    public const string Expiry = "works.expiry.v1";

    /// <summary>Attaches a child discovered after its parent changed.</summary>
    public const string LateAttach = "works.late-attach.v1";

    /// <summary>Gets the fixed ordinal for a single target role in version one.</summary>
    public const long PrimaryTargetOrdinal = 0;

    /// <summary>Returns whether this version knows the given family.</summary>
    public static bool IsKnown(string kind)
        => kind is ChildCompletionResume or DateResume or CascadeCancel or CascadeExpire
            or Registry or Expiry or LateAttach;
}
