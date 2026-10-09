using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Aggregates;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Retains all private page/response/staging charges through save, readback and any unresolved uncertainty.</summary>
internal sealed class DaprReplayPreparedTransition : IDisposable
{
    private readonly EventBufferReservation _stagingCharge;

    /// <summary>Takes already admitted ownership of one candidate transition.</summary>
    internal DaprReplayPreparedTransition(DaprLogicalReplayPage page, DaprLogicalResponseOwner response,
        EventBufferReservation stagingCharge, EventBufferBudget budget)
    {
        Page = page;
        Response = response;
        _stagingCharge = stagingCharge;
        Budget = budget;
    }

    /// <summary>Gets the prepared signed logical page.</summary>
    internal DaprLogicalReplayPage Page
    {
        get;
    }

    /// <summary>Gets the exact privately retained response.</summary>
    internal DaprLogicalResponseOwner Response
    {
        get;
    }

    /// <summary>Gets the one composed memory budget.</summary>
    internal EventBufferBudget Budget
    {
        get;
    }

    /// <summary>Gets private last-good canonical state retained through uncertain commit.</summary>
    internal DaprLogicalResponseOwner? PriorState
    {
        get; private set;
    }

    /// <summary>Gets private candidate canonical state retained through uncertain commit.</summary>
    internal DaprLogicalResponseOwner? CanonicalState
    {
        get; private set;
    }

    /// <summary>Gets the fully admitted private intake retained before the first state callback.</summary>
    internal PrivateLogicalReplayPage? Intake
    {
        get; private set;
    }

    /// <summary>Gets the parent-charged conservative future capacity retained through every child owner.</summary>
    internal EventBufferBudget? ReconstructionBudget
    {
        get; private set;
    }

    private EventBufferReservation? _stateStaging;
    private EventBufferReservation? _proofStaging;

    /// <summary>Gets admitted commitment/proof workspace retained before source callbacks and through uncertainty.</summary>
    internal EventBufferBudget? CommitmentBudget { get; private set; }
    /// <summary>Gets the private terminal command proof retained through its atomic save and readback.</summary>
    internal DaprLogicalResponseOwner? CommandProof { get; private set; }
    /// <summary>Gets exact expected and predecessor participant digests for independent save classification.</summary>
    internal byte[]? ExpectedParticipantDigest { get; set; }
    /// <summary>Gets the exact predecessor participant digest retained before staging.</summary>
    internal byte[]? PriorParticipantDigest { get; set; }

    /// <summary>Takes the already reserved parent partition before further callbacks or proof allocation.</summary>
    internal void AttachCommitments(EventBufferBudget budget) => CommitmentBudget = budget;

    /// <summary>Takes the terminal proof and all cache/readback staging capacity.</summary>
    internal void AttachCommandProof(DaprLogicalResponseOwner proof, EventBufferReservation staging)
    { CommandProof = proof; _proofStaging = staging; }

    /// <summary>Takes admitted private state and staging ownership before the durable boundary.</summary>
    internal void AttachState(DaprLogicalResponseOwner prior, DaprLogicalResponseOwner successor, EventBufferReservation staging)
    {
        PriorState = prior;
        CanonicalState = successor;
        _stateStaging = staging;
    }

    /// <summary>Retains admitted predecessor, private intake and all future capacity before callback entry.</summary>
    internal void AttachIntake(DaprLogicalResponseOwner prior, PrivateLogicalReplayPage intake, EventBufferBudget budget)
    {
        PriorState = prior;
        Intake = intake;
        ReconstructionBudget = budget;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Page.Dispose();
        Response.Dispose();
        PriorState?.Dispose();
        CanonicalState?.Dispose();
        Intake?.Dispose();
        _stateStaging?.Dispose();
        ReconstructionBudget?.Dispose();
        CommandProof?.Dispose();
        _proofStaging?.Dispose();
        CommitmentBudget?.Dispose();
        _stagingCharge.Dispose();
    }
}
