using System.Reflection;
using System.Text;
using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Tests.Aggregates.ApplyResolverFixtures;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Replay;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>
/// Story 4.3 acceptance tests for the single shared <see cref="ApplyMethodResolver"/>.
/// Every resolution row of the story I/O matrix is exercised on both the rehydrate path
/// (<see cref="DomainProcessorStateRehydrator"/>) and the projection path
/// (<see cref="EventStoreProjection{TReadModel}"/>), because those two call sites used to carry
/// character-identical copies of the resolution logic and drifted silently.
/// </summary>
public sealed class ApplyMethodResolverTests {
    // --- I/O matrix: exact full-name hit ---

    [Fact]
    public void TryResolve_ExactFullName_BindsWithoutSuffixScan() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(ExactHitState));

        MethodInfo? resolved = ApplyMethodResolver.TryResolve(table, typeof(EsFixA.ItemAdded).FullName!);

        resolved.ShouldNotBeNull();
        resolved.GetParameters()[0].ParameterType.ShouldBe(typeof(EsFixA.ItemAdded));
    }

    [Fact]
    public void RehydrateState_ExactFullName_AppliesTheMatchingEvent() {
        ExactHitState state = Rehydrate<ExactHitState>(
            EventArray(typeof(EsFixA.ItemAdded).FullName!, """{"Name":"exact"}"""));

        state.Applied.ShouldBe("EsFixA.ItemAdded:exact");
    }

    [Fact]
    public void ProjectFromJson_ExactFullName_AppliesTheMatchingEvent() {
        ExactHitState model = new ExactHitProjection().ProjectFromJson(
            EventArray(typeof(EsFixA.ItemAdded).FullName!, """{"Name":"exact"}"""));

        model.Applied.ShouldBe("EsFixA.ItemAdded:exact");
    }

    // --- I/O matrix: suffix collision (the defect: SubItemAdded used to bind Apply(ItemAdded)) ---

    [Fact]
    public void TryResolve_SuffixCollision_BindsTheLongerShortName() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(SuffixCollisionState));

        MethodInfo? resolved = ApplyMethodResolver.TryResolve(table, "Foreign.SubItemAdded");

        resolved.ShouldNotBeNull();
        resolved.GetParameters()[0].ParameterType.ShouldBe(typeof(EsFixA.SubItemAdded));
    }

    [Fact]
    public void TryResolve_SuffixCollisionRepeated_IsOrderIndependent() {
        // Rebuilding the table re-walks reflection, so a resolution that depended on dictionary
        // enumeration order would be free to answer differently here.
        MethodInfo?[] resolutions = [.. Enumerable
            .Range(0, 8)
            .Select(_ => ApplyMethodResolver.TryResolve(
                ApplyMethodResolver.BuildTable(typeof(SuffixCollisionState)),
                "Foreign.SubItemAdded"))];

        resolutions.ShouldAllBe(m => m!.GetParameters()[0].ParameterType == typeof(EsFixA.SubItemAdded));
    }

    [Fact]
    public void RehydrateState_SuffixCollision_AppliesTheLongerShortName() {
        SuffixCollisionState state = Rehydrate<SuffixCollisionState>(
            EventArray("Foreign.SubItemAdded", """{"Name":"sub"}"""));

        state.Applied.ShouldBe("SubItemAdded:sub");
    }

    [Fact]
    public void ProjectFromJson_SuffixCollision_AppliesTheLongerShortName() {
        SuffixCollisionState model = new SuffixCollisionProjection().ProjectFromJson(
            EventArray("Foreign.SubItemAdded", """{"Name":"sub"}"""));

        model.Applied.ShouldBe("SubItemAdded:sub");
    }

    // --- I/O matrix: unanchored near-miss (mutation guard for the '.' anchor) ---

    [Fact]
    public void TryResolve_UnanchoredNearMiss_DoesNotMatch() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(OnlyItemAddedState));

        ApplyMethodResolver.TryResolve(table, "Foreign.SubItemAdded").ShouldBeNull();
    }

    [Fact]
    public void RehydrateState_UnanchoredNearMiss_ThrowsMissingApplyMethod() {
        JsonElement events = EventArray("Foreign.SubItemAdded", """{"Name":"sub"}""");

        _ = Should.Throw<MissingApplyMethodException>(() => Rehydrate<OnlyItemAddedState>(events));
    }

    [Fact]
    public void ProjectFromJson_UnanchoredNearMiss_ThrowsWithTheExistingNotFoundBehavior() {
        JsonElement events = EventArray("Foreign.SubItemAdded", """{"Name":"sub"}""");
        var projection = new OnlyItemAddedProjection();

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => projection.ProjectFromJson(events));

        exception.Message.ShouldContain("no matching Apply method");
    }

    // --- I/O matrix: legacy short name ---

    [Fact]
    public void TryResolve_LegacyShortName_Binds() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(OnlyItemAddedState));

        MethodInfo? resolved = ApplyMethodResolver.TryResolve(table, nameof(EsFixA.ItemAdded));

        resolved.ShouldNotBeNull();
        resolved.GetParameters()[0].ParameterType.ShouldBe(typeof(EsFixA.ItemAdded));
    }

    [Fact]
    public void RehydrateState_LegacyShortName_Applies() {
        OnlyItemAddedState state = Rehydrate<OnlyItemAddedState>(
            EventArray("ItemAdded", """{"Name":"legacy"}"""));

        state.Applied.ShouldBe("ItemAdded:legacy");
    }

    [Fact]
    public void ProjectFromJson_LegacyShortName_Applies() {
        OnlyItemAddedState model = new OnlyItemAddedProjection().ProjectFromJson(
            EventArray("ItemAdded", """{"Name":"legacy"}"""));

        model.Applied.ShouldBe("ItemAdded:legacy");
    }

    // --- I/O matrix: ambiguous short name ---

    [Fact]
    public void TryResolve_AmbiguousShortName_ThrowsNamingEveryCandidate() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(AmbiguousShortState));

        AmbiguousApplyMethodException exception = Should.Throw<AmbiguousApplyMethodException>(
            () => ApplyMethodResolver.TryResolve(table, "ItemAdded"));

        exception.EventTypeName.ShouldBe("ItemAdded");
        exception.StateType.ShouldBe(typeof(AmbiguousShortState));
        exception.CandidateCount.ShouldBe(2);
        exception.CandidateEventTypeNames.ShouldBe([
            typeof(EsFixA.ItemAdded).FullName!,
            typeof(EsFixB.ItemAdded).FullName!,
        ]);
        exception.Message.ShouldContain(typeof(EsFixA.ItemAdded).FullName!);
        exception.Message.ShouldContain(typeof(EsFixB.ItemAdded).FullName!);
    }

    [Fact]
    public void TryResolve_AmbiguousShortNameCandidates_AreDeduplicatedAndOrdinallySorted() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(AmbiguousShortState));

        AmbiguousApplyMethodException first = Should.Throw<AmbiguousApplyMethodException>(
            () => ApplyMethodResolver.TryResolve(table, "ItemAdded"));
        AmbiguousApplyMethodException second = Should.Throw<AmbiguousApplyMethodException>(
            () => ApplyMethodResolver.TryResolve(ApplyMethodResolver.BuildTable(typeof(AmbiguousShortState)), "ItemAdded"));

        first.CandidateEventTypeNames.ShouldBe(
            [.. first.CandidateEventTypeNames.OrderBy(static name => name, StringComparer.Ordinal)]);
        first.CandidateEventTypeNames.Distinct(StringComparer.Ordinal).Count()
            .ShouldBe(first.CandidateEventTypeNames.Count);
        second.Message.ShouldBe(first.Message);
    }

    [Fact]
    public void TryResolve_AmbiguousShortNameAddressedByFullName_StillBinds() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(AmbiguousShortState));

        MethodInfo? resolved = ApplyMethodResolver.TryResolve(table, typeof(EsFixB.ItemAdded).FullName!);

        resolved.ShouldNotBeNull();
        resolved.GetParameters()[0].ParameterType.ShouldBe(typeof(EsFixB.ItemAdded));
    }

    [Fact]
    public void RehydrateState_AmbiguousShortName_Throws() {
        JsonElement events = EventArray("ItemAdded", """{"Name":"x"}""");

        _ = Should.Throw<AmbiguousApplyMethodException>(() => Rehydrate<AmbiguousShortState>(events));
    }

    [Fact]
    public void ProjectFromJson_AmbiguousShortName_Throws() {
        JsonElement events = EventArray("ItemAdded", """{"Name":"x"}""");
        var projection = new AmbiguousShortProjection();

        _ = Should.Throw<AmbiguousApplyMethodException>(() => projection.ProjectFromJson(events));
    }

    // --- I/O matrix: nested-type stored name ('+' is a name boundary) ---

    [Fact]
    public void TryResolve_NestedTypeStoredName_BindsAcrossThePlusBoundary() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(OnlyItemAddedState));

        MethodInfo? resolved = ApplyMethodResolver.TryResolve(table, "Foreign.Order+ItemAdded");

        resolved.ShouldNotBeNull();
        resolved.GetParameters()[0].ParameterType.ShouldBe(typeof(EsFixA.ItemAdded));
    }

    [Fact]
    public void RehydrateState_NestedTypeStoredName_Applies() {
        OnlyItemAddedState state = Rehydrate<OnlyItemAddedState>(
            EventArray("Foreign.Order+ItemAdded", """{"Name":"nested"}"""));

        state.Applied.ShouldBe("ItemAdded:nested");
    }

    [Fact]
    public void ProjectFromJson_NestedTypeStoredName_Applies() {
        OnlyItemAddedState model = new OnlyItemAddedProjection().ProjectFromJson(
            EventArray("Foreign.Order+ItemAdded", """{"Name":"nested"}"""));

        model.Applied.ShouldBe("ItemAdded:nested");
    }

    [Fact]
    public void TryResolve_NestedTypeNearMiss_DoesNotMatchAcrossThePlusBoundary() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(OnlyItemAddedState));

        ApplyMethodResolver.TryResolve(table, "Foreign.Order+SubItemAdded").ShouldBeNull();
    }

    // --- I/O matrix: assembly-qualified stored name ---

    [Fact]
    public void TryResolve_AssemblyQualifiedStoredName_StripsQualificationBeforeMatching() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(OnlyItemAddedState));

        MethodInfo? exact = ApplyMethodResolver.TryResolve(
            table,
            typeof(EsFixA.ItemAdded).FullName + ", MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
        MethodInfo? suffix = ApplyMethodResolver.TryResolve(table, "Foreign.ItemAdded, MyAsm, Version=1.0.0.0");

        exact.ShouldNotBeNull();
        exact.GetParameters()[0].ParameterType.ShouldBe(typeof(EsFixA.ItemAdded));
        suffix.ShouldNotBeNull();
        suffix.GetParameters()[0].ParameterType.ShouldBe(typeof(EsFixA.ItemAdded));
    }

    [Fact]
    public void RehydrateState_AssemblyQualifiedStoredName_Applies() {
        OnlyItemAddedState state = Rehydrate<OnlyItemAddedState>(
            EventArray(
                typeof(EsFixA.ItemAdded).FullName + ", MyAsm, Version=1.0.0.0",
                """{"Name":"qualified"}"""));

        state.Applied.ShouldBe("ItemAdded:qualified");
    }

    [Fact]
    public void ProjectFromJson_AssemblyQualifiedStoredName_Applies() {
        OnlyItemAddedState model = new OnlyItemAddedProjection().ProjectFromJson(
            EventArray(
                typeof(EsFixA.ItemAdded).FullName + ", MyAsm, Version=1.0.0.0",
                """{"Name":"qualified"}"""));

        model.Applied.ShouldBe("ItemAdded:qualified");
    }

    // --- I/O matrix: two candidates, different depth (longest anchored match wins) ---

    [Fact]
    public void TryResolve_TwoCandidatesDifferentDepth_BindsTheLongestAnchoredMatch() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(DepthState));

        MethodInfo? resolved = ApplyMethodResolver.TryResolve(
            table,
            "Outer." + typeof(EsFixOuter.EsFixDeep.Foo).FullName);

        resolved.ShouldNotBeNull();
        resolved.GetParameters()[0].ParameterType.ShouldBe(typeof(EsFixOuter.EsFixDeep.Foo));
    }

    [Fact]
    public void RehydrateState_TwoCandidatesDifferentDepth_AppliesTheLongestAnchoredMatch() {
        DepthState state = Rehydrate<DepthState>(
            EventArray("Outer." + typeof(EsFixOuter.EsFixDeep.Foo).FullName, """{"Name":"deep"}"""));

        state.Applied.ShouldBe("EsFixOuter.EsFixDeep.Foo:deep");
    }

    [Fact]
    public void ProjectFromJson_TwoCandidatesDifferentDepth_AppliesTheLongestAnchoredMatch() {
        DepthState model = new DepthProjection().ProjectFromJson(
            EventArray("Outer." + typeof(EsFixOuter.EsFixDeep.Foo).FullName, """{"Name":"deep"}"""));

        model.Applied.ShouldBe("EsFixOuter.EsFixDeep.Foo:deep");
    }

    // --- I/O matrix: two candidates, equal depth (a longest-match tie is genuine ambiguity) ---

    [Fact]
    public void TryResolve_TwoCandidatesEqualDepth_Throws() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(EqualDepthState));

        AmbiguousApplyMethodException exception = Should.Throw<AmbiguousApplyMethodException>(
            () => ApplyMethodResolver.TryResolve(table, "Outer.Foo"));

        exception.CandidateCount.ShouldBe(2);
        exception.CandidateEventTypeNames.ShouldBe([
            typeof(EsFixA.Foo).FullName!,
            typeof(EsFixB.Foo).FullName!,
        ]);
    }

    [Fact]
    public void RehydrateState_TwoCandidatesEqualDepth_Throws() {
        JsonElement events = EventArray("Outer.Foo", """{"Name":"tie"}""");

        _ = Should.Throw<AmbiguousApplyMethodException>(() => Rehydrate<EqualDepthState>(events));
    }

    [Fact]
    public void ProjectFromJson_TwoCandidatesEqualDepth_Throws() {
        JsonElement events = EventArray("Outer.Foo", """{"Name":"tie"}""");
        var projection = new EqualDepthProjection();

        _ = Should.Throw<AmbiguousApplyMethodException>(() => projection.ProjectFromJson(events));
    }

    // --- Ambiguity symmetry: the runtime-type entry point must not silently return null ---

    [Fact]
    public void TryResolve_RuntimeTypeUnderShortNameAmbiguity_BindsExactlyInsteadOfReturningNull() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(AmbiguousShortState));

        MethodInfo? resolved = ApplyMethodResolver.TryResolve(table, typeof(EsFixB.ItemAdded));

        resolved.ShouldNotBeNull();
        resolved.GetParameters()[0].ParameterType.ShouldBe(typeof(EsFixB.ItemAdded));
    }

    [Fact]
    public void Project_RuntimeTypeUnderShortNameAmbiguity_DoesNotSilentlyDropTheEvent() {
        AmbiguousShortState model = new AmbiguousShortProjection()
            .Project(new object[] { new EsFixB.ItemAdded { Name = "runtime" } });

        model.Applied.ShouldBe("EsFixB.ItemAdded:runtime");
    }

    [Fact]
    public void RehydrateState_RuntimeTypeUnderShortNameAmbiguity_DoesNotThrow() {
        AmbiguousShortState? state = DomainProcessorStateRehydrator.RehydrateState<AmbiguousShortState>(
            new object[] { new EsFixA.ItemAdded { Name = "runtime" } },
            ApplyMethodResolver.GetOrBuildTable(typeof(AmbiguousShortState)));

        state.ShouldNotBeNull();
        state.Applied.ShouldBe("EsFixA.ItemAdded:runtime");
    }

    // --- Suffix-key union: a key that is one type's full name and another's short name ---

    [Fact]
    public void TryResolve_SuffixKeyClaimedByBothANameForms_ThrowsInsteadOfSelectingOneMap() {
        // A global-namespace type registers key "EsFixGlobalCollider" as its FULL name; the namespaced
        // type registers the same key as its SHORT name. Reading only one of the two maps during the
        // suffix scan silently bound whichever map was consulted first.
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(GlobalNamespaceCollisionState));

        AmbiguousApplyMethodException exception = Should.Throw<AmbiguousApplyMethodException>(
            () => ApplyMethodResolver.TryResolve(table, "Foreign.EsFixGlobalCollider"));

        exception.CandidateCount.ShouldBe(2);
        exception.CandidateEventTypeNames.ShouldBe([
            typeof(EsFixA.EsFixGlobalCollider).FullName!,
            typeof(EsFixGlobalCollider).FullName!,
        ]);
    }

    [Fact]
    public void RehydrateState_SuffixKeyClaimedByBothNameForms_Throws() {
        JsonElement events = EventArray("Foreign.EsFixGlobalCollider", """{"Name":"x"}""");

        _ = Should.Throw<AmbiguousApplyMethodException>(() => Rehydrate<GlobalNamespaceCollisionState>(events));
    }

    [Fact]
    public void ProjectFromJson_SuffixKeyClaimedByBothNameForms_Throws() {
        JsonElement events = EventArray("Foreign.EsFixGlobalCollider", """{"Name":"x"}""");
        var projection = new GlobalNamespaceCollisionProjection();

        _ = Should.Throw<AmbiguousApplyMethodException>(() => projection.ProjectFromJson(events));
    }

    [Fact]
    public void TryResolve_GlobalNamespaceTypeAddressedByItsFullName_StillBindsExactly() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(GlobalNamespaceCollisionState));

        MethodInfo? resolved = ApplyMethodResolver.TryResolve(table, typeof(EsFixGlobalCollider).FullName!);

        resolved.ShouldNotBeNull();
        resolved.GetParameters()[0].ParameterType.ShouldBe(typeof(EsFixGlobalCollider));
    }

    [Fact]
    public void BuildTable_GlobalNamespaceTypeAlone_RegistersOneCandidateNotTwo() {
        // The suffix map registers both alias forms unconditionally; for a type outside any namespace both
        // are the same string, so the candidate builder must de-duplicate the identical declaration.
        ApplyMethodTable table = ApplyMethodResolver.BuildTable(typeof(GlobalNamespaceOnlyState));

        table.Count.ShouldBe(1);
        table.BySuffixKey[nameof(EsFixGlobalCollider)].CandidateCount.ShouldBe(1);
        ApplyMethodResolver.TryResolve(table, "Foreign.EsFixGlobalCollider").ShouldNotBeNull();
    }

    // --- Constructed generics: assembly qualification is nested inside the name ---

    [Fact]
    public void TryResolve_ClosedGenericStoredName_DoesNotTruncateIntoASuffixMatch() {
        // Splitting on the first comma truncated this name to "…Wrapper`1[[EsFixA.ItemAdded", which then
        // anchored on ".ItemAdded" and silently bound Apply(ItemAdded) for a Wrapper<ItemAdded> event.
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(OnlyItemAddedState));

        ApplyMethodResolver.TryResolve(table, typeof(EsFixA.Wrapper<EsFixA.ItemAdded>).AssemblyQualifiedName!)
            .ShouldBeNull();
    }

    [Fact]
    public void TryResolve_ClosedGenericStoredName_BindsRegardlessOfAssemblyQualification() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(GenericEventState));

        MethodInfo? fromFullName = ApplyMethodResolver.TryResolve(
            table,
            typeof(EsFixA.Wrapper<EsFixA.ItemAdded>).FullName!);
        MethodInfo? fromAssemblyQualified = ApplyMethodResolver.TryResolve(
            table,
            typeof(EsFixA.Wrapper<EsFixA.ItemAdded>).AssemblyQualifiedName!);

        fromFullName.ShouldNotBeNull();
        fromFullName.GetParameters()[0].ParameterType.ShouldBe(typeof(EsFixA.Wrapper<EsFixA.ItemAdded>));
        fromAssemblyQualified.ShouldBeSameAs(fromFullName);
    }

    [Fact]
    public void RehydrateState_ClosedGenericStoredName_Applies() {
        GenericEventState state = Rehydrate<GenericEventState>(
            EventArray(typeof(EsFixA.Wrapper<EsFixA.ItemAdded>).AssemblyQualifiedName!, """{"Name":"generic"}"""));

        state.Applied.ShouldBe("Wrapper:generic");
    }

    // --- Runtime-type resolution must never return a non-assignable method ---

    [Fact]
    public void TryResolve_RuntimeTypeMatchingOnlyByShortName_IsTreatedAsNoMatch() {
        // EsFixB.ItemAdded anchors on the short-name key "ItemAdded", whose only candidate is
        // Apply(EsFixA.ItemAdded). Invoking it would surface as an opaque reflection ArgumentException.
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(OnlyItemAddedState));

        ApplyMethodResolver.TryResolve(table, typeof(EsFixB.ItemAdded)).ShouldBeNull();
    }

    [Fact]
    public void Project_RuntimeTypeMatchingOnlyByShortName_SkipsInsteadOfThrowingReflectionError() {
        OnlyItemAddedState model = new OnlyItemAddedProjection()
            .Project(new object[] { new EsFixB.ItemAdded { Name = "foreign" } });

        model.Applied.ShouldBeNull();
    }

    [Fact]
    public void RehydrateState_RuntimeTypeMatchingOnlyByShortName_ThrowsMissingApplyMethod() {
        _ = Should.Throw<MissingApplyMethodException>(
            () => DomainProcessorStateRehydrator.RehydrateState<OnlyItemAddedState>(
                new object[] { new EsFixB.ItemAdded { Name = "foreign" } },
                ApplyMethodResolver.GetOrBuildTable(typeof(OnlyItemAddedState))));
    }

    [Fact]
    public void TryResolve_RuntimeTypeAssignableToTheMatchedParameter_IsKept() {
        // EsFixA.Holder+BaseNotification anchors on "BaseNotification" across the '+' boundary AND really
        // derives from EsFixA.BaseNotification, so the assignability guard must not reject it.
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(BaseNotificationState));

        MethodInfo? resolved = ApplyMethodResolver.TryResolve(table, typeof(EsFixA.Holder.BaseNotification));

        resolved.ShouldNotBeNull();
        resolved.GetParameters()[0].ParameterType.ShouldBe(typeof(EsFixA.BaseNotification));
    }

    [Fact]
    public void Project_RuntimeTypeAssignableToTheMatchedParameter_IsApplied() {
        BaseNotificationState model = new BaseNotificationProjection()
            .Project(new object[] { new EsFixA.Holder.BaseNotification { Name = "derived" } });

        model.Applied.ShouldBe("BaseNotification:derived");
    }

    // --- Ambiguity diagnostics must carry stream identity on the envelope path ---

    [Fact]
    public async Task ProcessAsync_EnvelopeWithAmbiguousEventTypeName_CarriesMessageAndAggregateId() {
        var aggregate = new AmbiguousShortAggregate();
        string messageId = UniqueIdHelper.GenerateSortableUniqueStringId();
        EventEnvelope envelope = new(
            new EventMetadata(
                MessageId: messageId,
                AggregateId: "agg-4-3",
                AggregateType: "ambiguous-short",
                TenantId: "tenant-1",
                Domain: "resolver",
                SequenceNumber: 2,
                GlobalPosition: 2,
                Timestamp: DateTimeOffset.UtcNow,
                CorrelationId: "corr-1",
                CausationId: "corr-1",
                UserId: "user-1",
                DomainServiceVersion: "v1",
                EventTypeName: "ItemAdded",
                MetadataVersion: 1,
                SerializationFormat: "json"),
            JsonSerializer.SerializeToUtf8Bytes(new { }),
            null);
        var currentState = new DomainServiceCurrentState(new AmbiguousShortState(), [envelope], 1, 2);
        var command = new CommandEnvelope(
            MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
            TenantId: "tenant-1",
            Domain: "resolver",
            AggregateId: "agg-4-3",
            CommandType: nameof(TouchAggregate),
            Payload: JsonSerializer.SerializeToUtf8Bytes(new TouchAggregate()),
            CorrelationId: "corr-1",
            CausationId: null,
            UserId: "user-1",
            Extensions: null);

        AmbiguousApplyMethodException exception = await Should.ThrowAsync<AmbiguousApplyMethodException>(
            () => aggregate.ProcessAsync(command, currentState));

        exception.MessageId.ShouldBe(messageId);
        exception.AggregateId.ShouldBe("agg-4-3");
        exception.EventTypeName.ShouldBe("ItemAdded");
        exception.Message.ShouldContain(messageId);
        exception.Message.ShouldContain("agg-4-3");
    }

    // --- Replay path: ambiguity becomes a categorized failure, not an escaping exception ---

    [Fact]
    public void Replay_AmbiguousShortName_ReturnsCategorizedFailureInsteadOfThrowing() {
        AggregateReconstructionResult result = AggregateReplayer.Replay<AmbiguousShortState>(
            ReplayRequest("ItemAdded", """{"Name":"x"}"""));

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnknownEventType);
        result.FailedSequenceNumber.ShouldBe(1);
        result.FailedEventType.ShouldBe("ItemAdded");
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain(typeof(EsFixA.ItemAdded).FullName!);
        result.Message.ShouldContain(typeof(EsFixB.ItemAdded).FullName!);
    }

    [Fact]
    public void Replay_SuffixCollision_AppliesTheLongerShortName() {
        AggregateReconstructionResult result = AggregateReplayer.Replay<SuffixCollisionState>(
            ReplayRequest("Foreign.SubItemAdded", """{"Name":"sub"}"""));

        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        result.StateJson.ShouldNotBeNull();
        result.StateJson.ShouldContain("SubItemAdded:sub");
    }

    // --- Registration rules ---

    [Fact]
    public void BuildTable_GenericApplyOverload_IsNotRegistered() {
        ApplyMethodTable table = ApplyMethodResolver.BuildTable(typeof(GenericApplyState));

        table.Count.ShouldBe(0);
        ApplyMethodResolver.TryResolve(table, "T").ShouldBeNull();
        ApplyMethodResolver.TryResolve(table, typeof(EsFixA.ItemAdded).FullName!).ShouldBeNull();
    }

    [Fact]
    public void BuildTable_ByRefApplyOverload_IsNotRegistered() {
        ApplyMethodTable table = ApplyMethodResolver.BuildTable(typeof(ByRefApplyState));

        table.Count.ShouldBe(1);
        MethodInfo? resolved = ApplyMethodResolver.TryResolve(table, typeof(EsFixA.ItemAdded).FullName!);
        resolved.ShouldNotBeNull();
        resolved.GetParameters()[0].ParameterType.IsByRef.ShouldBeFalse();
    }

    [Fact]
    public void TryResolve_DuplicateFullNameFromHiddenOverload_FailsLoudlyInsteadOfLastWriterWins() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(HidingDerivedState));

        AmbiguousApplyMethodException exception = Should.Throw<AmbiguousApplyMethodException>(
            () => ApplyMethodResolver.TryResolve(table, typeof(EsFixA.ItemAdded).FullName!));

        exception.CandidateCount.ShouldBe(2);
        exception.CandidateEventTypeNames.ShouldBe([typeof(EsFixA.ItemAdded).FullName!]);
    }

    [Fact]
    public void TryResolve_EmptyOrWhitespaceStoredName_ReturnsNull() {
        ApplyMethodTable table = ApplyMethodResolver.GetOrBuildTable(typeof(OnlyItemAddedState));

        ApplyMethodResolver.TryResolve(table, string.Empty).ShouldBeNull();
        ApplyMethodResolver.TryResolve(table, "   ").ShouldBeNull();
    }

    private static AggregateReconstructionRequest ReplayRequest(string eventTypeName, string payloadJson)
        => new(
            TenantId: "tenant-1",
            Domain: "resolver",
            AggregateType: string.Empty,
            AggregateId: "agg-1",
            UpToSequence: 1,
            Events: [
                new ReplayEventEnvelope(
                    SequenceNumber: 1,
                    EventTypeName: eventTypeName,
                    Payload: Encoding.UTF8.GetBytes(payloadJson),
                    SerializationFormat: "json",
                    MetadataVersion: 1,
                    MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
                    CorrelationId: "corr-1",
                    CausationId: null),
            ],
            IncludeTimeline: false,
            RequestId: null);

    private static TState Rehydrate<TState>(JsonElement events)
        where TState : class, new()
        => DomainProcessorStateRehydrator.RehydrateState<TState>(
            events,
            ApplyMethodResolver.GetOrBuildTable(typeof(TState)))!;

    private static JsonElement EventArray(string eventTypeName, string payloadJson)
        => JsonSerializer.Deserialize<JsonElement>(
            "[{\"eventTypeName\":"
            + JsonSerializer.Serialize(eventTypeName)
            + ",\"payload\":"
            + payloadJson
            + "}]");
}
