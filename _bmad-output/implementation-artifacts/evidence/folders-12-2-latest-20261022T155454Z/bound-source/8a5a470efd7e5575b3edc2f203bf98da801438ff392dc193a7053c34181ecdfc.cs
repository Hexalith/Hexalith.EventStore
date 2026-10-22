using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Processes;

namespace Hexalith.EventStore.DomainService;

/// <summary>Canonical, disjoint keys for process control and tenant state.</summary>
public static class ProcessStateKeys
{
    /// <summary>Gets the durable recovery sweep key.</summary>
    public static string Sweep => "eventstore:process:v1:control:sweep";

    /// <summary>Gets a control-namespace tenant registry page key.</summary>
    public static string Tenants(int page) => $"eventstore:process:v1:control:tenants:{page}";

    /// <summary>Gets the repairable control tenant-page tail hint.</summary>
    public static string TenantsTail => "eventstore:process:v1:control:tenants:tail";

    /// <summary>Gets a tenant's candidate page key.</summary>
    public static string Candidates(string tenant, int page) => $"eventstore:process:v1:tenant:{Encode(tenant)}:candidates:{page}";

    /// <summary>Gets the repairable candidate-page tail hint for a tenant.</summary>
    public static string CandidatesTail(string tenant) => $"eventstore:process:v1:tenant:{Encode(tenant)}:candidates:tail";

    /// <summary>Gets one source's checkpoint key.</summary>
    public static string Source(ProcessSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return $"eventstore:process:v1:tenant:{Encode(source.Tenant)}:source:{SourceId(source)}";
    }

    /// <summary>Gets one exact durable publication marker.</summary>
    public static string CandidateMarker(ProcessSource source) => Source(source) + ":candidate";

    /// <summary>Gets one exact durable tenant-publication marker.</summary>
    public static string TenantMarker(string tenant) => $"eventstore:process:v1:tenant:{Encode(tenant)}:published";

    /// <summary>Gets the stable source identifier.</summary>
    public static string SourceId(ProcessSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Validate();
        byte[] bytes = Encoding.UTF8.GetBytes(source.Tenant + "\0" + source.Domain + "\0" + source.Aggregate);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
