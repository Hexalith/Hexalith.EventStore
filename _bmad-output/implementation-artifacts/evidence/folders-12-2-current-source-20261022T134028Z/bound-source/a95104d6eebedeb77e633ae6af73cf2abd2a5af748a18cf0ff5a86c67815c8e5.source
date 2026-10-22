using System.Collections.Immutable;

namespace Hexalith.EventStore.RestApi.Generators;

internal sealed class RestApiMessageDescriptorArrayComparer : IEqualityComparer<ImmutableArray<RestApiMessageDescriptor>>
{
    public static RestApiMessageDescriptorArrayComparer Instance { get; } = new();

    public bool Equals(ImmutableArray<RestApiMessageDescriptor> x, ImmutableArray<RestApiMessageDescriptor> y)
        => x.SequenceEqual(y);

    public int GetHashCode(ImmutableArray<RestApiMessageDescriptor> obj)
    {
        unchecked
        {
            int hash = 17;
            foreach (RestApiMessageDescriptor descriptor in obj)
            {
                hash = (hash * 397) ^ descriptor.GetHashCode();
            }

            return hash;
        }
    }
}
