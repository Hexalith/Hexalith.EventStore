namespace Hexalith.EventStore.Server.Tests.GlobalPositions;

/// <summary>Comparison outcomes from the approved semantic contract.</summary>
internal enum QualificationComparison
{
    /// <summary>A decoded operand violates its supported version's value rules.</summary>
    InvalidPosition,

    /// <summary>An outer or canonicalization version is unrecognized.</summary>
    UnsupportedScheme,

    /// <summary>The operands use different recognized position schemes.</summary>
    UnsupportedCrossScheme,

    /// <summary>Recognized supported canonicalization versions differ.</summary>
    UnsupportedCrossCanonicalization,

    /// <summary>The exact tenant or domain bytes differ.</summary>
    UnsupportedCrossShard,

    /// <summary>The shard is equal but the generations differ.</summary>
    UnsupportedCrossGeneration,

    /// <summary>At least one otherwise-comparable v1 scalar is zero.</summary>
    UnknownPosition,

    /// <summary>The left comparable allocation label is smaller.</summary>
    Less,

    /// <summary>The complete supported position identities are equal.</summary>
    Equal,

    /// <summary>The left comparable allocation label is larger.</summary>
    Greater,
}
