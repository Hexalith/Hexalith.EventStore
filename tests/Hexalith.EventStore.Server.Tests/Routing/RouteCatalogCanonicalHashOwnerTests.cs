using System.Reflection;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Checks both existing host scope-entry sites without changing option observation order.</summary>
public sealed class RouteCatalogCanonicalHashOwnerTests
{
#if DEBUG
    private static bool PrivateBuild => true;
#else
    private static bool PrivateBuild => false;
#endif

    /// <summary>Only the original complete host selection lends a live pure memo, with the original option reads.</summary>
    [Theory]
    [InlineData("enabled")]
    [InlineData("disabled")]
    [InlineData("retention-disabled")]
    [InlineData("disposed")]
    [InlineData("environment")]
    [InlineData("missing-environment")]
    [InlineData("fixture")]
    [InlineData("technical")]
    [InlineData("profile")]
    public void OriginalHostSelectionAndOptionReadsChooseTheMemo(string changed)
    {
        using var boundary = RouteCatalogDecodeScope.Suppress();
        var local = new RouteCatalogHostOptions
        {
            ScopedDecodeReuse = changed != "disabled", RetainValidatedCatalogBytes = changed != "retention-disabled",
            DevelopmentFixture = changed != "fixture", RequireTechnicalHttpBinding = changed != "technical",
            Profile = changed == "profile" ? "Production" : "Development",
        };
        int reads = 0;
        var options = Substitute.For<IOptions<RouteCatalogHostOptions>>();
        options.Value.Returns(call => { reads++; return local; });
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(changed == "environment" ? Environments.Production : Environments.Development);
        using var authority = new RouteCatalogAuthority(Substitute.For<IRouteCatalogStateStore>(), Substitute.For<IRouteCatalogTrustValidator>(), options,
            environment: changed == "missing-environment" ? null : environment);
        if (changed == "disposed") authority.Dispose();
        using var scope = (RouteCatalogDecodeScope?)typeof(RouteCatalogAuthority)
            .GetMethod("BeginPrivateDecodeScope", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(authority, null);
        bool host = PrivateBuild && changed == "enabled";
        (scope?.HasCanonicalHashMemo == true).ShouldBe(host);
        bool scoped = PrivateBuild && changed is "enabled" or "retention-disabled" or "disposed";
        (scope is not null).ShouldBe(scoped);
        int expected = !PrivateBuild || changed is "environment" or "missing-environment" ? 0
            : changed is "enabled" or "retention-disabled" ? 3 : changed == "disposed" ? 2 : 1;
        reads.ShouldBe(expected);
        var owner = (RouteCatalogCanonicalHashMemo)typeof(RouteCatalogAuthority)
            .GetField("_canonicalHashes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(authority)!;
        CanonicalHashTestInputs.Hash(new { purpose = "owner-selection", schemaVersion = 1 });
        owner.Count.ShouldBe(host ? 1 : 0);
        authority.Dispose();
        owner.IsClosed.ShouldBeTrue();
        owner.Count.ShouldBe(0);
        scope?.HasCanonicalHashMemo.ShouldBeFalse();
    }

    /// <summary>Release uses its original direct host-selected scope and original option-read count, then closes the scope.</summary>
    [Theory]
    [InlineData("enabled")]
    [InlineData("retention-disabled")]
    [InlineData("disposed")]
    public async Task ReleaseEntryBorrowsOnlyItsStillOpenHostOwner(string changed)
    {
        using var boundary = RouteCatalogDecodeScope.Suppress();
        var local = new RouteCatalogHostOptions
        {
            ScopedDecodeReuse = true, RetainValidatedCatalogBytes = changed != "retention-disabled",
            DevelopmentFixture = true, RequireTechnicalHttpBinding = true, Profile = "Development",
        };
        int reads = 0;
        var options = Substitute.For<IOptions<RouteCatalogHostOptions>>();
        options.Value.Returns(call => { reads++; return local; });
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        var store = Substitute.For<IRouteCatalogStateStore>();
        RouteCatalogDecodeScope? observed = null;
        bool selected = false;
        store.ReadAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            observed = RouteCatalogDecodeScope.Current;
            selected = observed?.HasCanonicalHashMemo == true;
            CanonicalHashTestInputs.Hash(new { purpose = "release-entry", schemaVersion = 1 });
            return Task.FromResult((RouteCatalogActivationState.Empty, "synthetic-etag"));
        });
        using var authority = new RouteCatalogAuthority(store, Substitute.For<IRouteCatalogTrustValidator>(), options, environment: environment);
        if (changed == "disposed") authority.Dispose();
        await (Task)typeof(RouteCatalogAuthority).GetMethod("ReleaseCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(authority, ["synthetic-lease"])!;
        selected.ShouldBe(PrivateBuild && changed == "enabled");
        reads.ShouldBe(!PrivateBuild ? 0 : changed == "disposed" ? 2 : 4);
        RouteCatalogDecodeScope.Current.ShouldBeNull();
        if (observed is not null) observed.HasCanonicalHashMemo.ShouldBeFalse();
    }
}
