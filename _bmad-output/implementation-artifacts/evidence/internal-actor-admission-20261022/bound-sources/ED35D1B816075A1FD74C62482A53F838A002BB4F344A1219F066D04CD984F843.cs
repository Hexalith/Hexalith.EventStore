namespace Hexalith.EventStore.Client.Projections;

internal enum SharedProjectionEpochPhase
{
    Open,
    Building,
    CatchingUp,
    Aborting,
    Capturing,
}
