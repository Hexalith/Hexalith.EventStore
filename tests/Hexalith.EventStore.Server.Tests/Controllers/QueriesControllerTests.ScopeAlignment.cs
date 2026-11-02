using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Controllers;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public partial class QueriesControllerTests
{
    [Theory]
    [InlineData("get-tenant")]
    [InlineData("get-tenant-users")]
    [InlineData("get-tenant-audit")]
    public async Task SingleTenantReadRejectsDifferentScopeAndAggregateBeforeAuthorization(string queryType)
    {
        IMediator mediator = Substitute.For<IMediator>();
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        QueriesController controller = CreateController(mediator, tenantValidator: tenantValidator, rbacValidator: rbacValidator);
        var request = new SubmitQueryRequest("tenant-a", "tenants", "tenant-b", queryType);

        IActionResult result = await controller.Submit(request, null, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<BadRequestObjectResult>();
        mediator.ReceivedCalls().ShouldBeEmpty();
        tenantValidator.ReceivedCalls().ShouldBeEmpty();
        rbacValidator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task SingleTenantReadCanonicalizesMatchingScopeAndAggregateBeforeAuthorization()
    {
        IMediator mediator = Substitute.For<IMediator>();
        ITenantValidator tenantValidator = Substitute.For<ITenantValidator>();
        tenantValidator.ValidateAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);
        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        rbacValidator.ValidateAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(RbacValidationResult.Allowed);
        QueriesController controller = CreateController(mediator, tenantValidator: tenantValidator, rbacValidator: rbacValidator);
        var request = new SubmitQueryRequest("Tenant-A", "tenants", "TENANT-A", "get-tenant");

        IActionResult result = await controller.Submit(request, null, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(503);
        await tenantValidator.Received(1).ValidateAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
            "tenant-a", Arg.Any<CancellationToken>(), "tenant-a");
        mediator.ReceivedCalls().ShouldBeEmpty();
    }
}
