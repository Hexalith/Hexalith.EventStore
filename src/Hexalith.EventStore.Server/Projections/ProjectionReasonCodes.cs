namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Stable bounded reason codes for projection delivery diagnostics. Both production code
/// and tests must reference these symbols rather than string literals so that a future
/// rename keeps both halves consistent.
/// </summary>
internal static class ProjectionReasonCodes
{
    public const string ProjectUpstream4xx = "project_upstream_4xx";
    public const string ProjectUpstream5xx = "project_upstream_5xx";
    public const string ProjectUnexpectedStatus = "project_unexpected_status";
    public const string ProjectUnsupportedContentType = "project_unsupported_content_type";
    public const string ProjectInvalidCharset = "project_invalid_charset";
    public const string ProjectMalformedJson = "project_malformed_json";
    public const string ProjectInvalidProjectionType = "project_invalid_projection_type";
    public const string ProjectInvalidState = "project_invalid_state";
    public const string ProjectTimeout = "project_timeout";
    public const string CheckpointDrift = "checkpoint_drift";
    public const string Unknown = "unknown";
}
