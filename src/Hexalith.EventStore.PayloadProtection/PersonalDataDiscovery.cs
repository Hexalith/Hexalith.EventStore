// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Discovers paths against one exact serialization, with bounded metadata-driven correlation.</summary>
internal sealed class PersonalDataDiscovery(PersonalDataPolicyRegistry policies, PayloadTypeRegistry types, ISensitiveBufferObserver? observer = null)
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

            using (var destination = new PayloadSerializationBuffer(observer, cancellationToken))
            {
                using (var writer = new Utf8JsonWriter(destination, new JsonWriterOptions
                {
                    Encoder = typeInfo.Options.Encoder,
                    Indented = typeInfo.Options.WriteIndented,
                    IndentCharacter = typeInfo.Options.IndentCharacter,
                    IndentSize = typeInfo.Options.IndentSize,
                    NewLine = typeInfo.Options.NewLine,
                    MaxDepth = Math.Min(PayloadProtectionLimits.JsonDepth, typeInfo.Options.MaxDepth == 0 ? PayloadProtectionLimits.JsonDepth : typeInfo.Options.MaxDepth),
                }))
                {
                    JsonSerializer.Serialize(writer, root, typeInfo);
                    writer.Flush();
                }
                serialized = destination.ToArray();
            }
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
            bool hasCanonicalPolicy = policies.Entries.Any(e => e.PathPolicy is not null);
            var ancestors = new HashSet<object>(ReferenceEqualityComparer.Instance);
            int visited = 0;
            foreach (PersonalDataPolicyRegistration registration in policies.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (registration.PathPolicy is { } policy)
                {
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
                    // Explicit conflicting selections remain invalid, even within one canonical callback.
                    _ = ProtectedPathManifestCodec.Create(selected, snapshot: selected.Count == 1 && selected[0].Length == 0,
                        allowEmpty: true, cancellationToken: cancellationToken);
                }
                else
                {
                    visited = 0;
                    Walk(root, typeInfo, json.RootElement, string.Empty, registration);
                }
            }
            _ = ProtectedPathManifestCodec.Create(selected, snapshot: selected.Count == 1 && selected[0].Length == 0,
                allowEmpty: true, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return selected.OrderBy(p => Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(p)), StringComparer.Ordinal).ToArray();

            void Walk(object? owner, JsonTypeInfo metadata, JsonElement value, string pointer, PersonalDataPolicyRegistration registration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++visited > PayloadProtectionLimits.JsonNodes || pointer.Count(c => c == '/') > PayloadProtectionLimits.JsonDepth)
                {
                    throw new PayloadProtectionFormatException();
                }

                if (value.ValueKind == JsonValueKind.Null) { return; }
                if (owner is null) { throw new PayloadProtectionFormatException(); }
                if (selected.Contains(pointer, StringComparer.Ordinal)) { return; }

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
                                RequireCanonicalAuthority();
                                continue;
                            }

                            PropertyInfo? clr = types.Property(correlated);
                            if (clr is null) { RequireCanonicalAuthority(); continue; }
                            int selectionCount = 0;
                            if (registration.PolicyId == PersonalDataPolicyRegistry.AttributePolicyId)
                            {
                                if (clr.IsDefined(typeof(PersonalDataAttribute), inherit: true)) { selectionCount++; }
                            }
                            else if (registration.Policy is { } ordinary)
                            {
                                PersonalDataPolicyDecision decision = ordinary.Evaluate(new(identity, root, json.RootElement,
                                    typeInfo, owner, clr, property.Value, path, payloadType, kind));
                                if (!Enum.IsDefined(decision)) { throw new PayloadProtectionFormatException(); }
                                if (decision == PersonalDataPolicyDecision.Protect) { selectionCount++; }
                            }
                            if (selectionCount > 1) { throw new PayloadProtectionFormatException(); }
                            if (selectionCount == 1)
                            {
                                selected.Add(path);
                                if (selected.Count > 4096) { throw new PayloadProtectionFormatException(); }
                            }
                            else
                            {
                                object? child = correlated.Get is null ? throw new PayloadProtectionFormatException() : correlated.Get(owner);
                                if (child is null) { throw new PayloadProtectionFormatException(); }
                                // A later getter cannot substitute runtime metadata for the serializer's declared shape.
                                // Whole-property selection above needs no descendant CLR provenance.
                                Type declared = Nullable.GetUnderlyingType(correlated.PropertyType) ?? correlated.PropertyType;
                                if (child.GetType() != declared || declared == typeof(object))
                                { RequireCanonicalAuthority(); continue; }
                                JsonTypeInfo declaredMetadata = ResolveDeclaredMetadata(correlated.PropertyType);
                                if (declaredMetadata.PolymorphismOptions is not null) { RequireCanonicalAuthority(); continue; }
                                Walk(child, declaredMetadata, property.Value, path, registration);
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
                                if (child is null && item.ValueKind != JsonValueKind.Null) { throw new PayloadProtectionFormatException(); }
                                if (child is not null)
                                {
                                    Type? element = metadata.Type.IsArray ? metadata.Type.GetElementType() : metadata.Type.IsGenericType && metadata.Type.GetGenericTypeDefinition() == typeof(List<>)
                                        ? metadata.Type.GenericTypeArguments[0] : null;
                                    string path = pointer + "/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                    if (element is null || (Nullable.GetUnderlyingType(element) ?? element) != child.GetType() || element == typeof(object)) { RequireCanonicalAuthority(); }
                                    else
                                    {
                                        JsonTypeInfo elementMetadata = ResolveDeclaredMetadata(element);
                                        if (elementMetadata.PolymorphismOptions is not null) { RequireCanonicalAuthority(); }
                                        else { Walk(child, elementMetadata, item, path, registration); }
                                    }
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

            JsonTypeInfo ResolveDeclaredMetadata(Type declared)
            {
                JsonTypeInfo metadata = types.Resolve(declared);
                Type? underlying = Nullable.GetUnderlyingType(declared);
                // Only the built-in nullable wrapper can be unwrapped; custom conversion still needs canonical authority.
                return underlying is not null && metadata.Converter.GetType().Assembly == typeof(JsonSerializer).Assembly
                    ? types.Resolve(underlying) : metadata;
            }

            void RequireCanonicalAuthority()
            {
                // This contract supplies the complete sensitive-path set for the exact serialized root,
                // including opaque shapes; public siblings need no selection or CLR traversal.
                if (!hasCanonicalPolicy) { throw new PayloadProtectionFormatException(); }
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
