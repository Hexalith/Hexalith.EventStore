using System.Security.Claims;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.SignalRHub;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.SignalR;

public class ProjectionChangedHubTests {
    public static TheoryData<string, string> InvalidGrantInvocations
    {
        get
        {
            var cases = new TheoryData<string, string>();
            foreach (string invocation in new[] { "join", "scoped-join", "validate", "reconcile" })
            {
                foreach (string grant in new[] { "", " ", "bad!", "system", "ACME", "acme" })
                {
                    cases.Add(invocation, grant);
                }
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(InvalidGrantInvocations))]
    public async Task ProjectionAdmission_MalformedOrDuplicateGrant_RejectsBeforeValidationOrMembership(
        string invocation, string invalidGrant)
    {
        string connectionId = "invalid-grant-" + Guid.NewGuid().ToString("N");
        IGroupManager groups = Substitute.For<IGroupManager>();
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        _ = tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "user-1"),
            new Claim("eventstore:tenant", "acme"),
            new Claim("eventstore:tenant", invalidGrant),
        ], "Bearer"));
        ProjectionChangedHub sut = CreateHub(connectionId, groups, user: user, tenantValidator: tenantValidator);

        try
        {
            HubException ex = await Should.ThrowAsync<HubException>(() => InvokeProjectionAdmissionAsync(sut, invocation));

            ex.Message.ShouldBe("Tenant authorization failed.");
            _ = await tenantValidator.DidNotReceive().ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>(), Arg.Any<string?>());
            await groups.DidNotReceive().AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            await groups.DidNotReceive().RemoveFromGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            await sut.OnDisconnectedAsync(null);
        }
    }

    [Theory]
    [InlineData("join")]
    [InlineData("scoped-join")]
    [InlineData("validate")]
    [InlineData("reconcile")]
    public async Task ProjectionAdmission_ValidDistinctGrants_ForwardsCanonicalExplicitTenant(string invocation)
    {
        string connectionId = "canonical-admission-" + Guid.NewGuid().ToString("N");
        IGroupManager groups = Substitute.For<IGroupManager>();
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        _ = tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), "acme",
                Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "user-1"),
            new Claim("eventstore:tenant", "OTHER"),
            new Claim("eventstore:tenant", "ACME"),
        ], "Bearer"));
        ProjectionChangedHub sut = CreateHub(connectionId, groups, user: user, tenantValidator: tenantValidator);

        try
        {
            await InvokeProjectionAdmissionAsync(sut, invocation);

            _ = await tenantValidator.Received(1).ValidateAsync(user, "acme",
                Arg.Any<CancellationToken>(), Arg.Any<string?>());
            if (invocation == "validate")
            {
                await groups.DidNotReceive().AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            }
            else
            {
                string group = invocation == "join" ? "order-list:acme" : "order-list:acme:scope-1";
                await groups.Received(1).AddToGroupAsync(connectionId, group, Arg.Any<CancellationToken>());
            }
        }
        finally
        {
            await sut.OnDisconnectedAsync(null);
        }
    }

    [Fact]
    public async Task CanceledSnapshotAdmissionRemovesNewMembershipState()
    {
        string id = "canceled-snapshot-" + Guid.NewGuid().ToString("N");
        ProjectionChangedHub hub = CreateHub(id, Substitute.For<IGroupManager>(), user: CreateUser("acme"));
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        hub.Context.ConnectionAborted.Returns(canceled.Token);
        await Should.ThrowAsync<OperationCanceledException>(() => hub.ReconcileProjectionMembership(1, []));
        var field = typeof(ProjectionChangedHub).GetField("_membershipStates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        ((System.Collections.IDictionary)field.GetValue(null)!).Contains(id).ShouldBeFalse();
    }

    [Fact]
    public async Task JoinGroup_ValidInput_AddsConnectionToGroup() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-join", groups, maxGroupsPerConnection: 5, user: CreateUser("acme"));

        await sut.JoinGroup("order-list", "acme");

        await groups.Received(1).AddToGroupAsync("conn-join", "order-list:acme", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_ProjectionTypeContainsColon_ThrowsHubException() {
        ProjectionChangedHub sut = CreateHub("conn-invalid", Substitute.For<IGroupManager>());

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.JoinGroup("order:list", "acme"));

        ex.Message.ShouldContain("must not contain colons");
    }

    [Fact]
    public async Task JoinGroup_ProjectionTypeContainsColon_RejectsBeforeAuthorization() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        ProjectionChangedHub sut = CreateHub(
            "conn-invalid-before-auth",
            groups,
            maxGroupsPerConnection: 1,
            user: CreateUser("acme"),
            tenantValidator: tenantValidator);

        _ = await Should.ThrowAsync<HubException>(() =>
            sut.JoinGroup("order:list", "acme"));

        _ = await tenantValidator.DidNotReceive().ValidateAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>());
        await groups.DidNotReceive().AddToGroupAsync(
            "conn-invalid-before-auth", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_MaxGroupsExceeded_ThrowsHubException() {
        ProjectionChangedHub sut = CreateHub("conn-max", Substitute.For<IGroupManager>(), maxGroupsPerConnection: 1, user: CreateUser("acme"));

        await sut.JoinGroup("order-list", "acme");

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.JoinGroup("invoice-list", "acme"));

        ex.Message.ShouldContain("Maximum groups per connection");
    }

    [Fact]
    public async Task LeaveGroup_ValidInput_RemovesConnectionFromGroup() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-leave", groups, maxGroupsPerConnection: 5, user: CreateUser("acme"));
        await sut.JoinGroup("order-list", "acme");

        await sut.LeaveGroup("order-list", "acme");

        await groups.Received(1).RemoveFromGroupAsync("conn-leave", "order-list:acme", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_AddToGroupFailure_DoesNotConsumeQuota() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        _ = groups.AddToGroupAsync("conn-failure", "order-list:acme", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("add failed")));

        ProjectionChangedHub sut = CreateHub("conn-failure", groups, maxGroupsPerConnection: 1, user: CreateUser("acme"));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => sut.JoinGroup("order-list", "acme"));
        await Should.NotThrowAsync(() => sut.JoinGroup("invoice-list", "acme"));
    }

    [Fact]
    public async Task JoinGroup_UnauthenticatedUser_ThrowsHubExceptionWithoutAddingGroupOrConsumingQuota() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        HubCallerContext context = Substitute.For<HubCallerContext>();
        _ = context.ConnectionId.Returns("conn-anonymous-recovery");
        _ = context.User.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
        _ = context.ConnectionAborted.Returns(default(CancellationToken));

        ProjectionChangedHub sut = new(
            tenantValidator,
            Options.Create(new SignalROptions { Enabled = true, MaxGroupsPerConnection = 1 }),
            Substitute.For<ILogger<ProjectionChangedHub>>()) {
            Context = context,
            Groups = groups,
            Clients = Substitute.For<IHubCallerClients<IProjectionChangedClient>>(),
        };

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.JoinGroup("order-list", "acme"));

        ex.Message.ShouldContain("Authentication is required");
        _ = await tenantValidator.DidNotReceive().ValidateAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>());
        await groups.DidNotReceive().AddToGroupAsync(
            "conn-anonymous-recovery", Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Quota of 1 must remain fully available: switching to an authorized principal
        // on the same connection lets a valid join succeed despite the prior denial.
        _ = context.User.Returns(CreateUser("acme"));
        _ = tenantValidator.ValidateAsync(
                Arg.Any<ClaimsPrincipal>(),
                "acme",
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);

        await sut.JoinGroup("invoice-list", "acme");

        await groups.Received(1).AddToGroupAsync(
            "conn-anonymous-recovery", "invoice-list:acme", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_WrongTenant_ThrowsHubExceptionWithoutAddingGroupOrConsumingQuota() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-wrong-tenant", groups, maxGroupsPerConnection: 1, user: CreateUser("other"));

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.JoinGroup("order-list", "acme"));
        await sut.JoinGroup("invoice-list", "other");

        ex.Message.ShouldBe("Tenant authorization failed.");
        await groups.DidNotReceive().AddToGroupAsync("conn-wrong-tenant", "order-list:acme", Arg.Any<CancellationToken>());
        await groups.Received(1).AddToGroupAsync("conn-wrong-tenant", "invoice-list:other", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_NoTenantClaims_ThrowsHubExceptionWithoutAddingGroupOrConsumingQuota() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub(
            "conn-no-tenant",
            groups,
            maxGroupsPerConnection: 1,
            user: new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")], "Bearer")));

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.JoinGroup("order-list", "acme"));

        ex.Message.ShouldBe("Tenant authorization failed.");
        await groups.DidNotReceive().AddToGroupAsync("conn-no-tenant", "order-list:acme", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_GlobalAdmin_RequiresTenantGrant() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("global_admin", "true")], "Bearer"));
        ProjectionChangedHub sut = CreateHub("conn-admin", groups, maxGroupsPerConnection: 1, user: user);

        HubException ex = await Should.ThrowAsync<HubException>(() => sut.JoinGroup("order-list", "any-tenant"));

        ex.Message.ShouldBe("Tenant authorization failed.");
        await groups.DidNotReceive().AddToGroupAsync("conn-admin", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_TenantComparisonNormalizesCaseForGrantedUsers() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-case", groups, user: CreateUser("ACME"));

        await sut.JoinGroup("order-list", "acme");

        await groups.Received(1).AddToGroupAsync("conn-case", "order-list:acme", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_TenantValidatorThrows_LogsAndThrowsGenericHubException() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        _ = tenantValidator.ValidateAsync(
                Arg.Any<ClaimsPrincipal>(),
                "acme",
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns<Task<TenantValidationResult>>(_ => throw new InvalidOperationException("validator down"));
        ProjectionChangedHub sut = CreateHub(
            "conn-validator-fail",
            groups,
            user: CreateUser("acme"),
            tenantValidator: tenantValidator);

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.JoinGroup("order-list", "acme"));

        ex.Message.ShouldBe("Tenant authorization unavailable.");
        await groups.DidNotReceive().AddToGroupAsync(
            "conn-validator-fail", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_UsesConnectionAbortedTokenForTenantValidation() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        using var cts = new CancellationTokenSource();
        _ = tenantValidator.ValidateAsync(
                Arg.Any<ClaimsPrincipal>(),
                "acme",
                cts.Token,
                Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);
        ProjectionChangedHub sut = CreateHub(
            "conn-cancel",
            groups,
            user: CreateUser("acme"),
            tenantValidator: tenantValidator,
            connectionAborted: cts.Token);

        await sut.JoinGroup("order-list", "acme");

        _ = await tenantValidator.Received(1).ValidateAsync(
            Arg.Any<ClaimsPrincipal>(),
            "acme",
            cts.Token,
            Arg.Any<string?>());
    }

    [Fact]
    public async Task JoinGroup_DeniedThenAllowedOnSameConnection_DoesNotPoisonTrackingState() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-recovery", groups, maxGroupsPerConnection: 1, user: CreateUser("allowed"));

        _ = await Should.ThrowAsync<HubException>(() => sut.JoinGroup("order-list", "denied"));
        await sut.JoinGroup("invoice-list", "allowed");

        await groups.DidNotReceive().AddToGroupAsync("conn-recovery", "order-list:denied", Arg.Any<CancellationToken>());
        await groups.Received(1).AddToGroupAsync("conn-recovery", "invoice-list:allowed", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroupScoped_ValidInput_AddsConnectionToScopedGroup() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-scoped-join", groups, maxGroupsPerConnection: 5, user: CreateUser("acme"));

        await sut.JoinGroupScoped("order-list", "acme", "conv-1");

        await groups.Received(1).AddToGroupAsync("conn-scoped-join", "order-list:acme:conv-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroupScoped_NullScope_FallsBackToTenantWideGroup() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-scoped-null", groups, maxGroupsPerConnection: 5, user: CreateUser("acme"));

        await sut.JoinGroupScoped("order-list", "acme", null);

        await groups.Received(1).AddToGroupAsync("conn-scoped-null", "order-list:acme", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroupScoped_WhitespaceScope_FallsBackToTenantWideGroup() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-scoped-ws", groups, maxGroupsPerConnection: 5, user: CreateUser("acme"));

        await sut.JoinGroupScoped("order-list", "acme", "   ");

        await groups.Received(1).AddToGroupAsync("conn-scoped-ws", "order-list:acme", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroupScoped_ScopeContainsColon_ThrowsHubException() {
        ProjectionChangedHub sut = CreateHub("conn-scoped-colon", Substitute.For<IGroupManager>(), user: CreateUser("acme"));

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.JoinGroupScoped("order-list", "acme", "conv:1"));

        ex.Message.ShouldContain("scope must not contain colons");
    }

    [Fact]
    public async Task JoinGroupScoped_ScopeExceeds64Characters_ThrowsHubExceptionBeforeAddingGroup() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-scoped-long", groups, user: CreateUser("acme"));

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.JoinGroupScoped("order-list", "acme", new string('a', 65)));

        ex.Message.ShouldContain("scope must not exceed 64 characters");
        await groups.DidNotReceive().AddToGroupAsync(
            "conn-scoped-long", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroupScoped_ScopeContainsColon_RejectsBeforeAuthorization() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        ProjectionChangedHub sut = CreateHub(
            "conn-scoped-colon-before-auth",
            groups,
            maxGroupsPerConnection: 1,
            user: CreateUser("acme"),
            tenantValidator: tenantValidator);

        _ = await Should.ThrowAsync<HubException>(() =>
            sut.JoinGroupScoped("order-list", "acme", "conv:1"));

        _ = await tenantValidator.DidNotReceive().ValidateAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>());
        await groups.DidNotReceive().AddToGroupAsync(
            "conn-scoped-colon-before-auth", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroupScoped_WrongTenant_ThrowsWithoutAddingScopedGroup() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-scoped-wrong", groups, maxGroupsPerConnection: 1, user: CreateUser("other"));

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.JoinGroupScoped("order-list", "acme", "conv-1"));

        ex.Message.ShouldBe("Tenant authorization failed.");
        await groups.DidNotReceive().AddToGroupAsync("conn-scoped-wrong", "order-list:acme:conv-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveGroupScoped_ValidInput_RemovesScopedGroup() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-scoped-leave", groups, maxGroupsPerConnection: 5, user: CreateUser("acme"));
        await sut.JoinGroupScoped("order-list", "acme", "conv-1");

        await sut.LeaveGroupScoped("order-list", "acme", "conv-1");

        await groups.Received(1).RemoveFromGroupAsync("conn-scoped-leave", "order-list:acme:conv-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveGroupScoped_ScopeContainsColon_ThrowsHubExceptionBeforeRemovingGroup() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-scoped-leave-colon", groups, user: CreateUser("acme"));

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.LeaveGroupScoped("order-list", "acme", "conv:1"));

        ex.Message.ShouldContain("scope must not contain colons");
        await groups.DidNotReceive().RemoveFromGroupAsync(
            "conn-scoped-leave-colon", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveGroupScoped_ScopeExceeds64Characters_ThrowsHubExceptionBeforeRemovingGroup() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-scoped-leave-long", groups, user: CreateUser("acme"));

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.LeaveGroupScoped("order-list", "acme", new string('a', 65)));

        ex.Message.ShouldContain("scope must not exceed 64 characters");
        await groups.DidNotReceive().RemoveFromGroupAsync(
            "conn-scoped-leave-long", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveGroup_ProjectionTypeContainsColon_ThrowsHubExceptionBeforeRemovingGroup() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-leave-colon", groups, user: CreateUser("acme"));

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.LeaveGroup("order:list", "acme"));

        ex.Message.ShouldContain("must not contain colons");
        await groups.DidNotReceive().RemoveFromGroupAsync(
            "conn-leave-colon", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveGroup_TenantIdContainsColon_ThrowsHubExceptionBeforeRemovingGroup() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-leave-tenant-colon", groups, user: CreateUser("acme"));

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.LeaveGroup("order-list", "ac:me"));

        ex.Message.ShouldContain("must not contain colons");
        await groups.DidNotReceive().RemoveFromGroupAsync(
            "conn-leave-tenant-colon", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LeaveGroup_NullOrWhitespaceProjectionType_ThrowsBeforeRemovingGroup(string? projectionType) {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-leave-null-projection", groups, user: CreateUser("acme"));

        _ = await Should.ThrowAsync<ArgumentException>(() =>
            sut.LeaveGroup(projectionType!, "acme"));

        await groups.DidNotReceive().RemoveFromGroupAsync(
            "conn-leave-null-projection", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LeaveGroup_NullOrWhitespaceTenantId_ThrowsBeforeRemovingGroup(string? tenantId) {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-leave-null-tenant", groups, user: CreateUser("acme"));

        _ = await Should.ThrowAsync<ArgumentException>(() =>
            sut.LeaveGroup("order-list", tenantId!));

        await groups.DidNotReceive().RemoveFromGroupAsync(
            "conn-leave-null-tenant", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveGroupScoped_ProjectionTypeContainsColon_ThrowsHubExceptionBeforeRemovingGroup() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-scoped-leave-projection-colon", groups, user: CreateUser("acme"));

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.LeaveGroupScoped("order:list", "acme", "conv-1"));

        ex.Message.ShouldContain("must not contain colons");
        await groups.DidNotReceive().RemoveFromGroupAsync(
            "conn-scoped-leave-projection-colon", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroupScoped_CountsAgainstPerConnectionQuota() {
        // Scoped groups count against the per-connection cap (proposal §4.3).
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-scoped-quota", groups, maxGroupsPerConnection: 1, user: CreateUser("acme"));

        await sut.JoinGroupScoped("order-list", "acme", "conv-1");

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.JoinGroup("order-list", "acme"));
        ex.Message.ShouldContain("Maximum groups per connection");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcknowledgedSnapshotReplacesAtQuotaAndIgnoresOlderIntent(bool scoped)
    {
        string connection = "snapshot-quota-" + scoped;
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub hub = CreateHub(connection, groups, maxGroupsPerConnection: 1);
        string scope = scoped ? "scope" : string.Empty;
        (await hub.ReconcileProjectionMembership(1, [["old", "acme", scope]])).ShouldBe(1);
        (await hub.ReconcileProjectionMembership(3, [["new", "acme", scope]])).ShouldBe(3);
        (await hub.ReconcileProjectionMembership(2, [])).ShouldBe(3);
        await groups.Received(1).RemoveFromGroupAsync(connection, "old:acme" + (scoped ? ":scope" : string.Empty), Arg.Any<CancellationToken>());
        await groups.DidNotReceive().RemoveFromGroupAsync(connection, "new:acme" + (scoped ? ":scope" : string.Empty), Arg.Any<CancellationToken>());
        await hub.OnDisconnectedAsync(null);
    }

    private static Task InvokeProjectionAdmissionAsync(ProjectionChangedHub hub, string invocation)
        => invocation switch
        {
            "join" => hub.JoinGroup("order-list", "Acme"),
            "scoped-join" => hub.JoinGroupScoped("order-list", "Acme", "scope-1"),
            "validate" => hub.ValidateProjectionGroup("order-list", "Acme", "scope-1"),
            "reconcile" => hub.ReconcileProjectionMembership(1, [["order-list", "Acme", "scope-1"]]),
            _ => throw new ArgumentOutOfRangeException(nameof(invocation)),
        };

    private static ProjectionChangedHub CreateHub(
        string connectionId,
        IGroupManager groups,
        int maxGroupsPerConnection = 50,
        ClaimsPrincipal? user = null,
        ITenantValidator? tenantValidator = null,
        CancellationToken connectionAborted = default) {
        HubCallerContext context = Substitute.For<HubCallerContext>();
        _ = context.ConnectionId.Returns(connectionId);
        _ = context.User.Returns(user ?? CreateUser("acme"));
        _ = context.ConnectionAborted.Returns(connectionAborted);

        tenantValidator ??= new ClaimsTenantValidator();

        ProjectionChangedHub hub = new(
            tenantValidator,
            Options.Create(new SignalROptions {
                Enabled = true,
                MaxGroupsPerConnection = maxGroupsPerConnection,
            }),
            Substitute.For<ILogger<ProjectionChangedHub>>()) {
            Context = context,
            Groups = groups,
            Clients = Substitute.For<IHubCallerClients<IProjectionChangedClient>>(),
        };

        return hub;
    }

    private static ClaimsPrincipal CreateUser(string tenantId) =>
        new(new ClaimsIdentity([new Claim("eventstore:tenant", tenantId), new Claim("sub", "user-1")], "Bearer"));
}
