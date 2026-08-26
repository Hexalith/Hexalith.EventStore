using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Actors;

/// <summary>
/// Deterministic coverage for the Story 4.5 perturbation switch. The fail-closed throw is the only
/// thing standing between an operator typo and a receipt that looks like a real mutation but was
/// produced by the unperturbed harness, so it is exercised rather than assumed.
/// </summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class Story45MutationSwitchTests
{
    /// <summary>Verifies an unrecognized perturbation name fails closed instead of running unperturbed.</summary>
    [Fact]
    public void Armed_UnrecognizedValue_Throws()
    {
        WithMutationVariable(
            "not-a-real-perturbation",
            () =>
            {
                InvalidOperationException error = Should.Throw<InvalidOperationException>(
                    () => Story45MutationSwitch.Armed);
                error.Message.ShouldContain("not-a-real-perturbation");
                error.Message.ShouldContain("Recognized perturbations");
            });
    }

    /// <summary>Verifies an unset or blank variable reports no perturbation rather than throwing.</summary>
    /// <param name="value">The environment value under test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Armed_BlankValue_ReportsNoPerturbation(string? value)
        => WithMutationVariable(value, () => Story45MutationSwitch.Armed.ShouldBeNull());

    /// <summary>Verifies each recognized name arms exactly itself.</summary>
    [Fact]
    public void IsArmed_RecognizedValue_ArmsOnlyThatPerturbation()
    {
        foreach (string mutation in Story45MutationSwitch.KnownMutations)
        {
            WithMutationVariable(
                mutation,
                () =>
                {
                    Story45MutationSwitch.Armed.ShouldBe(mutation);
                    Story45MutationSwitch.IsArmed(mutation).ShouldBeTrue();
                    foreach (string other in Story45MutationSwitch.KnownMutations.Where(
                        name => !string.Equals(name, mutation, StringComparison.Ordinal)))
                    {
                        Story45MutationSwitch.IsArmed(other).ShouldBeFalse();
                    }
                });
        }
    }

    /// <summary>Verifies querying an unknown perturbation name is itself a fail-closed error.</summary>
    [Fact]
    public void IsArmed_UnknownName_Throws()
        => Should.Throw<InvalidOperationException>(() => Story45MutationSwitch.IsArmed("nope"));

    private static void WithMutationVariable(string? value, Action assertion)
    {
        string? previous = Environment.GetEnvironmentVariable(Story45MutationSwitch.EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(Story45MutationSwitch.EnvironmentVariable, value);
            assertion();
        }
        finally
        {
            Environment.SetEnvironmentVariable(Story45MutationSwitch.EnvironmentVariable, previous);
        }
    }
}
