using System.Security.Claims;

using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.AspNetCore.Http;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class HttpContextAdminAuthContextTests
{
    // === GetToken ===

    [Fact]
    public void GetToken_ReturnsBearerToken_WhenAuthorizationHeaderPresent()
    {
        IHttpContextAccessor accessor = CreateAccessorWithAuthHeader("Bearer eyJhbGciOiJSUzI1NiJ9.test");

        var sut = new HttpContextAdminAuthContext(accessor);

        sut.GetToken().ShouldBe("eyJhbGciOiJSUzI1NiJ9.test");
    }

    [Fact]
    public void GetToken_ReturnsNull_WhenNoAuthorizationHeader()
    {
        IHttpContextAccessor accessor = CreateAccessorWithAuthHeader(null);

        var sut = new HttpContextAdminAuthContext(accessor);

        sut.GetToken().ShouldBeNull();
    }

    [Fact]
    public void GetToken_ReturnsNull_WhenAuthorizationHeaderIsEmpty()
    {
        IHttpContextAccessor accessor = CreateAccessorWithAuthHeader("");

        var sut = new HttpContextAdminAuthContext(accessor);

        sut.GetToken().ShouldBeNull();
    }

    [Fact]
    public void GetToken_ReturnsNull_WhenNotBearerScheme()
    {
        IHttpContextAccessor accessor = CreateAccessorWithAuthHeader("Basic dXNlcjpwYXNz");

        var sut = new HttpContextAdminAuthContext(accessor);

        sut.GetToken().ShouldBeNull();
    }

    [Fact]
    public void GetToken_ReturnsNull_WhenBearerTokenIsBlank()
    {
        IHttpContextAccessor accessor = CreateAccessorWithAuthHeader("Bearer   ");

        var sut = new HttpContextAdminAuthContext(accessor);

        sut.GetToken().ShouldBeNull();
    }

    [Fact]
    public void GetToken_IsCaseInsensitive_ForBearerPrefix()
    {
        IHttpContextAccessor accessor = CreateAccessorWithAuthHeader("bearer my-token-123");

        var sut = new HttpContextAdminAuthContext(accessor);

        sut.GetToken().ShouldBe("my-token-123");
    }

    [Fact]
    public void GetToken_TrimsWhitespace_FromToken()
    {
        IHttpContextAccessor accessor = CreateAccessorWithAuthHeader("Bearer  my-token  ");

        var sut = new HttpContextAdminAuthContext(accessor);

        sut.GetToken().ShouldBe("my-token");
    }

    [Fact]
    public void GetToken_ReturnsNull_WhenHttpContextIsNull()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        var sut = new HttpContextAdminAuthContext(accessor);

        sut.GetToken().ShouldBeNull();
    }

    // === GetUserId ===

    [Fact]
    public void GetUserId_ReturnsSubClaim_WhenPresent()
    {
        IHttpContextAccessor accessor = CreateAccessorWithClaims(new Claim("sub", "user-abc-123"));

        var sut = new HttpContextAdminAuthContext(accessor);

        sut.GetUserId().ShouldBe("user-abc-123");
    }

    [Fact]
    public void GetUserId_FallsBackToNameIdentifier_WhenNoSubClaim()
    {
        IHttpContextAccessor accessor = CreateAccessorWithClaims(
            new Claim(ClaimTypes.NameIdentifier, "user-fallback-456"));

        var sut = new HttpContextAdminAuthContext(accessor);

        sut.GetUserId().ShouldBe("user-fallback-456");
    }

    [Fact]
    public void GetUserId_PrefersSubClaim_OverNameIdentifier()
    {
        IHttpContextAccessor accessor = CreateAccessorWithClaims(
            new Claim("sub", "sub-value"),
            new Claim(ClaimTypes.NameIdentifier, "nameid-value"));

        var sut = new HttpContextAdminAuthContext(accessor);

        sut.GetUserId().ShouldBe("sub-value");
    }

    [Fact]
    public void GetUserId_ReturnsNull_WhenNoClaims()
    {
        IHttpContextAccessor accessor = CreateAccessorWithClaims();

        var sut = new HttpContextAdminAuthContext(accessor);

        sut.GetUserId().ShouldBeNull();
    }

    [Fact]
    public void GetUserId_ReturnsNull_WhenHttpContextIsNull()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        var sut = new HttpContextAdminAuthContext(accessor);

        sut.GetUserId().ShouldBeNull();
    }

    // === Helpers ===

    private static IHttpContextAccessor CreateAccessorWithAuthHeader(string? authorizationValue)
    {
        DefaultHttpContext httpContext = new();
        if (authorizationValue is not null)
        {
            httpContext.Request.Headers.Authorization = authorizationValue;
        }

        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return accessor;
    }

    private static IHttpContextAccessor CreateAccessorWithClaims(params Claim[] claims)
    {
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")),
        };

        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return accessor;
    }
}
