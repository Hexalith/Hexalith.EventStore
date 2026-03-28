namespace Hexalith.EventStore.Pipeline;

public static class AuthorizationConstants {
    public const string SubmitPermission = "command:submit";
    public const string WildcardPermission = "commands:*";
    public const string ReadPermission = "query:read";
    public const string QueryWildcardPermission = "queries:*";
}
