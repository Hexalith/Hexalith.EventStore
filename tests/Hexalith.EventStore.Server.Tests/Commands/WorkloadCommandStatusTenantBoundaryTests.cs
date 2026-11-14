using System.Security.Claims;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Models;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

/// <summary>Verifies workload status requests carry one canonical tenant before state access.</summary>
public sealed class WorkloadCommandStatusTenantBoundaryTests
{
    private const string MessageId = "01ARZ3NDEKTSV4RRFFQ69G5FAV";

    /// <summary>Rejects malformed and reserved route tenants even when the signed binding agrees.</summary>
    /// <param name="tenant">The invalid route tenant and resource binding.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("")]
    [InlineData(" acme")]
    [InlineData("acme ")]
    [InlineData("ac_me")]
    [InlineData("-acme")]
    [InlineData("acme-")]
    [InlineData("system")]
    [InlineData("SYSTEM")]
    [InlineData("platform:tenants")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task InvalidRouteTenantNeverReadsStatus(string tenant)
    {
        ICommandStatusStore store = Substitute.For<ICommandStatusStore>();
        CommandStatusController controller = CreateController(store, tenant);

        (await controller.GetWorkloadStatus(tenant, MessageId, TestContext.Current.CancellationToken))
            .ShouldBeOfType<ForbidResult>();

        store.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Rejects duplicate, conflicting and mixed request sources before reading state.</summary>
    /// <param name="source">The extra scope source supplied with the tenant route.</param>
    /// <param name="value">The duplicate or conflicting value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("X-Hexalith-Tenant", "acme")]
    [InlineData("X-Hexalith-Tenant", "other")]
    [InlineData("X-Hexalith-Scope", "platform:tenants")]
    [InlineData("tenant", "acme")]
    [InlineData("tenantId", "other")]
    public async Task AdditionalRequestScopeNeverReadsStatus(string source, string value)
    {
        ArgumentNullException.ThrowIfNull(source);
        ICommandStatusStore store = Substitute.For<ICommandStatusStore>();
        CommandStatusController controller = CreateController(store, "acme");
        if (source.StartsWith("X-", StringComparison.Ordinal))
        {
            controller.Request.Headers[source] = value;
        }
        else
        {
            controller.Request.QueryString = QueryString.Create(source, value);
        }

        (await controller.GetWorkloadStatus("acme", MessageId, TestContext.Current.CancellationToken))
            .ShouldBeOfType<ForbidResult>();

        store.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Canonicalizes the explicit route before comparing its binding and reading status.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Fact]
    public async Task MixedCaseRouteUsesCanonicalBindingAndStorageTenant()
    {
        ICommandStatusStore store = Substitute.For<ICommandStatusStore>();
        store.ReadStatusAsync("acme", MessageId, Arg.Any<CancellationToken>()).Returns(
            new CommandStatusRecord(CommandStatus.Completed, DateTimeOffset.UtcNow, "aggregate", 1,
                null, null, null, MessageId, MessageId) { Domain = "tenants" });
        CommandStatusController controller = CreateController(store, "acme");

        CommandStatusResponse response = (await controller.GetWorkloadStatus("AcMe", MessageId,
            TestContext.Current.CancellationToken)).ShouldBeOfType<OkObjectResult>()
            .Value.ShouldBeOfType<CommandStatusResponse>();

        response.TenantId.ShouldBe("acme");
        await store.Received(1).ReadStatusAsync("acme", MessageId, Arg.Any<CancellationToken>());
        store.ReceivedCalls().ShouldHaveSingleItem();
    }

    /// <summary>Keeps the exact canonical resource binding required by workload admission.</summary>
    /// <param name="binding">The nonmatching or noncanonical signed resource binding.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("other")]
    [InlineData("ACME")]
    public async Task IncorrectResourceBindingNeverReadsStatus(string binding)
    {
        ICommandStatusStore store = Substitute.For<ICommandStatusStore>();
        CommandStatusController controller = CreateController(store, binding);

        (await controller.GetWorkloadStatus("acme", MessageId, TestContext.Current.CancellationToken))
            .ShouldBeOfType<ForbidResult>();

        store.ReceivedCalls().ShouldBeEmpty();
    }

    private static CommandStatusController CreateController(ICommandStatusStore store, string binding)
        => new(store, NullLogger<CommandStatusController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "caller"),
                        new Claim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, binding),
                        new Claim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType, "tenants"),
                    ], "workload")),
                },
            },
        };
}
