namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Immutable installed protected-operation catalogue, independent of admission-provider availability.</summary>
public static class IdentityOperationCatalog
{
    /// <summary>Recognizes the installed authoritative identity contracts before any protected lookup.</summary>
    public static bool RequiresAdmission(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        string name = operation.Split(',', 2)[0].Trim().Trim('"', '\'');
        string shortName = name[(name.LastIndexOf('.') + 1)..];
        return shortName is "ProvisionAgentParty" or "EstablishHumanActorBinding" or "RebindHumanActorBinding"
            or "RevokeHumanActorBinding" or "ResolvePartyIdentity" or "ResolveHumanActorBindingAt";
    }
}
