namespace Hexalith.EventStore.Server.Tests.GlobalPositions;

/// <summary>Compares decoded values without a global scalar fallback.</summary>
internal static class QualificationPositionComparer
{
    /// <summary>Compares two decoded descriptors using invalid-before-unsupported precedence.</summary>
    /// <param name="left">The left descriptor.</param>
    /// <param name="right">The right descriptor.</param>
    /// <returns>The bounded comparison outcome.</returns>
    internal static QualificationComparison Compare(QualificationPosition left, QualificationPosition right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (!IsStructurallyValid(left) || !IsStructurallyValid(right))
        {
            return QualificationComparison.InvalidPosition;
        }

        if (!IsSupported(left) || !IsSupported(right))
        {
            return QualificationComparison.UnsupportedScheme;
        }

        if (left.MetadataVersion != right.MetadataVersion)
        {
            return QualificationComparison.UnsupportedCrossScheme;
        }

        if (left.MetadataVersion == 2)
        {
            if (left.CanonicalizationVersion != right.CanonicalizationVersion)
            {
                return QualificationComparison.UnsupportedCrossCanonicalization;
            }

            if (left.Shard != right.Shard)
            {
                return QualificationComparison.UnsupportedCrossShard;
            }

            if (left.Generation != right.Generation)
            {
                return QualificationComparison.UnsupportedCrossGeneration;
            }
        }
        else if (left.Scalar == 0 || right.Scalar == 0)
        {
            return QualificationComparison.UnknownPosition;
        }

        long first = left.MetadataVersion == 1 ? left.Scalar : left.Counter;
        long second = right.MetadataVersion == 1 ? right.Scalar : right.Counter;
        return first.CompareTo(second) switch
        {
            < 0 => QualificationComparison.Less,
            > 0 => QualificationComparison.Greater,
            _ => QualificationComparison.Equal,
        };
    }

    private static bool IsStructurallyValid(QualificationPosition value)
        => !string.IsNullOrEmpty(value.Scheme) && value.MetadataVersion switch
        {
            1 => value.Scalar >= 0 && value.Shard == default && value.Generation == 0 && value.Counter == 0,
            2 => value.Scalar == 0 && value.Shard.IsValid && value.Generation > 0 && value.Counter > 0,
            _ => true,
        };

    private static bool IsSupported(QualificationPosition value)
        => (value.MetadataVersion == 1 && value.Scheme == "global-v1")
            || (value.MetadataVersion == 2 && value.Scheme == "tenant-domain-v2" && value.CanonicalizationVersion == 1);
}
