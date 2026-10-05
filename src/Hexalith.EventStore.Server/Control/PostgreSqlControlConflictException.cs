namespace Hexalith.EventStore.Server.Control;

/// <summary>One addressed predecessor or in-transaction image disagreed with the admitted transaction.</summary>
internal sealed class PostgreSqlControlConflictException : InvalidOperationException
{
    internal PostgreSqlControlConflictException()
        : base("CapabilityMismatch: a declared PostgreSQL metadata participant disagrees with its admitted image.") { }
}
