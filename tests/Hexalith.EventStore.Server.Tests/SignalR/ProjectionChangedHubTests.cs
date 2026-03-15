using Hexalith.EventStore.CommandApi.SignalR;

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
        ProjectionChangedHub sut = CreateHub("conn-join", groups, maxGroupsPerConnection: 5);

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
    public async Task JoinGroup_MaxGroupsExceeded_ThrowsHubException() {
        ProjectionChangedHub sut = CreateHub("conn-max", Substitute.For<IGroupManager>(), maxGroupsPerConnection: 1);

        await sut.JoinGroup("order-list", "acme");

        HubException ex = await Should.ThrowAsync<HubException>(() =>
            sut.JoinGroup("invoice-list", "acme"));

        ex.Message.ShouldContain("Maximum groups per connection");
    }

    [Fact]
    public async Task LeaveGroup_ValidInput_RemovesConnectionFromGroup() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        ProjectionChangedHub sut = CreateHub("conn-leave", groups, maxGroupsPerConnection: 5);
        await sut.JoinGroup("order-list", "acme");

        await sut.LeaveGroup("order-list", "acme");

        await groups.Received(1).RemoveFromGroupAsync("conn-leave", "order-list:acme", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinGroup_AddToGroupFailure_DoesNotConsumeQuota() {
        IGroupManager groups = Substitute.For<IGroupManager>();
        _ = groups.AddToGroupAsync("conn-failure", "order-list:acme", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("add failed")));

        ProjectionChangedHub sut = CreateHub("conn-failure", groups, maxGroupsPerConnection: 1);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => sut.JoinGroup("order-list", "acme"));
        await Should.NotThrowAsync(() => sut.JoinGroup("invoice-list", "acme"));
    }

    private static ProjectionChangedHub CreateHub(string connectionId, IGroupManager groups, int maxGroupsPerConnection = 50) {
        HubCallerContext context = Substitute.For<HubCallerContext>();
        _ = context.ConnectionId.Returns(connectionId);

        ProjectionChangedHub hub = new(
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
}