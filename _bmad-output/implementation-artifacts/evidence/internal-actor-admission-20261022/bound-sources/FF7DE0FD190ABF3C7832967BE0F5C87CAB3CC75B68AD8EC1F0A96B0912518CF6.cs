using System.Runtime.Serialization;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Defines the persisted crash-resumable digest-key promotion phase.</summary>
[DataContract]
public enum IdempotencyAdmissionPromotionPhase
{
    /// <summary>The directory has one stable canonical actor.</summary>
    [EnumMember]
    Stable,

    /// <summary>The target must durably prepare and acknowledge the copied record.</summary>
    [EnumMember]
    PrepareTarget,

    /// <summary>The source must durably redirect to the acknowledged target.</summary>
    [EnumMember]
    RedirectSource,

    /// <summary>The directory must atomically flip its canonical pointer.</summary>
    [EnumMember]
    FlipDirectory,

    /// <summary>The target must activate only after the directory flip.</summary>
    [EnumMember]
    ActivateTarget,
}
