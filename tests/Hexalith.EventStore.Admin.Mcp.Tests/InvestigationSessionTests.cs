namespace Hexalith.EventStore.Admin.Mcp.Tests;

public class InvestigationSessionTests
{
    [Fact]
    public void SetContext_SetsTenantIdAndDomain()
    {
        var session = new InvestigationSession();

        session.SetContext("acme-corp", "Orders");

        session.TenantId.ShouldBe("acme-corp");
        session.Domain.ShouldBe("Orders");
    }

    [Fact]
    public void SetContext_WithNullTenantId_PreservesExistingTenantId()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", null);

        session.SetContext(null, "Orders");

        session.TenantId.ShouldBe("acme-corp");
        session.Domain.ShouldBe("Orders");
    }

    [Fact]
    public void SetContext_WithNullDomain_PreservesExistingDomain()
    {
        var session = new InvestigationSession();
        session.SetContext(null, "Orders");

        session.SetContext("acme-corp", null);

        session.TenantId.ShouldBe("acme-corp");
        session.Domain.ShouldBe("Orders");
    }

    [Fact]
    public void SetContext_InitializesStartedAtUtc_OnFirstCall()
    {
        var session = new InvestigationSession();

        session.SetContext("acme-corp", null);

        session.StartedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public void SetContext_PreservesStartedAtUtc_OnSubsequentCalls()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", null);
        DateTimeOffset? firstStarted = session.StartedAtUtc;

        session.SetContext(null, "Orders");

        session.StartedAtUtc.ShouldBe(firstStarted);
    }

    [Fact]
    public void ClearTenantId_ClearsOnlyTenantId_PreservesDomain()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", "Orders");

        session.ClearTenantId();

        session.TenantId.ShouldBeNull();
        session.Domain.ShouldBe("Orders");
    }

    [Fact]
    public void ClearDomain_ClearsOnlyDomain_PreservesTenantId()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", "Orders");

        session.ClearDomain();

        session.TenantId.ShouldBe("acme-corp");
        session.Domain.ShouldBeNull();
    }

    [Fact]
    public void ClearTenantId_ClearsStartedAtUtc_WhenDomainAlsoNull()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", null);

        session.ClearTenantId();

        session.StartedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void ClearTenantId_PreservesStartedAtUtc_WhenDomainStillSet()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", "Orders");

        session.ClearTenantId();

        session.StartedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public void Clear_ResetsAllFieldsToNull()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", "Orders");

        session.Clear();

        session.TenantId.ShouldBeNull();
        session.Domain.ShouldBeNull();
        session.StartedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void HasContext_ReturnsFalse_Initially()
    {
        var session = new InvestigationSession();

        session.HasContext.ShouldBeFalse();
    }

    [Fact]
    public void HasContext_ReturnsTrue_AfterSetContext()
    {
        var session = new InvestigationSession();

        session.SetContext("acme-corp", null);

        session.HasContext.ShouldBeTrue();
    }

    [Fact]
    public void HasContext_ReturnsFalse_AfterClear()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", "Orders");

        session.Clear();

        session.HasContext.ShouldBeFalse();
    }

    [Fact]
    public void ThreadSafety_ConcurrentSetContextAndClear_DoesNotCorruptState()
    {
        var session = new InvestigationSession();

        Parallel.For(0, 100, i =>
        {
            if (i % 3 == 0)
            {
                session.SetContext($"tenant-{i}", $"domain-{i}");
            }
            else if (i % 3 == 1)
            {
                session.Clear();
            }
            else
            {
                _ = session.HasContext;
                _ = session.TenantId;
                _ = session.Domain;
            }
        });

        // After concurrent operations, session should be in a valid state
        // (either has context or doesn't — no corruption)
        bool hasContext = session.HasContext;
        if (!hasContext)
        {
            session.TenantId.ShouldBeNull();
            session.Domain.ShouldBeNull();
        }
    }

    [Fact]
    public void GetSnapshot_ReturnsConsistentContextShape()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", "Orders");

        InvestigationSession.Snapshot snapshot = session.GetSnapshot();

        snapshot.TenantId.ShouldBe("acme-corp");
        snapshot.Domain.ShouldBe("Orders");
        snapshot.StartedAtUtc.ShouldNotBeNull();
        snapshot.HasContext.ShouldBeTrue();
    }

    [Fact]
    public void SetContext_TrimsValuesAndIgnoresWhitespaceOnlyInput()
    {
        var session = new InvestigationSession();

        session.SetContext("  acme-corp  ", "  Orders  ");
        session.SetContext("\t", " ");

        session.TenantId.ShouldBe("acme-corp");
        session.Domain.ShouldBe("Orders");
    }
}
