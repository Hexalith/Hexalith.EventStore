using System.Reflection;

using Dapr.Actors;

using Hexalith.EventStore.Server.Actors;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Protects released publication implementations from additional abstract obligations.</summary>
public sealed class ReplayPublicationReleasedShapeTests
{
    [Fact]
    public void ReleasedPublicationContractRetainsItsFourMethodsAndSignatures()
    {
        MethodInfo[] methods = typeof(IProjectionReplayPublicationActor).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        methods.Select(method => method.Name).Order(StringComparer.Ordinal).ShouldBe(new[]
        {
            nameof(IProjectionReplayPublicationActor.FinalizeReplayAsync),
            nameof(IProjectionReplayPublicationActor.PromoteReplayAsync),
            nameof(IProjectionReplayPublicationActor.ReadReplayCompletionAsync),
            nameof(IProjectionReplayPublicationActor.StageReplayAsync),
        }.Order(StringComparer.Ordinal));
        foreach (MethodInfo method in methods)
        {
            method.IsAbstract.ShouldBeTrue();
            method.GetParameters().Select(parameter => parameter.ParameterType).ShouldBe([typeof(ProjectionReplayPublicationRequest)]);
            method.ReturnType.ShouldBe(method.Name is nameof(IProjectionReplayPublicationActor.StageReplayAsync)
                or nameof(IProjectionReplayPublicationActor.PromoteReplayAsync) ? typeof(Task<ProjectionRebuildInspection>) : typeof(Task<bool>));
        }
    }

    [Fact]
    public void StrictCompletionIsAnIndependentAdditiveActorCapability()
    {
        typeof(IProjectionReplayPublicationActor).IsAssignableFrom(typeof(IProjectionBoundReplayCompletionActor)).ShouldBeFalse();
        typeof(IActor).IsAssignableFrom(typeof(IProjectionBoundReplayCompletionActor)).ShouldBeTrue();
        typeof(IProjectionBoundReplayCompletionActor).IsAssignableFrom(typeof(EventReplayProjectionActor)).ShouldBeTrue();
    }
}
