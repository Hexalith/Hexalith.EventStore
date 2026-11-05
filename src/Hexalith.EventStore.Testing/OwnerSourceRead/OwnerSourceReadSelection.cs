using System.Text.Json;

using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Testing.CatalogInvocation;

namespace Hexalith.EventStore.Testing.OwnerSourceRead;

/// <summary>Finite source admission and result bounds shared by the private owner and caller.</summary>
internal static class OwnerSourceReadSelection
{
    /// <summary>The only supported public source scope.</summary>
    internal const string Scope = "platform:global-administrators";
    /// <summary>The only supported domain and aggregate identity.</summary>
    internal const string Administrator = "global-administrators";
    /// <summary>The only supported helper mode.</summary>
    internal const string Mode = "administrator-bootstrap";
    /// <summary>The maximum complete stream event count.</summary>
    internal const int MaximumEvents = 100;

    /// <summary>Refuses any expanded target, helper mode, or deadline.</summary>
    /// <param name="request">The actual private request.</param>
    internal static void Validate(OwnerSourceReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        TimeSpan remaining = request.ExpiresAt - DateTimeOffset.UtcNow;
        if (request.Mode != Mode || request.Scope != Scope || request.Domain != Administrator
            || request.AggregateId != Administrator || remaining <= TimeSpan.Zero || remaining > TimeSpan.FromSeconds(45))
            throw new InvalidOperationException("Private owner source request is outside the finite selection.");
    }

    /// <summary>Requires the exact complete retained storage stream and bounded serialized result.</summary>
    /// <param name="page">The actual SDK page.</param>
    internal static void ValidatePage(StreamReadPage page)
    {
        if (page is null || page.Tenant != "system" || page.Domain != Administrator || page.AggregateId != Administrator
            || page.Events is null || page.Events.Count > MaximumEvents || page.Metadata is null
            || page.Metadata.FromSequence != 0 || page.Metadata.ToSequence is not null
            || page.Metadata.EventCount != page.Events.Count || page.Metadata.IsTruncated
            || page.Metadata.NextContinuationToken is not null
            || page.Metadata.LatestSequence != page.Events.Count
            || page.Metadata.LastSequenceReturned != (page.Events.Count == 0 ? (long?)null : page.Events.Count)
            || page.Events.Where((item, index) => item is null || item.SequenceNumber != index + 1L).Any()
            || JsonSerializer.SerializeToUtf8Bytes(page, CatalogInvocationFiles.Json).Length > CatalogInvocationFiles.MaximumBytes / 2)
            throw new InvalidOperationException("Private owner source response identity or bounds refused.");
    }
}
