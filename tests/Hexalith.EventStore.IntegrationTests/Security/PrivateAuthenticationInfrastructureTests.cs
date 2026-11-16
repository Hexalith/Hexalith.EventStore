using Shouldly;
using System.Text.Json;

namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>Checks temporary custody selection independently of checkout depth.</summary>
public sealed class PrivateAuthenticationInfrastructureTests
{
    /// <summary>Retries failed ownership release before marking composite disposal complete.</summary>
    [Fact]
    public async Task FailedDisposalRetainsOwnershipAndCanBeRetried()
    {
        string identity = new('d', 64);
        int removals = 0;
        OwnedAuthenticationContainer? container = null;
        container = new OwnedAuthenticationContainer((arguments, _) =>
        {
            if (arguments[0] == "run") return Task.FromResult(identity);
            if (arguments[0] == "inspect") return Task.FromResult(JsonSerializer.Serialize(new { id = identity, owner = container!.Owner }));
            if (++removals == 1) throw new InvalidOperationException("Temporary Docker failure");
            return Task.FromResult(identity);
        });
        await container.StartAsync("unused", null, 6379, [], TestContext.Current.CancellationToken);
        var infrastructure = new PrivateAuthenticationInfrastructure(container);
        try
        {
            await Should.ThrowAsync<AggregateException>(() => infrastructure.DisposeAsync().AsTask());
            await infrastructure.DisposeAsync();
            await infrastructure.DisposeAsync();
            removals.ShouldBe(2);
        }
        finally { await infrastructure.DisposeAsync(); }
    }

    /// <summary>Keeps custody under private scratch and refuses both Git files and directories in ancestors.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustodyUsesOwnedTemporarySpaceAndRejectsEnclosingCheckouts(bool gitDirectory)
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("private-auth-custody-control-");
        try
        {
            string selected = PrivateAuthenticationInfrastructure.CreateTransportCustodyPath(root.FullName);
            Path.GetDirectoryName(selected).ShouldBe(root.FullName);
            Path.Exists(selected).ShouldBeFalse();
            DirectoryInfo checkout = root.CreateSubdirectory("checkout");
            if (gitDirectory) Directory.CreateDirectory(Path.Combine(checkout.FullName, ".git"));
            else File.WriteAllText(Path.Combine(checkout.FullName, ".git"), "gitdir: unrelated-metadata");
            DirectoryInfo nested = checkout.CreateSubdirectory("nested/checkouts/private-temp");
            Should.Throw<InvalidOperationException>(() => PrivateAuthenticationInfrastructure.CreateTransportCustodyPath(nested.FullName));
            Should.Throw<InvalidOperationException>(() => PrivateAuthenticationInfrastructure.CreateTransportCustodyPath("relative-temp"));
            Should.Throw<InvalidOperationException>(() => PrivateAuthenticationInfrastructure.CreateTransportCustodyPath(Path.Combine(root.FullName, "missing")));
        }
        finally { root.Delete(recursive: true); }
    }
}
