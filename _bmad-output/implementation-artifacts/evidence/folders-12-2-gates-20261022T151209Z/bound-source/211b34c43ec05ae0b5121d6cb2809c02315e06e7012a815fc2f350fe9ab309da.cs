namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Closed EventStore-owned technical migration/fence/guard protocol operations.</summary>
public enum GovernanceGuardOperation
{
    /// <summary>InstallEpoch exact owned transition.</summary>
    InstallEpoch = 1,
    /// <summary>InstallRepairFence exact owned transition.</summary>
    InstallRepairFence = 2,
    /// <summary>RecordBridgeDrain exact owned transition.</summary>
    RecordBridgeDrain = 3,
    /// <summary>ActivateSuccessor exact owned transition.</summary>
    ActivateSuccessor = 4,
    /// <summary>AppendWrite exact owned transition.</summary>
    AppendWrite = 5,
    /// <summary>AuthorizeAdmissionFence exact owned transition.</summary>
    AuthorizeAdmissionFence = 6,
    /// <summary>CommitAdmissionFence exact owned transition.</summary>
    CommitAdmissionFence = 7,
    /// <summary>RecordViolation exact owned transition.</summary>
    RecordViolation = 8,
    /// <summary>RecordOwnerCycleEffective exact owned transition.</summary>
    RecordOwnerCycleEffective = 9,
    /// <summary>AuthorizeContentBinding exact owned transition.</summary>
    AuthorizeContentBinding = 10,
    /// <summary>CommitContentBinding exact owned transition.</summary>
    CommitContentBinding = 11,
    /// <summary>RegisterHold exact owned transition.</summary>
    RegisterHold = 12,
    /// <summary>ReleaseHold exact owned transition.</summary>
    ReleaseHold = 13,
    /// <summary>AuthorizeDestructionStart exact owned transition.</summary>
    AuthorizeDestructionStart = 14,
    /// <summary>CommitDestructionStart exact owned transition.</summary>
    CommitDestructionStart = 15,
    /// <summary>AuthorizeContainment exact owned transition.</summary>
    AuthorizeContainment = 16,
    /// <summary>CommitContainment exact owned transition.</summary>
    CommitContainment = 17,
    /// <summary>RecordBatchIssued exact owned transition.</summary>
    RecordBatchIssued = 18,
    /// <summary>RecordBatchIssuanceStale exact owned transition.</summary>
    RecordBatchIssuanceStale = 19,
    /// <summary>AuthorizeDispatch exact owned transition.</summary>
    AuthorizeDispatch = 20,
    /// <summary>CommitDispatch exact owned transition.</summary>
    CommitDispatch = 21,
    /// <summary>RecordKeyCompromise exact owned transition.</summary>
    RecordKeyCompromise = 22,
    /// <summary>ReplaceAttestation exact owned transition.</summary>
    ReplaceAttestation = 23,
    /// <summary>RecordProtectionOutcome exact owned transition.</summary>
    RecordProtectionOutcome = 24,
    /// <summary>AuthorizeCompletion exact owned transition.</summary>
    AuthorizeCompletion = 25,
    /// <summary>CommitCompletion exact owned transition.</summary>
    CommitCompletion = 26,
}
