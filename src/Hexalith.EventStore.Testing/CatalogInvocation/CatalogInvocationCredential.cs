namespace Hexalith.EventStore.Testing.CatalogInvocation;

/// <summary>The only credential modes accepted by the private technical proof transport.</summary>
public enum CatalogInvocationCredential
{
    /// <summary>Acquire the proper assertion from the gateway's actual issuer.</summary>
    Workload,
    /// <summary>Send no credential.</summary>
    Anonymous,
    /// <summary>Send the supplied public human bearer credential unchanged.</summary>
    HumanBearer,
    /// <summary>Send the supplied public-issuer workload masquerade unchanged.</summary>
    PublicIssuerWorkload,
    /// <summary>Acquire a genuine metadata assertion for the projection callback.</summary>
    WrongOperation,
}
