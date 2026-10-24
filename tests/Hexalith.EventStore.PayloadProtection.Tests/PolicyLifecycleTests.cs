using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using NSubstitute;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Exercises section 9 policy and ownership vectors before any material generation.</summary>
public sealed class PolicyLifecycleTests
{
    private static readonly AggregateIdentity Identity = new("tenant-a", "parties", "party-01");

    [Fact]
    public void V031_V040_AttributesUseSerializedNamesAndSkipNulls()
    {
        PersonalDataDiscovery discovery = Discovery();
        discovery.Select(Identity, new PolicyLifecyclePayload { Secret = "" }, PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload,
            "fixture", PayloadProtectionPayloadKind.Event).ShouldBe(["/a~1b~0"]);
        discovery.Select(Identity, new PolicyLifecyclePayload(), PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload,
            "fixture", PayloadProtectionPayloadKind.Event).ShouldBeEmpty();
    }

    [Fact]
    public void V036_V037_CanonicalOverlapAndDuplicateSelectionFailBeforeMaterial()
    {
        ICanonicalPersonalDataPathPolicy canonical = Canonical(["/child", "/child/a~1b~0"]);
        var payload = new PolicyLifecyclePayload { Child = new() { Secret = "fixture-canary" } };
        Should.Throw<PayloadProtectionFormatException>(() => Discovery(paths: [canonical]).Select(Identity, payload,
            PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload, "fixture", PayloadProtectionPayloadKind.Event));
        canonical.SelectCanonicalJsonPointers(Arg.Any<AggregateIdentity>(), Arg.Any<JsonElement>(), Arg.Any<JsonTypeInfo>(), Arg.Any<PayloadProtectionPayloadKind>())
            .Returns(["/child/a~1b~0"]);
        Should.Throw<PayloadProtectionFormatException>(() => Discovery(paths: [canonical]).Select(Identity, payload,
            PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload, "fixture", PayloadProtectionPayloadKind.Event));
    }

    [Fact]
    public void V038_V039_CyclePolicyFaultAndTypedJsonDisagreementFailClosed()
    {
        var cyclic = new PolicyLifecyclePayload();
        cyclic.Child = cyclic;
        Should.Throw<PayloadProtectionFormatException>(() => Discovery().Select(Identity, cyclic,
            PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload, "fixture", PayloadProtectionPayloadKind.Event));
        IPersonalDataPolicy policy = Policy("fault-policy", 1);
        policy.Evaluate(Arg.Any<PersonalDataPolicyContext>()).Returns(_ => throw new InvalidOperationException("private-canary"));
        PayloadProtectionFormatException failure = Should.Throw<PayloadProtectionFormatException>(() => Discovery([policy]).Select(Identity,
            new PolicyLifecyclePayload { Name = "fixture" }, PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload, "fixture", PayloadProtectionPayloadKind.Event));
        failure.ToString().ShouldNotContain("private-canary");
        Should.Throw<PayloadProtectionFormatException>(() => Discovery().Select(Identity, new PolicyLifecyclePayload { Secret = "typed" },
            PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload, "fixture", PayloadProtectionPayloadKind.Event, "{\"a/b~\":\"different\"}"u8.ToArray()));
    }

    [Fact]
    public void PolicyRegistrationUsesOneIdentityNamespaceAndDeterministicOrdering()
    {
        IPersonalDataPolicy z = Policy("z-policy", -1);
        IPersonalDataPolicy a = Policy("a-policy", -1);
        new PersonalDataPolicyRegistry([z, a], []).Entries.Select(p => p.PolicyId)
            .ShouldBe(["a-policy", "z-policy", PersonalDataPolicyRegistry.AttributePolicyId]);
        Should.Throw<PayloadProtectionFormatException>(() => new PersonalDataPolicyRegistry([a], [Canonical([], "a-policy")]));
        Should.Throw<PayloadProtectionFormatException>(() => new PersonalDataPolicyRegistry([Policy(PersonalDataPolicyRegistry.AttributePolicyId, 0)], []));
    }

    [Fact]
    public void V038_CustomConverterRequiresCanonicalPathsAndSerializesExactlyOnce()
    {
        var converter = new PolicyLifecycleConverter();
        var options = new JsonSerializerOptions(); options.Converters.Add(converter);
        var context = new PolicyLifecycleJsonContext(options);
        var types = new PayloadTypeRegistry([context.PolicyLifecyclePayload]);
        var payload = new PolicyLifecyclePayload { Secret = "fixture" };
        Should.Throw<PayloadProtectionFormatException>(() => new PersonalDataDiscovery(new([], []), types).Select(Identity,
            payload, context.PolicyLifecyclePayload, "fixture", PayloadProtectionPayloadKind.Event));
        converter.WriteCount.ShouldBe(1);
        new PersonalDataDiscovery(new([], [Canonical([""])]), types).Select(Identity, payload,
            context.PolicyLifecyclePayload, "hx-snapshot-v1:fixture-state", PayloadProtectionPayloadKind.Snapshot).ShouldBe([""]);
        converter.WriteCount.ShouldBe(2);
    }

    [Fact]
    public void V039_UndefinedDecisionFailsAndAbstainCannotOverrideAttribute()
    {
        IPersonalDataPolicy policy = Policy("ordinary-policy", 1);
        Discovery([policy]).Select(Identity, new PolicyLifecyclePayload { Secret = "fixture" },
            PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload, "fixture", PayloadProtectionPayloadKind.Event).ShouldBe(["/a~1b~0"]);
        policy.Evaluate(Arg.Any<PersonalDataPolicyContext>()).Returns((PersonalDataPolicyDecision)99);
        Should.Throw<PayloadProtectionFormatException>(() => Discovery([policy]).Select(Identity, new PolicyLifecyclePayload { Secret = "fixture" },
            PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload, "fixture", PayloadProtectionPayloadKind.Event));
    }

    [Fact]
    public void V041_NoSelectionNeverGeneratesMaterial()
    {
        var payload = new PolicyLifecyclePayload();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload);
        IReadOnlyList<string> paths = Discovery().Select(Identity, payload, PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload,
            "fixture", PayloadProtectionPayloadKind.Event, bytes);
        int materialCalls = 0;
        CoreProtectionResult result = new PayloadProtectionCore().ProtectEvent(bytes, paths,
            new(Identity, "fixture", PayloadProtectionPayloadKind.Event, 0), () => { materialCalls++; throw new InvalidOperationException(); });
        result.PayloadBytes.ShouldBe(bytes); result.ProtectedPathCount.ShouldBe(0); materialCalls.ShouldBe(0);
    }

    [Fact]
    public async Task V073_ErasureOwnershipEpochRegressionAndInvalidTransitionAreRejected()
    {
        IErasureStateProvider provider = Substitute.For<IErasureStateProvider>();
        provider.ContractVersion.Returns(1);
        var registry = new ErasureStateRegistry(new([new("tenant-a", "parties")]), [new("parties", provider)]);
        PayloadErasureStateResult State(PayloadErasureState state, ulong epoch, PayloadErasureReasonCode reason) => new(state, epoch, reason, DateTimeOffset.UtcNow);
        provider.GetStateAsync(Arg.Any<PayloadErasureStateRequest>(), Arg.Any<CancellationToken>()).Returns(State(PayloadErasureState.Active, 3, PayloadErasureReasonCode.None));
        (await registry.ObserveAsync(Identity)).LifecycleEpoch.ShouldBe(3UL);
        provider.GetStateAsync(Arg.Any<PayloadErasureStateRequest>(), Arg.Any<CancellationToken>()).Returns(State(PayloadErasureState.Active, 2, PayloadErasureReasonCode.None));
        (await Should.ThrowAsync<PayloadProtectionStateException>(async () => await registry.ObserveAsync(Identity))).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        provider.GetStateAsync(Arg.Any<PayloadErasureStateRequest>(), Arg.Any<CancellationToken>()).Returns(State(PayloadErasureState.Pending, 3, PayloadErasureReasonCode.DomainPending));
        await Should.ThrowAsync<PayloadProtectionStateException>(async () => await registry.ObserveAsync(Identity));
        Should.Throw<PayloadProtectionFormatException>(() => new ErasureStateRegistry(new([new("tenant-a", "parties")]), [new("parties", provider), new("parties", provider)]));
        await Should.ThrowAsync<PayloadProtectionStateException>(async () => await registry.ObserveAsync(new("tenant-a", "other", "party-01")));
    }

    [Fact]
    public void ErasureEnrollmentRequiresExactlyTheProtectedDomainsAndBoundsProviderMetadataFaults()
    {
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties"), new("tenant-b", "parties")]);
        IErasureStateProvider provider = Substitute.For<IErasureStateProvider>();
        provider.ContractVersion.Returns(1);
        _ = new ErasureStateRegistry(roots, [new("parties", provider)]);
        Should.Throw<PayloadProtectionFormatException>(() => new ErasureStateRegistry(roots, []));
        Should.Throw<PayloadProtectionFormatException>(() => new ErasureStateRegistry(roots, [new("parties", provider), new("unprotected", provider)]));
        provider.ContractVersion.Returns(_ => throw new InvalidOperationException("private-provider-metadata-canary"));
        PayloadProtectionFormatException failure = Should.Throw<PayloadProtectionFormatException>(() => new ErasureStateRegistry(roots, [new("parties", provider)]));
        failure.InnerException.ShouldBeNull();
        failure.ToString().ShouldNotContain("private-provider-metadata-canary");
    }

    [Fact]
    public async Task CancellationPropagatesBeforeDiscoveryOrProviderCalls()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Should.Throw<OperationCanceledException>(() => Discovery().Select(Identity, new PolicyLifecyclePayload(),
            PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload, "fixture", PayloadProtectionPayloadKind.Event, cancellationToken: cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(async () => await new ErasureStateRegistry(new([new("tenant-a", "parties")]), [new("parties", new LifecycleTestErasureProvider())]).ObserveAsync(Identity, cancellationToken: cancelled.Token));
    }

    [Fact]
    public void E1_ChangingTypedGetterFailsBeforeASelectionCanBeOmitted()
    {
        var metadata = PolicyLifecycleJsonContext.Default;
        var discovery = new PersonalDataDiscovery(new([], []), new([metadata.ChangingGetterPayload, metadata.PolicyLifecyclePayload, metadata.String]));
        PayloadProtectionFormatException failure = Should.Throw<PayloadProtectionFormatException>(() => discovery.Select(Identity,
            new ChangingGetterPayload(), metadata.ChangingGetterPayload, "fixture", PayloadProtectionPayloadKind.Event));
        failure.InnerException.ShouldBeNull(); failure.ToString().ShouldNotContain("changing-getter-private-canary");
    }

    [Fact]
    public void E2_E11_CanonicalAndOrdinaryCallbacksFollowTheCapturedPrecedenceAndSuppressSelectedDescendants()
    {
        var calls = new List<string>();
        IPersonalDataPolicy ordinary = Policy("early-ordinary", -2);
        ordinary.Evaluate(Arg.Any<PersonalDataPolicyContext>()).Returns(call =>
        {
            var context = call.Arg<PersonalDataPolicyContext>();
            if (context.JsonPointer == "/name") { calls.Add("ordinary"); }
            return PersonalDataPolicyDecision.Abstain;
        });
        ICanonicalPersonalDataPathPolicy canonical = Canonical(["/child"], "later-canonical");
        canonical.Order.Returns(-1);
        canonical.SelectCanonicalJsonPointers(Arg.Any<AggregateIdentity>(), Arg.Any<JsonElement>(), Arg.Any<JsonTypeInfo>(), Arg.Any<PayloadProtectionPayloadKind>())
            .Returns(_ => { calls.Add("canonical"); return new[] { "/child" }; });
        IPersonalDataPolicy late = Policy("late-ordinary", 1);
        late.Evaluate(Arg.Any<PersonalDataPolicyContext>()).Returns(call =>
        {
            call.Arg<PersonalDataPolicyContext>().JsonPointer.ShouldNotStartWith("/child/");
            return PersonalDataPolicyDecision.Abstain;
        });
        // The early policy visits the child before its selection; the attribute policy and late policy suppress it afterwards.
        Discovery([ordinary, late], [canonical]).Select(Identity, new PolicyLifecyclePayload { Name = "fixture", Child = new() { Secret = "fixture" } },
            PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload, "fixture", PayloadProtectionPayloadKind.Event).ShouldBe(["/child"]);
        calls.ShouldBe(["ordinary", "canonical"]);
    }

    [Fact]
    public void NonNullRuntimeSubstitutionCannotOmitSerializedPersonalDataMarkers()
    {
        var context = PolicyLifecycleJsonContext.Default;
        var payload = new RuntimeSubstitutionPayload();
        var mapping = new PayloadPropertyRegistration(typeof(RuntimeSubstitutionPayload), context.RuntimeSubstitutionPayload.Properties.Single(p => p.Name == "child"), typeof(RuntimeSubstitutionPayload).GetProperty(nameof(RuntimeSubstitutionPayload.Child))!);
        var discovery = new PersonalDataDiscovery(new([], []), new([context.RuntimeSubstitutionPayload, context.PolicyLifecyclePayload, context.PartySnapshotState, context.String], [mapping]));
        var failure = Should.Throw<PayloadProtectionFormatException>(() => discovery.Select(Identity, payload, context.RuntimeSubstitutionPayload, "fixture", PayloadProtectionPayloadKind.Event));
        payload.Reads.ShouldBe(2); failure.InnerException.ShouldBeNull(); failure.ToString().ShouldNotContain("substitution-private-canary");
        var safe = new PersonalDataDiscovery(new([], [Canonical(["/child"])]), new([context.RuntimeSubstitutionPayload, context.PolicyLifecyclePayload, context.PartySnapshotState, context.String]));
        safe.Select(Identity, new RuntimeSubstitutionPayload(), context.RuntimeSubstitutionPayload, "fixture", PayloadProtectionPayloadKind.Event).ShouldBe(["/child"]);
    }

    [Fact]
    public void CanonicalSensitiveLeafAllowsAPublicSiblingWithoutClrProvenance()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = PolicyLifecycleJsonContext.Default.WithAddedModifier(metadata =>
        {
            foreach (JsonPropertyInfo property in metadata.Properties) { property.AttributeProvider = null; }
        }) };
        JsonTypeInfo metadata = options.GetTypeInfo(typeof(PolicyLifecyclePayload));
        metadata.Properties.ShouldAllBe(property => property.AttributeProvider == null);
        var discovery = new PersonalDataDiscovery(new([], [Canonical(["/a~1b~0"])]), new([metadata]));
        discovery.Select(Identity, new PolicyLifecyclePayload { Name = "public-sibling", Secret = "private-leaf" }, metadata,
            "fixture", PayloadProtectionPayloadKind.Event).ShouldBe(["/a~1b~0"]);
    }

    [Fact]
    public void MappedWholeObjectPersonalDataNeedsNoDescendantRuntimeCorrelation()
    {
        var context = PolicyLifecycleJsonContext.Default;
        var mapping = new PayloadPropertyRegistration(typeof(WholeObjectSelectionPayload), context.WholeObjectSelectionPayload.Properties.Single(), typeof(WholeObjectSelectionPayload).GetProperty(nameof(WholeObjectSelectionPayload.Child))!);
        var discovery = new PersonalDataDiscovery(new([], []), new([context.WholeObjectSelectionPayload], [mapping]));
        discovery.Select(Identity, new WholeObjectSelectionPayload(), context.WholeObjectSelectionPayload, "fixture", PayloadProtectionPayloadKind.Event).ShouldBe(["/child"]);
    }

    [Fact]
    public void CanonicalPolicyReceivesTheOneSerializationWithConfiguredEscapingAndFormatting()
    {
        var converter = new FormattingPolicyConverter();
        var options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true, IndentCharacter = '\t', IndentSize = 1, NewLine = "\r\n" };
        options.Converters.Add(converter); var context = new PolicyLifecycleJsonContext(options);
        var canonical = Canonical(["/secret"]);
        canonical.SelectCanonicalJsonPointers(Arg.Any<AggregateIdentity>(), Arg.Any<JsonElement>(), Arg.Any<JsonTypeInfo>(), Arg.Any<PayloadProtectionPayloadKind>()).Returns(call =>
        {
            call.Arg<JsonElement>().GetRawText().ShouldBe("{\r\n\t\"secret\": \"<private>\",\r\n\t\"public\": [\r\n\t\t1\r\n\t]\r\n}");
            return new[] { "/secret" };
        });
        var discovery = new PersonalDataDiscovery(new([], [canonical]), new([context.PolicyLifecyclePayload]));
        discovery.Select(Identity, new PolicyLifecyclePayload { Secret = "<private>" }, context.PolicyLifecyclePayload, "fixture", PayloadProtectionPayloadKind.Event).ShouldBe(["/secret"]);
        converter.Writes.ShouldBe(1);
    }

    [Fact]
    public void OversizedSerializationStopsProductionAndClearsEveryDestinationBuffer()
    {
        var payload = new OversizedPolicyPayload(); var observer = new RecordingBufferObserver();
        var context = PolicyLifecycleJsonContext.Default;
        var discovery = new PersonalDataDiscovery(new([], []), new([context.OversizedPolicyPayload]), observer);
        Should.Throw<PayloadProtectionFormatException>(() => discovery.Select(Identity, payload, context.OversizedPolicyPayload, "fixture", PayloadProtectionPayloadKind.Event));
        payload.Produced.ShouldBeLessThan(50000);
        observer.Observed.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SingleOversizedScalarAndConverterOutputAreRejectedWithinBoundedStaging(bool converter)
    {
        string scalar = new('q', PayloadProtectionLimits.PayloadBytes + 1);
        var observer = new RecordingBufferObserver(); var custom = new OversizedScalarConverter();
        var options = new JsonSerializerOptions(); if (converter) { options.Converters.Add(custom); }
        var context = new PolicyLifecycleJsonContext(options);
        var metadata = converter ? (JsonTypeInfo)context.OversizedScalarPayload : context.String;
        object payload = converter ? new OversizedScalarPayload(scalar) : scalar;
        var discovery = new PersonalDataDiscovery(new([], [Canonical([""])]), new([metadata]), observer);
        Should.Throw<PayloadProtectionFormatException>(() => discovery.Select(Identity, payload, metadata, "hx-snapshot-v1:fixture-state", PayloadProtectionPayloadKind.Snapshot));
        custom.Writes.ShouldBe(converter ? 1 : 0); observer.Observed.ShouldNotBeEmpty();
        using var destination = new PayloadSerializationBuffer(observer);
        Should.Throw<PayloadProtectionFormatException>(() => destination.GetMemory(PayloadSerializationBuffer.MaximumAllocationBytes + 1));
    }

    [Fact]
    public void LegalExactPayloadByteBoundaryWithAnUnselectedScalarRemainsAccepted()
    {
        int overhead = JsonSerializer.SerializeToUtf8Bytes(new PolicyLifecyclePayload { Name = "" }, PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload).Length;
        var payload = new PolicyLifecyclePayload { Name = new('q', PayloadProtectionLimits.PayloadBytes - overhead) };
        Discovery().Select(Identity, payload, PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload, "fixture", PayloadProtectionPayloadKind.Event).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegistrationMetadataFaultsCannotExposePrivateDetails(bool canonical)
    {
        var ordinary = Policy("safe-policy", 0); var paths = Canonical([]);
        if (canonical) { paths.Order.Returns(_ => throw new InvalidOperationException("registration-private-canary")); }
        else { ordinary.PolicyId.Returns(_ => throw new InvalidOperationException("registration-private-canary")); }
        var failure = Should.Throw<PayloadProtectionFormatException>(() => new PersonalDataPolicyRegistry(canonical ? [] : [ordinary], canonical ? [paths] : []));
        failure.InnerException.ShouldBeNull(); failure.Data.Count.ShouldBe(0); failure.ToString().ShouldNotContain("registration-private-canary");
    }

    private static PersonalDataDiscovery Discovery(IPersonalDataPolicy[]? policies = null, ICanonicalPersonalDataPathPolicy[]? paths = null)
    {
        JsonTypeInfo metadata = PolicyLifecycleJsonContext.Default.PolicyLifecyclePayload;
        var mappings = metadata.Properties.Select(p => new PayloadPropertyRegistration(typeof(PolicyLifecyclePayload), p,
            typeof(PolicyLifecyclePayload).GetProperty(p.Name == "a/b~" ? nameof(PolicyLifecyclePayload.Secret) : p.Name == "name" ? nameof(PolicyLifecyclePayload.Name) : nameof(PolicyLifecyclePayload.Child))!));
        return new(new(policies ?? [], paths ?? []), new([metadata, PolicyLifecycleJsonContext.Default.String], mappings));
    }

    private static IPersonalDataPolicy Policy(string id, int order)
    {
        IPersonalDataPolicy policy = Substitute.For<IPersonalDataPolicy>();
        policy.ContractVersion.Returns(1); policy.PolicyVersion.Returns(1); policy.PolicyId.Returns(id); policy.Order.Returns(order);
        return policy;
    }

    private static ICanonicalPersonalDataPathPolicy Canonical(string[] paths, string id = "canonical-policy")
    {
        ICanonicalPersonalDataPathPolicy policy = Substitute.For<ICanonicalPersonalDataPathPolicy>();
        policy.ContractVersion.Returns(1); policy.PolicyVersion.Returns(1); policy.PolicyId.Returns(id);
        policy.SelectCanonicalJsonPointers(Arg.Any<AggregateIdentity>(), Arg.Any<JsonElement>(), Arg.Any<JsonTypeInfo>(), Arg.Any<PayloadProtectionPayloadKind>()).Returns(paths);
        return policy;
    }
}
