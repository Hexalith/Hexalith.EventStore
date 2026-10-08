using System.Net;
using System.Reflection;

using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Authorization;

public class AdminAuthorizationMiddlewareResultHandlerTests {
    [Theory]
    [InlineData(true, StatusCodes.Status401Unauthorized)]
    [InlineData(false, StatusCodes.Status403Forbidden)]
    public async Task HandleAsync_AdminFailure_ReplacesSchemeBodyAndUnsafeHeaders(
        bool challenged,
        int expectedStatus) {
        IAuthenticationService authentication = Substitute.For<IAuthenticationService>();
        _ = authentication
            .ChallengeAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>())
            .Returns(async call => await WriteSchemeResponseAsync(call.Arg<HttpContext>()));
        _ = authentication
            .ForbidAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>())
            .Returns(async call => await WriteSchemeResponseAsync(call.Arg<HttpContext>()));
        HttpContext context = CreateContext(authentication, adminEndpoint: true);
        var handler = new AdminAuthorizationMiddlewareResultHandler();
        AuthorizationPolicy policy = new AuthorizationPolicyBuilder("Test").RequireAuthenticatedUser().Build();

        await handler.HandleAsync(
            _ => Task.CompletedTask,
            context,
            policy,
            challenged ? PolicyAuthorizationResult.Challenge() : PolicyAuthorizationResult.Forbid());

        context.Response.StatusCode.ShouldBe(expectedStatus);
        context.Response.ContentType.ShouldBe("application/problem+json");
        context.Response.Headers.WWWAuthenticate.ToString().ShouldBe("Test realm=\"admin\"");
        context.Response.Headers.Location.Count.ShouldBe(0);
        context.Response.Headers["X-Correlation-ID"].ToString().ShouldBe("host-correlation");
        string body = await ReadResponseBodyAsync(context);
        body.ShouldNotContain("protected-scheme-body");
        body.ShouldNotContain("tenant-secret");
        body.ShouldNotContain("scheme-correlation");
        body.ShouldContain("host-correlation");
        body.Length.ShouldBeLessThan(512);
    }

    [Fact]
    public async Task HandleAsync_AdminChallenge_DiscardsLargeSchemeBodyThroughFramework() {
        IAuthenticationService authentication = Substitute.For<IAuthenticationService>();
        _ = authentication.ChallengeAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>())
            .Returns(async call => {
                HttpContext requestContext = call.Arg<HttpContext>();
                requestContext.Response.Body.ShouldBeSameAs(Stream.Null);
                byte[] chunk = new byte[64 * 1024];
                for (int index = 0; index < 1024; index++) {
                    await requestContext.Response.Body.WriteAsync(chunk);
                }
                requestContext.Response.Headers["X-Correlation-ID"] = "scheme-correlation";
                requestContext.Response.Headers["X-Protected"] = "tenant-secret";
            });
        IAuthorizationMiddlewareResultHandler hostHandler = Substitute.For<IAuthorizationMiddlewareResultHandler>();
        HttpContext context = CreateContext(authentication, adminEndpoint: true);
        var handler = new AdminAuthorizationMiddlewareResultHandler(hostHandler);
        AuthorizationPolicy policy = new AuthorizationPolicyBuilder("Test").RequireAuthenticatedUser().Build();

        await handler.HandleAsync(_ => Task.CompletedTask, context, policy, PolicyAuthorizationResult.Challenge());

        await authentication.Received(1).ChallengeAsync(context, "Test", null);
        await hostHandler.DidNotReceiveWithAnyArgs().HandleAsync(default!, default!, default!, default!);
        context.Response.Headers["X-Protected"].Count.ShouldBe(0);
        context.Response.Headers["X-Correlation-ID"].ToString().ShouldBe("host-correlation");
        context.Response.Body.Length.ShouldBeLessThan(512L);
    }

    [Fact]
    public async Task HandleAsync_NonAdminEndpoint_DelegatesFrameworkResponse() {
        IAuthenticationService authentication = Substitute.For<IAuthenticationService>();
        _ = authentication
            .ChallengeAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<AuthenticationProperties?>())
            .Returns(async call => await WriteSchemeResponseAsync(call.Arg<HttpContext>()));
        HttpContext context = CreateContext(authentication, adminEndpoint: false);
        var handler = new AdminAuthorizationMiddlewareResultHandler();
        AuthorizationPolicy policy = new AuthorizationPolicyBuilder("Test").RequireAuthenticatedUser().Build();

        await handler.HandleAsync(
            _ => Task.CompletedTask,
            context,
            policy,
            PolicyAuthorizationResult.Challenge());

        string body = await ReadResponseBodyAsync(context);
        body.ShouldBe("protected-scheme-body tenant-secret");
        context.Response.Headers.Location.ToString().ShouldBe("/tenant-secret");
    }

    private static HttpContext CreateContext(IAuthenticationService authentication, bool adminEndpoint) {
        var services = new ServiceCollection();
        _ = services.AddSingleton(authentication);
        var context = new DefaultHttpContext {
            RequestServices = services.BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();
        context.Items["CorrelationId"] = "host-correlation";
        context.Response.Headers["X-Correlation-ID"] = "host-correlation";
        if (adminEndpoint) {
            var descriptor = new ControllerActionDescriptor {
                ControllerTypeInfo = typeof(AdminStreamsController).GetTypeInfo(),
            };
            context.SetEndpoint(new Endpoint(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(descriptor),
                "Admin test endpoint"));
        }

        return context;
    }

    private static async Task WriteSchemeResponseAsync(HttpContext context) {
        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Test realm=\"admin\"";
        context.Response.Headers.Location = "/tenant-secret";
        context.Response.Headers["X-Correlation-ID"] = "scheme-correlation";
        await context.Response.WriteAsync("protected-scheme-body tenant-secret").ConfigureAwait(false);
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext context) {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }
}
