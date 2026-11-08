using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

using Hexalith.EventStore.DomainService;

using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.Server.Extensions;

/// <summary>Reads composer declarations and verifies each loaded extension assembly against them.</summary>
internal static partial class EventStoreExtensionLoader
{
    private static readonly string[] _entryKeys = ["PackageId", "ExtensionId", "ApiMajor", "ModuleId"];

    /// <summary>Reads the composer-supplied declarations without loading any assembly.</summary>
    /// <param name="configuration">The gateway configuration.</param>
    /// <returns>The declarations in configuration order; empty when no extension is declared.</returns>
    /// <exception cref="EventStoreExtensionException">An entry is malformed.</exception>
    internal static IReadOnlyList<EventStoreExtensionDeclaration> Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        List<EventStoreExtensionDeclaration> declarations = [];
        foreach (IConfigurationSection entry in configuration.GetSection(EventStoreExtensionServiceCollectionExtensions.ConfigurationSection).GetChildren())
        {
            bool unknown = entry.GetChildren().Any(child => !_entryKeys.Contains(child.Key, StringComparer.OrdinalIgnoreCase));
            string? packageId = entry["PackageId"];
            string? extensionId = entry["ExtensionId"];
            IConfigurationSection module = entry.GetSection("ModuleId");
            string? moduleId = module.Value;
            bool structuredModule = module.GetChildren().Any();
            if (unknown
                || string.IsNullOrWhiteSpace(packageId) || !PackageIdPattern().IsMatch(packageId)
                || string.IsNullOrWhiteSpace(extensionId) || extensionId.Trim() != extensionId
                || structuredModule || (moduleId is not null && (string.IsNullOrWhiteSpace(moduleId) || moduleId.Trim() != moduleId))
                || !int.TryParse(entry["ApiMajor"], NumberStyles.None, CultureInfo.InvariantCulture, out int apiMajor) || apiMajor <= 0)
            {
                string reason = $"Extension entry '{entry.Path}' must supply only an assembly-name PackageId, a nonblank ExtensionId and a positive ApiMajor.";
                if (moduleId is not null || structuredModule)
                    reason = ModulePrefix(moduleId) + $"Extension entry '{entry.Path}' (package '{packageId}', ApiMajor '{entry["ApiMajor"]}') must supply only an assembly-name PackageId, a nonblank ExtensionId, a positive ApiMajor and optional scalar ModuleId metadata that is nonblank and trimmed.";
                throw new EventStoreExtensionException(
                    EventStoreExtensionConditions.InvalidDeclaration,
                    reason);
            }

            declarations.Add(new EventStoreExtensionDeclaration(packageId, extensionId, apiMajor) { ModuleId = moduleId });
        }

        return declarations.AsReadOnly();
    }

    /// <summary>Checks the declared set, then loads and verifies every package before any adapter is registered.</summary>
    /// <param name="declarations">The composer declarations.</param>
    /// <param name="load">Loads one package assembly by name from the composed host.</param>
    /// <returns>The verified extensions and their adapters.</returns>
    /// <exception cref="EventStoreExtensionException">A declaration or loaded assembly is refused.</exception>
    internal static EventStoreExtensionCatalog Load(IReadOnlyList<EventStoreExtensionDeclaration> declarations, Func<string, Assembly> load)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(load);
        Dictionary<string, EventStoreExtensionDeclaration> packages = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, EventStoreExtensionDeclaration> identities = new(StringComparer.Ordinal);
        foreach (EventStoreExtensionDeclaration declaration in declarations)
        {
            ArgumentNullException.ThrowIfNull(declaration);
            if (packages.TryGetValue(declaration.PackageId, out EventStoreExtensionDeclaration? samePackage))
            {
                throw Duplicate("package", declaration.PackageId, samePackage, declaration);
            }

            if (identities.TryGetValue(declaration.ExtensionId, out EventStoreExtensionDeclaration? sameIdentity))
            {
                throw Duplicate("identity", declaration.ExtensionId, sameIdentity, declaration);
            }

            packages.Add(declaration.PackageId, declaration);
            identities.Add(declaration.ExtensionId, declaration);
            if (!EventStoreExtensionApi.IsSupported(declaration.ApiMajor))
            {
                throw new EventStoreExtensionException(
                    EventStoreExtensionConditions.UnsupportedMajor,
                    ModulePrefix(declaration.ModuleId) + $"Extension package '{declaration.PackageId}' ('{declaration.ExtensionId}') declares extension-API major {Major(declaration.ApiMajor)}; this gateway supports majors {SupportedMajors()}.");
            }
        }

        List<EventStoreLoadedExtension> loaded = [];
        List<IDeclaredIdempotencyIntentAdapter> adapters = [];
        foreach (EventStoreExtensionDeclaration declaration in declarations)
        {
            Assembly assembly = LoadAssembly(declaration, load);
            EventStoreExtensionAttribute attribute = assembly.GetCustomAttribute<EventStoreExtensionAttribute>()
                ?? throw new EventStoreExtensionException(
                    EventStoreExtensionConditions.AttributeMissing,
                    ModulePrefix(declaration.ModuleId) + $"Extension package '{declaration.PackageId}' carries no [assembly: EventStoreExtension] identity; declared '{declaration.ExtensionId}' major {Major(declaration.ApiMajor)}.");
            if (!string.Equals(attribute.Id, declaration.ExtensionId, StringComparison.Ordinal) || attribute.Major != declaration.ApiMajor)
            {
                throw new EventStoreExtensionException(
                    EventStoreExtensionConditions.DeclarationDrift,
                    ModulePrefix(declaration.ModuleId) + $"Extension package '{declaration.PackageId}' is declared as '{declaration.ExtensionId}' major {Major(declaration.ApiMajor)}, but its assembly declares '{attribute.Id}' major {Major(attribute.Major)}.");
            }

            int count = 0;
            foreach (IDeclaredIdempotencyIntentAdapter adapter in CreateAdapters(declaration, assembly))
            {
                adapters.Add(adapter);
                count++;
            }

            loaded.Add(new EventStoreLoadedExtension(declaration.PackageId, declaration.ExtensionId, declaration.ApiMajor, count));
        }

        return new EventStoreExtensionCatalog(loaded.AsReadOnly(), adapters.AsReadOnly());
    }

    private static Assembly LoadAssembly(EventStoreExtensionDeclaration declaration, Func<string, Assembly> load)
    {
        Assembly? assembly;
        try
        {
            assembly = load(declaration.PackageId);
        }
        catch (Exception exception) when (exception is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            throw new EventStoreExtensionException(
                EventStoreExtensionConditions.Unavailable,
                ModulePrefix(declaration.ModuleId) + $"Extension package '{declaration.PackageId}' ('{declaration.ExtensionId}') cannot be loaded into the composed gateway; reference it through the composed host bindings.",
                exception);
        }

        return assembly is not null && string.Equals(assembly.GetName().Name, declaration.PackageId, StringComparison.OrdinalIgnoreCase)
            ? assembly
            : throw new EventStoreExtensionException(
                EventStoreExtensionConditions.Unavailable,
                ModulePrefix(declaration.ModuleId) + $"Extension package '{declaration.PackageId}' ('{declaration.ExtensionId}') did not resolve to an assembly of the same name.");
    }

    private static IEnumerable<IDeclaredIdempotencyIntentAdapter> CreateAdapters(EventStoreExtensionDeclaration declaration, Assembly assembly)
    {
        List<IDeclaredIdempotencyIntentAdapter> adapters = [];
        foreach (RouteCatalogAdapterSourceAttribute source in assembly.GetCustomAttributes<RouteCatalogAdapterSourceAttribute>())
        {
            MethodInfo? create = source.SourceType?.Assembly == assembly
                ? source.SourceType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)
                : null;
            object? created;
            try
            {
                created = create?.Invoke(null, null);
            }
            catch (TargetInvocationException exception)
            {
                throw SourceUnavailable(declaration, source, exception.InnerException ?? exception);
            }

            if (created is not IEnumerable<IDeclaredIdempotencyIntentAdapter> declared)
            {
                throw SourceUnavailable(declaration, source, null);
            }

            foreach (IDeclaredIdempotencyIntentAdapter? adapter in declared)
            {
                adapters.Add(adapter ?? throw SourceUnavailable(declaration, source, null));
            }
        }

        return adapters;
    }

    private static EventStoreExtensionException SourceUnavailable(EventStoreExtensionDeclaration declaration, RouteCatalogAdapterSourceAttribute source, Exception? innerException)
        => new(
            EventStoreExtensionConditions.AdapterSourceUnavailable,
            ModulePrefix(declaration.ModuleId) + $"Extension package '{declaration.PackageId}' names adapter source '{source.SourceType?.FullName}', which is outside the package or returns no complete adapter inventory.",
            innerException);

    private static EventStoreExtensionException Duplicate(string kind, string value, EventStoreExtensionDeclaration first, EventStoreExtensionDeclaration second)
        => new(
            EventStoreExtensionConditions.Duplicate,
            $"Extension {kind} '{value}' is declared twice, by package '{first.PackageId}' ('{first.ExtensionId}'){ModuleSuffix(first.ModuleId)} and package '{second.PackageId}' ('{second.ExtensionId}'){ModuleSuffix(second.ModuleId)}.");

    private static string ModulePrefix(string? moduleId) => moduleId is null ? string.Empty : $"Module '{moduleId}': ";

    private static string ModuleSuffix(string? moduleId) => moduleId is null ? string.Empty : $" from module '{moduleId}'";

    private static string Major(int major) => major.ToString(CultureInfo.InvariantCulture);

    private static string SupportedMajors() => string.Join(", ", EventStoreExtensionApi.SupportedMajors.Select(Major));

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(?:[.-][A-Za-z0-9_]+)*\z", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex PackageIdPattern();
}
