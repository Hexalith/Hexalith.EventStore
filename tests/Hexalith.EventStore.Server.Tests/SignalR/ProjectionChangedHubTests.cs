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

        await tenantValidator.DidNotReceive().ValidateAsync(
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
        await tenantValidator.DidNotReceive().ValidateAsync(
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
    public async Task JoinGroup_GlobalAdmin_AllowsAnyTenant() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("global_admin", "true")], "Bearer"));
        ProjectionChangedHub sut = CreateHub("conn-admin", groups, maxGroupsPerConnection: 1, user: user);

        await sut.JoinGroup("order-list", "any-tenant");

        await groups.Received(1).AddToGroupAsync("conn-admin", "order-list:any-tenant", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_TenantComparisonIsCaseSensitiveForNonAdminUsers() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-case", groups, user: CreateUser("ACME"));

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.JoinGroup("order-list", "acme"));

        ex.Message.ShouldBe("Tenant authorization failed.");
        await groups.DidNotReceive().AddToGroupAsync("conn-case", "order-list:acme", Arg.Any<CancellationToken>());
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

        await tenantValidator.Received(1).ValidateAsync(
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
