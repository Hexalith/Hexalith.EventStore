namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Identifies one of three bounded, unauthenticated authority traffic buckets before form authentication.</summary>
public sealed class WorkloadAuthorityEndpointMetadata
{
    private WorkloadAuthorityEndpointMetadata(string partition) => Partition = partition;
    /// <summary>Gets the fixed authority token-request bucket; this does not attest client identity.</summary>
    public static WorkloadAuthorityEndpointMetadata Token { get; } = new("__workload-authority-token");
    /// <summary>Gets the fixed public discovery bucket.</summary>
    public static WorkloadAuthorityEndpointMetadata Discovery { get; } = new("__workload-authority-discovery");
    /// <summary>Gets the fixed public verification-key bucket.</summary>
    public static WorkloadAuthorityEndpointMetadata Keys { get; } = new("__workload-authority-keys");
    /// <summary>Gets the bounded partition key, independent of caller claims, form fields and request paths.</summary>
    public string Partition { get; }
}
