using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>Checks capture requests fall back through both default interface members.</summary>
public sealed class LegacyInvokerDelegationTests
{
    [Fact]
    public async Task CaptureOverloadsDelegateToLegacyInvokerAndPreserveResult()
    {
        DomainResult response = DomainResult.NoOp();
        var implementation = new LegacyOnlyDomainServiceInvoker(response);
        IDomainServiceInvoker invoker = implementation;
        var command = new CommandEnvelope("01HZX6FKA8B3YTX6MBQ3AYT3PT", "tenant", "domain", "aggregate",
            "Do", [], "01HZX6FKA8B3YTX6MBQ3AYT3PT", null, "user", null);

        (await invoker.InvokeAsync(command, null, true, "qualified-v1")).ShouldBeSameAs(response);
        (await invoker.InvokeAsync(command, null, true)).ShouldBeSameAs(response);
        implementation.CallCount.ShouldBe(2);
        response.PostCommandStateJson.ShouldBeNull();
    }
}
