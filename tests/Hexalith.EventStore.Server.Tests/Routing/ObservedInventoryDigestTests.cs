using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Checks owned per-observation computation without retaining host or authority decisions.</summary>
public sealed class ObservedInventoryDigestTests
{
    /// <summary>Nested source mutations and disposed source schemas cannot change an already owned observation.</summary>
    [Fact]
    public void OwnedDocumentsKeepNestedListsAndSchemasIndependent()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        RouteCatalogHostObservation original = fixture.Observations[0];
        RouteContractDeclaration source = fixture.Envelope.Declarations.First(declaration => declaration.Execution is not null);
        var fallback = source.FallbackTypes.ToList();
        var aliases = source.Execution!.Aliases.ToList();
        var outputs = source.Execution.ProjectionOutputs.ToList();
        var declaration = source with { FallbackTypes = fallback, Execution = source.Execution with { Aliases = aliases, ProjectionOutputs = outputs } };
        IdempotencyCatalogEntry policy = original.AdmissionPolicies![0];
        var fields = policy.DescriptorSchema.Fields.ToList();
        var readers = policy.ReaderDigestKeyGenerations.ToList();
        var keys = policy.KeyMap.ToList();
        policy = policy with { DescriptorSchema = policy.DescriptorSchema with { Fields = fields }, ReaderDigestKeyGenerations = readers, KeyMap = keys };
        RouteOperationContract operation = fixture.Envelope.Operations![0];
        using var document = JsonDocument.Parse(operation.RequestSchema.GetRawText());
        operation = operation with { RequestSchema = document.RootElement };
        var observation = original with { Declarations = [declaration], Operations = [operation], AdmissionPolicies = [policy] };
        string declarationDigest = RouteCatalogCodec.ComputeDeclarationDigest(declaration);
        string operationDigest = RouteCatalogCodec.ComputeOperationContractDigest(operation);
        string admissionDigest = RouteCatalogCodec.ComputeIdempotencyPolicyDigest(policy);

        var inventory = new ObservedRouteCatalogInventory(observation);
        fallback.Add("changed-fallback"); aliases.Add("changed-alias"); outputs.Add("changed-output");
        fields.Clear(); readers.Clear(); keys.Clear(); document.Dispose();

        RouteCatalogHostObservation owned = inventory.Observation;
        inventory.Declaration(owned.Declarations[0]).ShouldBe(declarationDigest);
        inventory.Operation(owned.Operations![0]).ShouldBe(operationDigest);
        inventory.Admission(owned.AdmissionPolicies![0]).ShouldBe(admissionDigest);
        RouteCatalogCodec.ComputeDeclarationDigest(owned.Declarations[0]).ShouldBe(declarationDigest);
        RouteCatalogCodec.ComputeOperationContractDigest(owned.Operations[0]).ShouldBe(operationDigest);
        RouteCatalogCodec.ComputeIdempotencyPolicyDigest(owned.AdmissionPolicies[0]).ShouldBe(admissionDigest);
        owned.Declarations[0].FallbackTypes.ShouldNotBeSameAs(fallback);
        owned.Declarations[0].Execution!.Aliases.ShouldNotBeSameAs(aliases);
        owned.Declarations[0].Execution!.ProjectionOutputs.ShouldNotBeSameAs(outputs);
        owned.AdmissionPolicies[0].DescriptorSchema.Fields.ShouldNotBeSameAs(fields);
        owned.AdmissionPolicies[0].ReaderDigestKeyGenerations.ShouldNotBeSameAs(readers);
        owned.AdmissionPolicies[0].KeyMap.ShouldNotBeSameAs(keys);
    }

    /// <summary>Every call captures and computes current declarations; a prior successful observation supplies no authority.</summary>
    [Fact]
    public async Task SuccessiveCapturesRefuseChangedNestedDeclarationAndRecoverOnlyAfterRestoration()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        RouteCatalogActivation candidate = fixture.Candidate();
        RouteCatalogHostObservation original = fixture.Observations.Single(observation => observation.AppId == "sample");
        RouteCatalogHostObservation current = original;
        int captures = 0;
        var observer = new ObservedRouteCatalogHost(_ => { captures++; return Task.FromResult(current); });
        var token = TestContext.Current.CancellationToken;
        (await observer.ObserveAsync(candidate, fixture.Store.Coordinates, token)).ShouldBe(ObservedRouteCatalogHost.Identity(original));
        RouteContractDeclaration changed = original.Declarations[0] with
        { Execution = original.Declarations[0].Execution! with { Aliases = [.. original.Declarations[0].Execution!.Aliases, "changed-owner-alias"] } };
        current = original with { Declarations = [changed, .. original.Declarations.Skip(1)] };
        (await Should.ThrowAsync<RouteCatalogUnavailableException>(() => observer.ObserveAsync(candidate, fixture.Store.Coordinates, token)))
            .Category.ShouldBe(RouteCatalogRefusalReason.Inventory);
        current = original;
        (await observer.ObserveAsync(candidate, fixture.Store.Coordinates, token)).ShouldBe(ObservedRouteCatalogHost.Identity(original));
        captures.ShouldBe(3);
    }

    /// <summary>Duplicate documents remain visible to existing exact-inventory refusal rather than collapsing by route identity.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateDeclarationIsNotCollapsed(bool sameReference)
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        RouteCatalogHostObservation actual = fixture.Observations.Single(observation => observation.AppId == "sample");
        actual = actual with { Declarations = [.. actual.Declarations, sameReference ? actual.Declarations[0] : actual.Declarations[0] with { }] };
        var observer = new ObservedRouteCatalogHost(_ => Task.FromResult(actual));
        (await Should.ThrowAsync<RouteCatalogUnavailableException>(() => observer.ObserveAsync(fixture.Candidate(), fixture.Store.Coordinates,
            TestContext.Current.CancellationToken))).Category.ShouldBe(RouteCatalogRefusalReason.Inventory);
    }

    /// <summary>Missing independent inventories still refuse before inspecting malformed nested documents.</summary>
    [Fact]
    public void MissingInventoryRetainsPreconditionRefusal()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        RouteCatalogHostObservation actual = fixture.Observations[0] with { AdmissionPolicies = null, Operations = [null!] };
        Should.Throw<RouteCatalogUnavailableException>(() => ObservedRouteCatalogHost.Identity(actual)).Category.ShouldBe(RouteCatalogRefusalReason.Configuration);
    }

    /// <summary>The same codec validation refuses malformed nested documents, and disposed schemas fail closed.</summary>
    [Fact]
    public void MalformedDocumentsAndDisposedSchemasCannotProduceIdentity()
    {
        var fixture = new RouteCatalogTestFixture(compiledSchemaThree: true);
        RouteCatalogHostObservation actual = fixture.Observations[0];
        RouteContractDeclaration invalid = fixture.Envelope.Declarations[0] with { FallbackTypes = null! };
        var expected = Should.Throw<RouteCatalogValidationException>(() => RouteCatalogCodec.ComputeDeclarationDigest(invalid));
        Should.Throw<RouteCatalogValidationException>(() => ObservedRouteCatalogHost.Identity(actual with { Declarations = [invalid] }))
            .Diagnostics.ShouldBe(expected.Diagnostics);
        using var document = JsonDocument.Parse(fixture.Envelope.Operations![0].RequestSchema.GetRawText());
        RouteOperationContract disposed = fixture.Envelope.Operations[0] with { RequestSchema = document.RootElement };
        document.Dispose();
        Should.Throw<ObjectDisposedException>(() => ObservedRouteCatalogHost.Identity(actual with { Operations = [disposed] }));
        Should.Throw<NullReferenceException>(() => ObservedRouteCatalogHost.Identity(actual with { Operations = [null!] }));
    }
}
