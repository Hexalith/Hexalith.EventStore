using System.Security.Claims;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public partial class QueriesControllerTests
{
    [Theory]
    [InlineData("roles")]
    [InlineData("permissions")]
    [InlineData("length")]
    public async Task BoundedAuthorityFailureIsSafeRefusalBeforeQueryDispatch(string kind)
    {
        IMediator mediator = Substitute.For<IMediator>();
        var claims = new List<Claim> { new("sub", "actor") };
        if (kind == "length") claims.Add(new("eventstore:permission", new string('x', 513)));
        else claims.AddRange(Enumerable.Range(0, 65).Select(index => new Claim(kind == "roles" ? ClaimTypes.Role : "eventstore:permission", "value-" + index)));
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Authentication:QueryAuthority:RequiredQueries:orders:0"] = "GetOrderStatus" }).Build();
        var controller = CreateController(mediator, principal: new ClaimsPrincipal(new ClaimsIdentity(claims, "validated")), configuration: config);
        (await controller.Submit(CreateTestRequest(), null, TestContext.Current.CancellationToken)).ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(403);
        await mediator.DidNotReceive().Send(Arg.Any<SubmitQuery>(), Arg.Any<CancellationToken>());
    }
}
