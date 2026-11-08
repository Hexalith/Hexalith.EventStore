using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Tests.Extensions;

/// <summary>Emits in-memory extension assemblies with an exact identity attribute and an owned adapter source.</summary>
internal static class ExtensionProbeAssembly
{
    private static readonly ConcurrentDictionary<string, Assembly> _emitted = new(StringComparer.Ordinal);

    static ExtensionProbeAssembly() =>
        // Attribute blobs name the emitted adapter source by assembly; resolve those names to the in-memory assemblies.
        AppDomain.CurrentDomain.AssemblyResolve += (_, request) => _emitted.GetValueOrDefault(new AssemblyName(request.Name).Name ?? string.Empty);

    /// <summary>Emits one probe package.</summary>
    /// <param name="name">The package and assembly name.</param>
    /// <param name="id">The identity attribute value, or null to omit the attribute.</param>
    /// <param name="major">The identity attribute major.</param>
    /// <param name="adapters">Whether the package names its own adapter source.</param>
    /// <param name="foreignSource">Whether the package names an adapter source from another assembly instead.</param>
    /// <returns>The emitted assembly.</returns>
    internal static Assembly Create(string name, string? id, int major, bool adapters = true, bool foreignSource = false)
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        Assembly loaded = assembly;
        if (id is not null)
        {
            assembly.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(EventStoreExtensionAttribute).GetConstructor([typeof(string), typeof(int)])!, [id, major]));
        }

        if (foreignSource)
        {
            assembly.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(RouteCatalogAdapterSourceAttribute).GetConstructor([typeof(Type)])!, [typeof(ExtensionProbeAdapters)]));
        }
        else if (adapters)
        {
            ModuleBuilder module = assembly.DefineDynamicModule(name);
            TypeBuilder source = module.DefineType(name + ".Adapters", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class);
            MethodBuilder create = source.DefineMethod("Create", MethodAttributes.Public | MethodAttributes.Static,
                typeof(IReadOnlyList<IDeclaredIdempotencyIntentAdapter>), Type.EmptyTypes);
            ILGenerator il = create.GetILGenerator();
            il.Emit(OpCodes.Call, typeof(ExtensionProbeAdapters).GetMethod(nameof(ExtensionProbeAdapters.Create))!);
            il.Emit(OpCodes.Ret);
            Type created = source.CreateType();
            assembly.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(RouteCatalogAdapterSourceAttribute).GetConstructor([typeof(Type)])!, [created]));

            // Return the runtime assembly that owns the emitted types, as a host load would.
            loaded = created.Assembly;
        }

        _emitted[name] = loaded;
        return loaded;
    }
}
