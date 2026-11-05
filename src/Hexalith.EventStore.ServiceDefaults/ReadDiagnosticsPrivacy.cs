using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.ServiceDefaults;

/// <summary>Classifies the shared read endpoints whose execution details can reveal protected resource existence.</summary>
public static class ReadDiagnosticsPrivacy
{
    private static readonly object _protectedRequestKey = new();

    /// <summary>Retains protection through path rewriting until the request's own Items are disposed.</summary>
    /// <param name="context">The actual protected request.</param>
    internal static void MarkProtectedRequest(HttpContext context) => context.Items[_protectedRequestKey] = true;

    /// <summary>Tests either the original protected classification or the request's sticky local marker.</summary>
    /// <param name="context">The actual incoming or re-executing request.</param>
    /// <returns>Whether detailed diagnostics remain protected for this request.</returns>
    internal static bool IsProtectedExecution(HttpContext context)
        => context.Items.ContainsKey(_protectedRequestKey) || IsProtectedRequest(context);

    /// <summary>Tests the finite SDK read surface without inspecting caller credentials or request bodies.</summary>
    /// <param name="context">The actual incoming HTTP request.</param>
    /// <returns>Whether detailed request and subrequest diagnostics must be omitted.</returns>
    public static bool IsProtectedRequest(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string path = (context.Request.Path.Value ?? string.Empty).TrimEnd('/');
        if (HttpMethods.IsPost(context.Request.Method)
            && (IsPublicQuery(context) || path.Equals("/query", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/v1/queries/owner-authority/workload", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/v1/streams/read/workload", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return (HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPost(context.Request.Method))
            && path.StartsWith("/actors/", StringComparison.OrdinalIgnoreCase)
            && (path.EndsWith("/method/GetStreamMetadataAsync", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/method/InspectSourceProofAsync", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/method/InspectSourceMetadataPairAsync", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Identifies the public boundary that receives one uniform completion record.</summary>
    /// <param name="context">The actual incoming HTTP request.</param>
    /// <returns>Whether the request is a public query submission.</returns>
    internal static bool IsPublicQuery(HttpContext context)
        => HttpMethods.IsPost(context.Request.Method)
            && (context.Request.Path.Value ?? string.Empty).TrimEnd('/').Equals("/api/v1/queries", StringComparison.OrdinalIgnoreCase);
}
