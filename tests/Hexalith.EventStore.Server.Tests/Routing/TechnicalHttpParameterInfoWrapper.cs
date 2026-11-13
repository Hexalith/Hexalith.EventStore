using System.Reflection;

using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Copies runtime provenance while optionally substituting the binding attributes consumed during qualification.</summary>
/// <param name="parameter">The actual runtime parameter whose identity is copied.</param>
/// <param name="queryBinding">Whether to substitute a query source for the declared body source.</param>
internal sealed class TechnicalHttpParameterInfoWrapper(ParameterInfo parameter, bool queryBinding) : ParameterInfo
{
    /// <inheritdoc/>
    public override MemberInfo Member => parameter.Member;

    /// <inheritdoc/>
    public override int MetadataToken => parameter.MetadataToken;

    /// <inheritdoc/>
    public override int Position => parameter.Position;

    /// <inheritdoc/>
    public override string? Name => parameter.Name;

    /// <inheritdoc/>
    public override Type ParameterType => parameter.ParameterType;

    /// <inheritdoc/>
    public override ParameterAttributes Attributes => parameter.Attributes;

    /// <inheritdoc/>
    public override object[] GetCustomAttributes(bool inherit) => queryBinding ? [new FromQueryAttribute()] : parameter.GetCustomAttributes(inherit);

    /// <inheritdoc/>
    public override object[] GetCustomAttributes(Type attributeType, bool inherit) => GetCustomAttributes(inherit).Where(attributeType.IsInstanceOfType).ToArray();

    /// <inheritdoc/>
    public override bool IsDefined(Type attributeType, bool inherit) => GetCustomAttributes(attributeType, inherit).Length != 0;
}
