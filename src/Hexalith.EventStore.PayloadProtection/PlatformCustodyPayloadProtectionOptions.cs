// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// On-premises authority: 835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180; approved supplement section 2.
using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed logical component selection; provider addresses and credentials remain inside Platform.</summary>
/// <param name="BindingName">The installed lowercase ASCII binding name.</param>
/// <param name="InstallationId">The canonical installed identity.</param>
/// <param name="ProfileSha256">The exact qualified installation/profile digest.</param>
internal sealed record PlatformCustodyPayloadProtectionOptions(string BindingName, string InstallationId, string ProfileSha256)
{
    /// <summary>Loads and validates only the three approved provider-neutral settings.</summary>
    internal static PlatformCustodyPayloadProtectionOptions Load(IConfigurationSection section)
    {
        if (section.GetChildren().Any(c => c.Key is not ("BindingName" or "InstallationId" or "ProfileSha256") || c.GetChildren().Any())) { throw new PayloadProtectionFormatException(); }
        var options = new PlatformCustodyPayloadProtectionOptions(section["BindingName"] ?? "", section["InstallationId"] ?? "", section["ProfileSha256"] ?? "");
        options.Validate();
        return options;
    }

    /// <summary>Rejects missing, noncanonical or private-provider configuration before client resolution.</summary>
    internal void Validate()
    {
        if (BindingName is not { Length: >= 1 and <= 128 } || BindingName.Any(c => c is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            || InstallationId is not { Length: >= 1 and <= 128 } || InstallationId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '_' or '-')))
        { throw new PayloadProtectionFormatException(); }
        _ = PayloadProtectionStateKeys.Sha(ProfileSha256);
    }

    /// <summary>Never formats installed identities or provider settings.</summary>
    public override string ToString() => nameof(PlatformCustodyPayloadProtectionOptions);
}
