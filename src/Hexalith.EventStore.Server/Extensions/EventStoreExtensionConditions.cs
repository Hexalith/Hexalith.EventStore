namespace Hexalith.EventStore.Server.Extensions;

/// <summary>Stable named conditions for extension composition refusals.</summary>
public static class EventStoreExtensionConditions
{
    /// <summary>A configured extension entry is malformed.</summary>
    public const string InvalidDeclaration = "eventstore-extension-declaration-invalid";

    /// <summary>The same extension identity or package is declared twice.</summary>
    public const string Duplicate = "eventstore-extension-duplicate";

    /// <summary>A declared extension-API major is outside the gateway's supported window.</summary>
    public const string UnsupportedMajor = "eventstore-extension-unsupported-major";

    /// <summary>A declared extension package cannot be loaded into the composed gateway.</summary>
    public const string Unavailable = "eventstore-extension-unavailable";

    /// <summary>A loaded package carries no extension identity attribute.</summary>
    public const string AttributeMissing = "eventstore-extension-attribute-missing";

    /// <summary>A loaded package's identity attribute differs from its declaration.</summary>
    public const string DeclarationDrift = "eventstore-extension-declaration-drift";

    /// <summary>A package names an adapter source that is unavailable or belongs to another assembly.</summary>
    public const string AdapterSourceUnavailable = "eventstore-extension-adapter-source-unavailable";

    /// <summary>Extensions were registered more than once on one gateway.</summary>
    public const string DuplicateRegistration = "eventstore-extension-duplicate-registration";
}
