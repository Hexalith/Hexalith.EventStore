using Hexalith.EventStore.Contracts.Projections;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class DomainProjectionLegacyCoexistenceTests
{
    [Theory]
    [InlineData("exact", true)]
    [InlineData("absent", false)]
    [InlineData("missing-legacy", false)]
    [InlineData("extra-legacy", false)]
    [InlineData("case-legacy", false)]
    [InlineData("wrong-legacy", false)]
    [InlineData("missing-named", false)]
    [InlineData("extra-named", false)]
    [InlineData("wrong-named", false)]
    [InlineData("wrong-route", false)]
    [InlineData("event-semantics", false)]
    [InlineData("incremental-rebuild", false)]
    public void OnlyTheCompleteExactSourceDeclarationEnablesCoexistence(string vector, bool expected)
    {
        IDomainProjectionHandler legacy = Substitute.For<IDomainProjectionHandler>();
        legacy.Domain.Returns(vector == "case-legacy" ? "Widget" : "widget");
        IAsyncDomainProjectionRebuildHandler named = Substitute.For<IAsyncDomainProjectionRebuildHandler, IAsyncDomainProjectionReconciliationHandler>();
        named.Domain.Returns("widget");
        named.ProjectionType.Returns(vector == "wrong-route" ? "other" : "detail");
        named.RebuildSemantics.Returns(vector == "incremental-rebuild" ? DomainProjectionRebuildSemantics.Incremental : DomainProjectionRebuildSemantics.FullReplay);
        ProjectionSourceGuardCapability capability = vector == "event-semantics"
            ? new(1, 1, 1, "single-event-v1", "projection-owner-proof/v1", "event-message-id/v1", "read-model-batch/v1")
            : new(1, 1, 1, "full-replay-v1", "projection-owner-proof/v1", null, null);
        var registration = new DomainProjectionOwnerProofRegistration("widget", "detail", capability, new("store", "detail"))
        {
            LegacyCoexistence = vector == "absent" ? null : new(vector == "wrong-named" ? typeof(object) : named.GetType(),
                vector == "wrong-legacy" ? typeof(object) : legacy.GetType()),
        };
        IDomainProjectionHandler[] legacyHandlers = vector == "missing-legacy" ? []
            : vector == "extra-legacy" ? [legacy, legacy] : [legacy];
        IAsyncDomainProjectionHandler[] namedHandlers = vector == "missing-named" ? []
            : vector == "extra-named" ? [named, named] : [named];
        var response = new AdminOperationalIndexMetadata.Response([
            new("widget", [], [], [], [], ["widget"], [])
            { NamedProjectionTypes = ["detail"], OrdinaryProjectionTypes = ["detail"] },
        ]) { AppId = "source-fixture", ServiceVersion = "v1", CatalogFingerprint = new string('a', 64) };
        var bound = DomainProjectionOwnerProof.BindMetadata(response, [registration], legacyHandlers, namedHandlers);
        (bound.Domains[0].SourceGuardCapabilities?.ContainsKey("detail") ?? false).ShouldBe(expected);
        var services = new ServiceCollection();
        foreach (var handler in legacyHandlers) services.AddSingleton(handler);
        foreach (var handler in namedHandlers) services.AddSingleton(handler);
        services.AddSingleton(registration);
        using var provider = services.BuildServiceProvider();
        DomainProjectionOwnerProof.SupportsEnrollment(provider, registration).ShouldBe(expected);
        var routes = DomainProjectionOwnerProof.BindRoutes(provider, [new("widget", "detail")]).Single();
        (routes.SourceGuard is not null).ShouldBe(expected);
    }
}
