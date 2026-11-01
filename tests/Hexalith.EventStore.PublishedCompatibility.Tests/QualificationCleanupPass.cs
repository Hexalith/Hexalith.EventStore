namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Retains actual independent cleanup/inventory coordinates for one owned runtime pass.</summary>
/// <param name="Owner">The exact runtime owner.</param>
/// <param name="Root">Its private command working directory.</param>
/// <param name="Started">The actual pass start.</param>
/// <param name="Ended">The actual pass completion.</param>
/// <param name="BaselineStarted">The actual shared-inventory capture start.</param>
/// <param name="BaselineEnded">The actual shared-inventory capture completion.</param>
/// <param name="Baseline">The independently normalized initial shared inventory.</param>
/// <param name="After">The independently normalized post-cleanup shared inventory.</param>
/// <param name="ContainerNames">Every name actually reserved by this owner.</param>
/// <param name="VolumeNames">Every volume actually reserved by this owner.</param>
/// <param name="Failures">All retained cleanup failures, including earlier passes.</param>
/// <param name="RemainingProcesses">The actual remaining owned process count.</param>
internal sealed record QualificationCleanupPass(string Owner, string Root, DateTimeOffset Started, DateTimeOffset Ended,
    DateTimeOffset? BaselineStarted, DateTimeOffset? BaselineEnded, string? Baseline, string? After,
    IReadOnlyList<string> ContainerNames, IReadOnlyList<string> VolumeNames, int Failures, int RemainingProcesses, string? ProcessScanId = null, bool Final = false);
