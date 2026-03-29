using Hexalith.EventStore.Admin.Server.Services;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class NullAdminAuthContextTests
{
    [Fact]
    public void GetToken_ReturnsNull()
    {
        NullAdminAuthContext context = new();
        context.GetToken().ShouldBeNull();
    }

    [Fact]
    public void GetUserId_ReturnsNull()
    {
        NullAdminAuthContext context = new();
        context.GetUserId().ShouldBeNull();
    }

    [Fact]
    public void ImplementsIAdminAuthContext()
    {
        NullAdminAuthContext context = new();
        context.ShouldBeAssignableTo<IAdminAuthContext>();
    }
}
