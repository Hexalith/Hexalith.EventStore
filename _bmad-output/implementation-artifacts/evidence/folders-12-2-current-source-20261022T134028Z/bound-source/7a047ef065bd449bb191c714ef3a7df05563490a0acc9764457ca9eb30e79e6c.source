using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Measures unique objects retained by explicit owning proof roots using the running CoreCLR's actual object sizes.</summary>
internal static class ProjectionRetainedGraphSize
{
    private static readonly Func<object, nuint> RawSize = typeof(RuntimeHelpers)
        .GetMethod("GetRawObjectDataSize", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Func<object, nuint>>();

    internal static long Measure(params object?[] roots)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>(roots.OfType<object>());
        long bytes = 0;
        while (pending.TryPop(out object? value))
        {
            if (!seen.Add(value)) continue;
            // The CoreCLR raw size excludes the sync-block and method-table words;
            // allocation size includes those words and native-pointer alignment.
            bytes += checked(((long)RawSize(value) + 2L * IntPtr.Size + IntPtr.Size - 1) & -IntPtr.Size);
            if (value is string) continue;
            if (value is Array array)
            {
                Type element = array.GetType().GetElementType()!;
                if (!element.IsValueType)
                {
                    foreach (object? item in array) if (item is not null) pending.Push(item);
                }
                else if (!element.IsPrimitive && !element.IsEnum)
                {
                    foreach (object item in array) AddReferences(item, element, pending);
                }
            }
            else AddReferences(value, value.GetType(), pending);
        }
        return bytes;
    }

    private static void AddReferences(object value, Type type, Stack<object> pending)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
            foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                Type fieldType = field.FieldType;
                if (fieldType.IsPointer || fieldType.IsPrimitive || fieldType.IsEnum) continue;
                object? fieldValue = field.GetValue(value);
                if (fieldValue is null) continue;
                if (fieldType.IsValueType) AddReferences(fieldValue, fieldType, pending);
                else pending.Push(fieldValue);
            }
    }
}
