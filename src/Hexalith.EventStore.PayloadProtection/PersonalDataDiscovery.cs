// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Discovers paths against one exact serialization, with bounded metadata-driven correlation.</summary>
internal sealed class PersonalDataDiscovery(PersonalDataPolicyRegistry policies, PayloadTypeRegistry types)
{
    /// <summary>Serializes once and returns only validated canonical, non-null selected pointers.</summary>
    /// <remarks>The optional supplied JSON must agree with that typed serialization. All owned bytes are cleared.</remarks>
    internal IReadOnlyList<string> Select(AggregateIdentity identity, object root, JsonTypeInfo typeInfo,
        string payloadType, PayloadProtectionPayloadKind kind, byte[]? suppliedJson = null, CancellationToken cancellationToken = default)
    {
        byte[]? serialized = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (root is null || types.Resolve(root.GetType()) != typeInfo || !Enum.IsDefined(kind))
            {
                throw new PayloadProtectionFormatException();
            }

            AadCodec.ValidateContext(new(identity, payloadType, kind, 0), kind);

            serialized = JsonSerializer.SerializeToUtf8Bytes(root, typeInfo);
            using BoundedJsonDocument bounded = BoundedJsonDocument.Parse(serialized, cancellationToken);
            if (bounded.ContainsProtectedMember)
            {
                throw new PayloadProtectionFormatException();
            }

            using JsonDocument json = JsonDocument.Parse(serialized);
            if (suppliedJson is not null)
            {
                using BoundedJsonDocument supplied = BoundedJsonDocument.Parse(suppliedJson, cancellationToken);
                using JsonDocument suppliedDocument = JsonDocument.Parse(suppliedJson);
                if (supplied.ContainsProtectedMember || !JsonElement.DeepEquals(json.RootElement, suppliedDocument.RootElement))
                {
                    throw new PayloadProtectionFormatException();
                }
            }

            var selected = new List<string>();
            bool hasCanonicalPolicy = false;
            foreach (PersonalDataPolicyRegistration registration in policies.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (registration.PathPolicy is not { } policy)
                {
                    continue;
                }

                hasCanonicalPolicy = true;
                IReadOnlyList<string> pointers = policy.SelectCanonicalJsonPointers(identity, json.RootElement, typeInfo, kind)
                    ?? throw new PayloadProtectionFormatException();
                if (pointers.Count > PayloadProtectionLimits.JsonNodes) { throw new PayloadProtectionFormatException(); }
                foreach (string pointer in pointers)
                {
                    if (bounded.Resolve(pointer, allowRoot: kind == PayloadProtectionPayloadKind.Snapshot, cancellationToken: cancellationToken).ValueKind != JsonValueKind.Null)
                    {
                        selected.Add(pointer);
                        if (selected.Count > 4096) { throw new PayloadProtectionFormatException(); }
                    }
                }
            }

            var ancestors = new HashSet<object>(ReferenceEqualityComparer.Instance);
            int visited = 0;
            Walk(root, typeInfo, json.RootElement, string.Empty);
            _ = ProtectedPathManifestCodec.Create(selected, snapshot: selected.Count == 1 && selected[0].Length == 0,
                allowEmpty: true, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return selected.OrderBy(p => Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(p)), StringComparer.Ordinal).ToArray();

            void Walk(object? owner, JsonTypeInfo metadata, JsonElement value, string pointer)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++visited > PayloadProtectionLimits.JsonNodes || pointer.Count(c => c == '/') > PayloadProtectionLimits.JsonDepth)
                {
                    throw new PayloadProtectionFormatException();
                }

                if (owner is null || value.ValueKind == JsonValueKind.Null)
                {
                    return;
                }

                if (metadata.Kind == JsonTypeInfoKind.None)
                {
                    if ((value.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                        || metadata.Converter.GetType().Assembly != typeof(JsonSerializer).Assembly) && !hasCanonicalPolicy)
                    {
                        throw new PayloadProtectionFormatException();
                    }

                    return;
                }

                bool tracked = !owner.GetType().IsValueType && owner is not string;
                if (tracked && !ancestors.Add(owner))
                {
                    throw new PayloadProtectionFormatException();
                }

                try
                {
                    if (metadata.Kind == JsonTypeInfoKind.Object)
                    {
                        foreach (JsonProperty property in value.EnumerateObject())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            JsonPropertyInfo correlated = metadata.Properties.SingleOrDefault(p => p.Name == property.Name)
                                ?? throw new PayloadProtectionFormatException();
                            string path = pointer + "/" + property.Name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
                            if (property.Value.ValueKind == JsonValueKind.Null)
                            {
                                continue;
                            }

                            if (correlated.CustomConverter is not null)
                            {
                                if (!hasCanonicalPolicy)
                                {
                                    throw new PayloadProtectionFormatException();
                                }

                                continue;
                            }

                            PropertyInfo? clr = types.Property(correlated);
                            object? child = correlated.Get is null ? throw new PayloadProtectionFormatException() : correlated.Get(owner);
                            int selectionCount = 0;
                            foreach (PersonalDataPolicyRegistration registration in policies.Entries)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (registration.PolicyId == PersonalDataPolicyRegistry.AttributePolicyId)
                                {
                                    if (clr?.IsDefined(typeof(PersonalDataAttribute), inherit: true) == true) { selectionCount++; }
                                }
                                else if (registration.Policy is { } policy)
                                {
                                    PersonalDataPolicyDecision decision = policy.Evaluate(new(identity, root, json.RootElement,
                                        typeInfo, owner, clr, property.Value, path, payloadType, kind));
                                    if (!Enum.IsDefined(decision))
                                    {
                                        throw new PayloadProtectionFormatException();
                                    }

                                    if (decision == PersonalDataPolicyDecision.Protect) { selectionCount++; }
                                }
                            }

                            if (selectionCount > 1) { throw new PayloadProtectionFormatException(); }
                            if (selectionCount == 1)
                            {
                                selected.Add(path);
                                if (selected.Count > 4096) { throw new PayloadProtectionFormatException(); }
                            }
                            else if (child is not null)
                            {
                                Walk(child, types.Resolve(child.GetType()), property.Value, path);
                            }
                        }
                    }
                    else if (value.ValueKind == JsonValueKind.Array && owner is IEnumerable sequence)
                    {
                        IEnumerator enumerator = sequence.GetEnumerator();
                        try
                        {
                            int index = 0;
                            foreach (JsonElement item in value.EnumerateArray())
                            {
                                if (!enumerator.MoveNext())
                                {
                                    throw new PayloadProtectionFormatException();
                                }

                                object? child = enumerator.Current;
                                if (child is not null)
                                {
                                    Walk(child, types.Resolve(child.GetType()), item, pointer + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                }

                                index++;
                            }

                            if (enumerator.MoveNext())
                            {
                                throw new PayloadProtectionFormatException();
                            }
                        }
                        finally
                        {
                            (enumerator as IDisposable)?.Dispose();
                        }
                    }
                    else
                    {
                        throw new PayloadProtectionFormatException();
                    }
                }
                finally
                {
                    if (tracked)
                    {
                        ancestors.Remove(owner);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new PayloadProtectionFormatException();
        }
        finally
        {
            if (serialized is not null)
            {
                CryptographicOperations.ZeroMemory(serialized);
            }
        }
    }
}
