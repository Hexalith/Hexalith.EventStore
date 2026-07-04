using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Hexalith.EventStore.RestApi.Generators;

internal static class RestApiControllerEmitter
{
    private static readonly string[] ReservedActionIdentifiers =
    [
        "__hexalithAggregateId",
        "__hexalithEntityId",
        "__hexalithPayload",
        "__hexalithPayloadValues",
        "__hexalithRequest",
        "__hexalithResponse",
        "__hexalithResult",
        "__hexalithTenant",
        "__hexalithTenantResult",
        "body",
        "cancellationToken",
        "ex",
        "gateway",
        "ifNoneMatch",
    ];

    public static RestApiGeneratedSource? Emit(
        RestApiOptions options,
        string controllerNamespace,
        ImmutableArray<RestApiMessageDescriptor> messages,
        Action<Diagnostic> reportDiagnostic)
    {
        ImmutableArray<RestApiMessageDescriptor> orderedMessages = messages
            .Where(static message => message.IsCommand || message.IsQuery)
            .OrderBy(static message => message.TypeName, StringComparer.Ordinal)
            .ToImmutableArray();
        if (orderedMessages.Length == 0)
        {
            return null;
        }

        var emittedMessages = ImmutableArray.CreateBuilder<RestApiMessageDescriptor>();
        var actionMethodNames = new HashSet<string>(StringComparer.Ordinal);
        var actionRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (RestApiMessageDescriptor message in orderedMessages)
        {
            RestApiRouteDescriptor route = GetRoute(message);
            if (message.UnsupportedReason is not null)
            {
                reportDiagnostic(RestApiDiagnosticDescriptors.CreateUnsupportedContractShape(message));
                continue;
            }

            if (TryFindRouteTemplateError(options, route, out string invalidRouteTemplate, out string routeTemplateError))
            {
                reportDiagnostic(RestApiDiagnosticDescriptors.CreateInvalidRouteTemplate(message, invalidRouteTemplate, routeTemplateError));
                continue;
            }

            if (!IsSupportedRestVerb(route.Verb))
            {
                reportDiagnostic(RestApiDiagnosticDescriptors.CreateUnsupportedRestVerb(message, route.Verb));
                continue;
            }

            ImmutableArray<RestApiRouteParameterDescriptor> effectiveRouteParameters = GetEffectiveRouteParameters(options, route);
            if (RequiresRouteTenant(options) && !HasTenantRouteParameter(effectiveRouteParameters))
            {
                reportDiagnostic(RestApiDiagnosticDescriptors.CreateMissingRouteTenant(message));
                continue;
            }

            if (TryFindDuplicateParameterIdentifier(message, effectiveRouteParameters, out string? duplicateRouteParameter))
            {
                reportDiagnostic(RestApiDiagnosticDescriptors.CreateDuplicateParameter(message, duplicateRouteParameter));
                continue;
            }

            if (message.IsCommand
                && TryFindUnmappedCommandRouteParameter(message, effectiveRouteParameters, out RestApiRouteParameterDescriptor unmappedParameter))
            {
                reportDiagnostic(RestApiDiagnosticDescriptors.CreateUnmappedRouteParameter(message, unmappedParameter));
                continue;
            }

            if (message.IsQuery
                && TryFindUnsupportedQueryParameter(message, effectiveRouteParameters, out RestApiBindablePropertyDescriptor unsupportedProperty))
            {
                reportDiagnostic(RestApiDiagnosticDescriptors.CreateUnsupportedQueryParameter(message, unsupportedProperty));
                continue;
            }

            if (message.IsQuery
                && TryFindDuplicateJsonName(message, out string duplicateJsonName))
            {
                reportDiagnostic(RestApiDiagnosticDescriptors.CreateDuplicateJsonName(message, duplicateJsonName));
                continue;
            }

            if (message.IsQuery
                && TryFindUnmappedQueryBindingRouteParameter(message, effectiveRouteParameters, out string queryBindingRouteParameter))
            {
                reportDiagnostic(RestApiDiagnosticDescriptors.CreateUnmappedQueryBindingRouteParameter(message, queryBindingRouteParameter));
                continue;
            }

            if (message.IsQuery && !message.QueryBinding.HasValue && IsAmbiguousQueryRoute(effectiveRouteParameters))
            {
                reportDiagnostic(RestApiDiagnosticDescriptors.CreateAmbiguousQueryRoute(message));
                continue;
            }

            ImmutableArray<string> methodNames = GetActionMethodNames(message);
            if (TryFindDuplicateActionMethodName(methodNames, actionMethodNames, out string duplicateMethodName))
            {
                reportDiagnostic(RestApiDiagnosticDescriptors.CreateDuplicateParameter(message, duplicateMethodName));
                continue;
            }

            ImmutableArray<string> routeKeys = GetActionRouteKeys(options, message, route);
            if (TryFindDuplicateActionRoute(routeKeys, actionRoutes, out string duplicateActionRoute))
            {
                reportDiagnostic(RestApiDiagnosticDescriptors.CreateDuplicateRoute(message, duplicateActionRoute));
                continue;
            }

            foreach (string methodName in methodNames)
            {
                _ = actionMethodNames.Add(methodName);
            }

            foreach (string routeKey in routeKeys)
            {
                _ = actionRoutes.Add(routeKey);
            }

            emittedMessages.Add(message);
        }

        if (emittedMessages.Count == 0)
        {
            return null;
        }

        string controllerName = GetControllerName(options);
        string hintScope = RestApiNameSanitizer.ToHintPart(controllerNamespace + "." + controllerName, "RestApi");
        string hintName = "Hexalith.EventStore.RestApi." + hintScope + ".Controller.g.cs";

        var builder = new StringBuilder();
        AppendHeader(builder, controllerNamespace);
        AppendControllerStart(builder, options, controllerName);
        foreach (RestApiMessageDescriptor message in emittedMessages)
        {
            if (message.IsCommand)
            {
                AppendCommandAction(builder, options, message);
            }

            if (message.IsQuery)
            {
                AppendQueryAction(builder, options, message);
            }
        }

        AppendHelpers(builder, options);
        builder.AppendLine("}");

        return new RestApiGeneratedSource(hintName, builder.ToString());
    }

    private static void AppendHeader(StringBuilder builder, string controllerNamespace)
    {
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using System.Globalization;");
        builder.AppendLine("using System.Linq;");
        builder.AppendLine("using System.Text.Json;");
        builder.AppendLine("using System.Text.Json.Serialization;");
        builder.AppendLine("using System.Threading;");
        builder.AppendLine("using System.Threading.Tasks;");
        builder.AppendLine();
        builder.AppendLine("using Hexalith.Commons.UniqueIds;");
        builder.AppendLine("using Hexalith.EventStore.Client.Gateway;");
        builder.AppendLine("using Hexalith.EventStore.Contracts.Commands;");
        builder.AppendLine("using Hexalith.EventStore.Contracts.Queries;");
        builder.AppendLine();
        builder.AppendLine("using Microsoft.AspNetCore.Authorization;");
        builder.AppendLine("using Microsoft.AspNetCore.Http;");
        builder.AppendLine("using Microsoft.AspNetCore.Mvc;");
        builder.AppendLine();
        builder.Append("namespace ").Append(controllerNamespace).AppendLine(";");
        builder.AppendLine();
    }

    private static void AppendControllerStart(StringBuilder builder, RestApiOptions options, string controllerName)
    {
        builder.AppendLine("[ApiController]");
        builder.AppendLine("[Authorize]");
        builder.Append("[Route(").Append(Literal(options.RoutePrefix)).AppendLine(")]");
        builder.Append("[Tags(").Append(Literal(GetTag(options))).AppendLine(")]");
        builder.Append("public sealed partial class ").Append(controllerName)
            .AppendLine("(IEventStoreGatewayClient gateway) : ControllerBase");
        builder.AppendLine("{");
        builder.AppendLine("    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)");
        builder.AppendLine("    {");
        builder.AppendLine("        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,");
        builder.AppendLine("        Converters = { new JsonStringEnumConverter() },");
        builder.AppendLine("    };");
    }

    private static void AppendCommandAction(StringBuilder builder, RestApiOptions options, RestApiMessageDescriptor message)
    {
        RestApiRouteDescriptor route = GetRoute(message);
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters = GetEffectiveRouteParameters(options, route);
        string methodName = GetCommandMethodName(message);

        builder.AppendLine();
        AppendHttpAttribute(builder, route);
        builder.AppendLine("    [Consumes(\"application/json\")]");
        builder.AppendLine("    [ProducesResponseType(typeof(SubmitCommandResponse), StatusCodes.Status202Accepted)]");
        builder.Append("    public async Task<IActionResult> ").Append(methodName).AppendLine("(");
        AppendRouteParameters(builder, message, routeParameters, trailingComma: true);
        builder.Append("        [FromBody] ").Append(message.FullyQualifiedTypeName).AppendLine("? body,");
        builder.AppendLine("        CancellationToken cancellationToken)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (body is null)");
        builder.AppendLine("        {");
        builder.AppendLine("            return CreateProblem(StatusCodes.Status400BadRequest, \"Bad Request\", \"Request body is required.\");");
        builder.AppendLine("        }");
        AppendTenantResolution(builder, options, routeParameters);
        AppendCommandRouteMismatchChecks(builder, message, routeParameters, options);
        builder.AppendLine();
        builder.AppendLine("        var __hexalithRequest = new SubmitCommandRequest(");
        builder.AppendLine("            UniqueIdHelper.GenerateSortableUniqueStringId(),");
        builder.AppendLine("            __hexalithTenant,");
        builder.Append("            ").Append(message.FullyQualifiedTypeName).AppendLine(".Domain,");
        builder.AppendLine("            body.AggregateId,");
        builder.Append("            ").Append(message.FullyQualifiedTypeName).AppendLine(".CommandType,");
        builder.AppendLine("            JsonSerializer.SerializeToElement(body, JsonOptions));");
        builder.AppendLine();
        builder.AppendLine("        try");
        builder.AppendLine("        {");
        builder.AppendLine("            SubmitCommandResponse __hexalithResponse = await gateway");
        builder.AppendLine("                .SubmitCommandAsync(__hexalithRequest, cancellationToken)");
        builder.AppendLine("                .ConfigureAwait(false);");
        builder.AppendLine();
        builder.AppendLine("            Response.Headers[\"Retry-After\"] = \"1\";");
        builder.AppendLine("            Response.Headers[\"Location\"] = \"/api/v1/commands/status/\" + Uri.EscapeDataString(__hexalithResponse.CorrelationId);");
        builder.AppendLine("            return Accepted(__hexalithResponse);");
        builder.AppendLine("        }");
        builder.AppendLine("        catch (EventStoreGatewayException ex)");
        builder.AppendLine("        {");
        builder.AppendLine("            return MapGatewayException(ex);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
    }

    private static void AppendQueryAction(StringBuilder builder, RestApiOptions options, RestApiMessageDescriptor message)
    {
        RestApiRouteDescriptor route = GetRoute(message);
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters = GetEffectiveRouteParameters(options, route);
        ImmutableArray<RestApiBindablePropertyDescriptor> queryParameters = GetQueryStringProperties(message, routeParameters);
        string methodName = GetQueryMethodName(message);

        builder.AppendLine();
        AppendHttpAttribute(builder, route);
        builder.AppendLine("    [ProducesResponseType(StatusCodes.Status200OK)]");
        builder.AppendLine("    [ProducesResponseType(StatusCodes.Status304NotModified)]");
        builder.Append("    public async Task<IActionResult> ").Append(methodName).AppendLine("(");
        bool hasEarlierParameter = AppendRouteParameters(builder, message, routeParameters, trailingComma: routeParameters.Length > 0 || queryParameters.Length > 0);
        AppendQueryParameters(builder, queryParameters, hasEarlierParameter);
        builder.AppendLine("        [FromHeader(Name = \"If-None-Match\")] string? ifNoneMatch,");
        builder.AppendLine("        CancellationToken cancellationToken)");
        builder.AppendLine("    {");
        AppendTenantResolution(builder, options, routeParameters);
        AppendQueryPayload(builder, queryParameters);
        AppendQueryRouteValues(builder, message, routeParameters);
        builder.AppendLine();
        builder.AppendLine("        var __hexalithRequest = new SubmitQueryRequest(");
        builder.AppendLine("            __hexalithTenant,");
        builder.Append("            ").Append(message.FullyQualifiedTypeName).AppendLine(".Domain,");
        builder.AppendLine("            __hexalithAggregateId,");
        builder.Append("            ").Append(message.FullyQualifiedTypeName).AppendLine(".QueryType,");
        builder.Append("            ").Append(message.FullyQualifiedTypeName).AppendLine(".ProjectionType,");
        builder.AppendLine("            __hexalithPayload,");
        builder.AppendLine("            __hexalithEntityId);");
        builder.AppendLine();
        builder.AppendLine("        try");
        builder.AppendLine("        {");
        builder.AppendLine("            EventStoreQueryResult __hexalithResult = await gateway");
        builder.AppendLine("                .SubmitQueryAsync(__hexalithRequest, ifNoneMatch, cancellationToken)");
        builder.AppendLine("                .ConfigureAwait(false);");
        builder.AppendLine();
        builder.AppendLine("            if (!string.IsNullOrWhiteSpace(__hexalithResult.ETag))");
        builder.AppendLine("            {");
        builder.AppendLine("                Response.Headers[\"ETag\"] = FormatStrongETag(__hexalithResult.ETag);");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            QueryResponseMetadata? __hexalithMetadata = __hexalithResult.Metadata;");
        builder.AppendLine("            string? __hexalithProjectionVersion = string.IsNullOrWhiteSpace(__hexalithMetadata?.ProjectionVersion)");
        builder.AppendLine("                ? __hexalithResult.ETag");
        builder.AppendLine("                : __hexalithMetadata.ProjectionVersion;");
        builder.AppendLine("            if (!string.IsNullOrWhiteSpace(__hexalithProjectionVersion))");
        builder.AppendLine("            {");
        builder.AppendLine("                Response.Headers[\"X-Hexalith-Projection-Version\"] = __hexalithProjectionVersion;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            if (__hexalithMetadata?.ServedAt is not null)");
        builder.AppendLine("            {");
        builder.AppendLine("                Response.Headers[\"X-Hexalith-Served-At\"] = __hexalithMetadata.ServedAt.Value.ToString(\"O\", CultureInfo.InvariantCulture);");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            if (__hexalithMetadata?.IsStale is not null)");
        builder.AppendLine("            {");
        builder.AppendLine("                Response.Headers[\"X-Hexalith-Is-Stale\"] = __hexalithMetadata.IsStale.Value ? \"true\" : \"false\";");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            if (__hexalithResult.IsNotModified)");
        builder.AppendLine("            {");
        builder.AppendLine("                return StatusCode(StatusCodes.Status304NotModified);");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            return Ok(__hexalithResult.Payload);");
        builder.AppendLine("        }");
        builder.AppendLine("        catch (EventStoreGatewayException ex)");
        builder.AppendLine("        {");
        builder.AppendLine("            return MapGatewayException(ex);");
        builder.AppendLine("        }");
        builder.AppendLine("        catch (ArgumentException ex)");
        builder.AppendLine("        {");
        builder.AppendLine("            return CreateProblem(StatusCodes.Status400BadRequest, \"Bad Request\", ex.Message);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
    }

    private static void AppendHelpers(StringBuilder builder, RestApiOptions options)
    {
        builder.AppendLine();
        builder.AppendLine("    private ActionResult<string> ResolveTenant(string? tenant, string? tenantId)");
        builder.AppendLine("    {");
        builder.Append("        const string tenantSource = ").Append(Literal(options.TenantSource)).AppendLine(";");
        builder.AppendLine("        if (string.Equals(tenantSource, \"System\", StringComparison.Ordinal))");
        builder.AppendLine("        {");
        builder.AppendLine("            return \"system\";");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        if (string.Equals(tenantSource, \"Route\", StringComparison.Ordinal))");
        builder.AppendLine("        {");
        builder.AppendLine("            string? normalizedTenant = string.IsNullOrWhiteSpace(tenant) ? null : tenant;");
        builder.AppendLine("            string? normalizedTenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;");
        builder.AppendLine("            if (normalizedTenant is not null");
        builder.AppendLine("                && normalizedTenantId is not null");
        builder.AppendLine("                && !string.Equals(normalizedTenant, normalizedTenantId, StringComparison.Ordinal))");
        builder.AppendLine("            {");
        builder.AppendLine("                return CreateProblem(StatusCodes.Status400BadRequest, \"Bad Request\", \"Tenant route values differ.\");");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            string? resolvedTenant = normalizedTenant ?? normalizedTenantId;");
        builder.AppendLine("            return string.IsNullOrWhiteSpace(resolvedTenant)");
        builder.AppendLine("                ? CreateProblem(StatusCodes.Status400BadRequest, \"Bad Request\", \"Tenant route value is required.\")");
        builder.AppendLine("                : resolvedTenant;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        string[] tenants = User.FindAll(\"eventstore:tenant\")");
        builder.AppendLine("            .Select(static claim => claim.Value)");
        builder.AppendLine("            .Where(static value => !string.IsNullOrWhiteSpace(value))");
        builder.AppendLine("            .Distinct(StringComparer.Ordinal)");
        builder.AppendLine("            .ToArray();");
        builder.AppendLine("        if (tenants.Length == 0)");
        builder.AppendLine("        {");
        builder.AppendLine("            return CreateProblem(StatusCodes.Status403Forbidden, \"Forbidden\", \"A tenant claim is required.\");");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return tenants.Length == 1");
        builder.AppendLine("            ? tenants[0]");
        builder.AppendLine("            : CreateProblem(StatusCodes.Status400BadRequest, \"Bad Request\", \"The tenant is ambiguous.\");");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private ObjectResult MapGatewayException(EventStoreGatewayException exception)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (!string.IsNullOrWhiteSpace(exception.RetryAfter))");
        builder.AppendLine("        {");
        builder.AppendLine("            Response.Headers[\"Retry-After\"] = exception.RetryAfter;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        var problem = new ProblemDetails");
        builder.AppendLine("        {");
        builder.AppendLine("            Status = exception.StatusCode,");
        builder.AppendLine("            Title = exception.Title,");
        builder.AppendLine("            Type = exception.Type,");
        builder.AppendLine("            Detail = exception.Detail,");
        builder.AppendLine("        };");
        builder.AppendLine();
        builder.AppendLine("        AddExtension(problem, \"correlationId\", exception.CorrelationId);");
        builder.AppendLine("        AddExtension(problem, \"tenantId\", exception.TenantId);");
        builder.AppendLine("        AddExtension(problem, \"reason\", exception.Reason);");
        builder.AppendLine("        AddExtension(problem, \"reasonCode\", exception.ReasonCode);");
        builder.AppendLine("        if (exception.Errors.Count > 0)");
        builder.AppendLine("        {");
        builder.AppendLine("            problem.Extensions[\"errors\"] = exception.Errors;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return new ObjectResult(problem)");
        builder.AppendLine("        {");
        builder.AppendLine("            StatusCode = exception.StatusCode,");
        builder.AppendLine("            ContentTypes = { \"application/problem+json\" },");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static ObjectResult CreateProblem(int statusCode, string title, string detail)");
        builder.AppendLine("    {");
        builder.AppendLine("        var problem = new ProblemDetails");
        builder.AppendLine("        {");
        builder.AppendLine("            Status = statusCode,");
        builder.AppendLine("            Title = title,");
        builder.AppendLine("            Type = \"about:blank\",");
        builder.AppendLine("            Detail = detail,");
        builder.AppendLine("        };");
        builder.AppendLine();
        builder.AppendLine("        return new ObjectResult(problem)");
        builder.AppendLine("        {");
        builder.AppendLine("            StatusCode = statusCode,");
        builder.AppendLine("            ContentTypes = { \"application/problem+json\" },");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static void AddExtension(ProblemDetails problem, string key, string? value)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (!string.IsNullOrWhiteSpace(value))");
        builder.AppendLine("        {");
        builder.AppendLine("            problem.Extensions[key] = value;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static string FormatStrongETag(string etag)");
        builder.AppendLine("    {");
        builder.AppendLine("        string value = etag.Trim();");
        builder.AppendLine("        if (value.StartsWith(\"W/\", StringComparison.Ordinal))");
        builder.AppendLine("        {");
        builder.AppendLine("            value = value.Substring(2).Trim();");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        return value.Length >= 2 && value[0] == '\"' && value[value.Length - 1] == '\"'");
        builder.AppendLine("            ? value");
        builder.AppendLine("            : \"\\\"\" + value.Trim('\"') + \"\\\"\";");
        builder.AppendLine("    }");
    }

    private static bool AppendRouteParameters(
        StringBuilder builder,
        RestApiMessageDescriptor message,
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters,
        bool trailingComma)
    {
        for (int i = 0; i < routeParameters.Length; i++)
        {
            RestApiRouteParameterDescriptor parameter = routeParameters[i];
            bool appendComma = trailingComma || i < routeParameters.Length - 1;
            builder.Append("        [FromRoute(Name = ").Append(Literal(parameter.Name)).Append(")] ")
                .Append("string ").Append(parameter.Identifier);
            builder.AppendLine(appendComma ? "," : string.Empty);
        }

        return routeParameters.Length > 0;
    }

    private static void AppendQueryParameters(
        StringBuilder builder,
        ImmutableArray<RestApiBindablePropertyDescriptor> queryParameters,
        bool hasEarlierParameter)
    {
        for (int i = 0; i < queryParameters.Length; i++)
        {
            RestApiBindablePropertyDescriptor parameter = queryParameters[i];
            string identifier = RestApiNameSanitizer.ToIdentifier(parameter.Name, parameter.Name, camelCase: true);
            builder.Append("        [FromQuery(Name = ").Append(Literal(parameter.JsonName)).Append(")] ")
                .Append(parameter.TypeName).Append(' ').Append(identifier).AppendLine(",");
            hasEarlierParameter = true;
        }
    }

    private static void AppendTenantResolution(
        StringBuilder builder,
        RestApiOptions options,
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters)
    {
        string tenantArgument = FindRouteParameterIdentifier(routeParameters, "tenant") ?? "null";
        string tenantIdArgument = FindRouteParameterIdentifier(routeParameters, "tenantId") ?? "null";
        builder.AppendLine();
        builder.Append("        ActionResult<string> __hexalithTenantResult = ResolveTenant(")
            .Append(RequiresRouteTenant(options) ? tenantArgument : "null")
            .Append(", ")
            .Append(RequiresRouteTenant(options) ? tenantIdArgument : "null")
            .AppendLine(");");
        builder.AppendLine("        if (__hexalithTenantResult.Result is not null)");
        builder.AppendLine("        {");
        builder.AppendLine("            return __hexalithTenantResult.Result;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        string __hexalithTenant = __hexalithTenantResult.Value!;");
    }

    private static void AppendCommandRouteMismatchChecks(
        StringBuilder builder,
        RestApiMessageDescriptor message,
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters,
        RestApiOptions options)
    {
        // Under Route tenant source the {tenant}/{tenantId} segment is the partition tenant and is
        // validated by ResolveTenant, so it is excluded from body reconciliation here. Under any
        // non-Route source (e.g. System) the tenant-named segment is a plain domain identifier —
        // typically the tenant aggregate id — and MUST be reconciled with the command body; otherwise
        // a route/body id mismatch such as POST /api/tenants/acme/disable {"tenantId":"other"} would
        // silently dispatch against the body id. AC2 requires a 400 problem detail on such a mismatch.
        ImmutableArray<RestApiRouteParameterDescriptor> reconciledParameters =
            RequiresRouteTenant(options)
                ? GetNonTenantParameters(routeParameters)
                : routeParameters;
        foreach (RestApiRouteParameterDescriptor parameter in reconciledParameters)
        {
            string routeValue = "Convert.ToString(" + parameter.Identifier + ", CultureInfo.InvariantCulture)";
            if (string.Equals(parameter.Name, "aggregateId", StringComparison.OrdinalIgnoreCase))
            {
                builder.AppendLine();
                builder.Append("        if (!string.Equals(").Append(routeValue)
                    .AppendLine(", body.AggregateId, StringComparison.Ordinal))");
                builder.AppendLine("        {");
                builder.AppendLine("            return CreateProblem(StatusCodes.Status400BadRequest, \"Bad Request\", \"Route aggregate id does not match the command body.\");");
                builder.AppendLine("        }");
                continue;
            }

            if (TryFindProperty(message, parameter.Name, out RestApiBindablePropertyDescriptor property))
            {
                builder.AppendLine();
                builder.Append("        if (!string.Equals(").Append(routeValue)
                    .Append(", Convert.ToString(body.").Append(RestApiNameSanitizer.EscapeIdentifier(property.Name))
                    .AppendLine(", CultureInfo.InvariantCulture), StringComparison.Ordinal))");
                builder.AppendLine("        {");
                builder.Append("            return CreateProblem(StatusCodes.Status400BadRequest, \"Bad Request\", ")
                    .Append(Literal("Route value '" + parameter.Name + "' does not match the command body."))
                    .AppendLine(");");
                builder.AppendLine("        }");
                continue;
            }

            if (reconciledParameters.Length == 1)
            {
                builder.AppendLine();
                builder.Append("        if (!string.Equals(").Append(routeValue)
                    .AppendLine(", body.AggregateId, StringComparison.Ordinal))");
                builder.AppendLine("        {");
                builder.AppendLine("            return CreateProblem(StatusCodes.Status400BadRequest, \"Bad Request\", \"Route aggregate id does not match the command body.\");");
                builder.AppendLine("        }");
            }
        }
    }

    private static void AppendQueryPayload(StringBuilder builder, ImmutableArray<RestApiBindablePropertyDescriptor> queryParameters)
    {
        builder.AppendLine();
        builder.AppendLine("        JsonElement? __hexalithPayload = null;");
        if (queryParameters.Length == 0)
        {
            return;
        }

        builder.AppendLine("        var __hexalithPayloadValues = new Dictionary<string, object?>(StringComparer.Ordinal)");
        builder.AppendLine("        {");
        foreach (RestApiBindablePropertyDescriptor parameter in queryParameters)
        {
            string identifier = RestApiNameSanitizer.ToIdentifier(parameter.Name, parameter.Name, camelCase: true);
            builder.Append("            [").Append(Literal(parameter.JsonName)).Append("] = ").Append(identifier).AppendLine(",");
        }

        builder.AppendLine("        };");
        builder.AppendLine("        __hexalithPayload = JsonSerializer.SerializeToElement(__hexalithPayloadValues, JsonOptions);");
    }

    private static void AppendQueryRouteValues(
        StringBuilder builder,
        RestApiMessageDescriptor message,
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters)
    {
        string aggregateExpression;
        string entityExpression;
        if (message.QueryBinding.HasValue)
        {
            RestApiQueryBindingDescriptor binding = message.QueryBinding.Value;
            aggregateExpression = GetQueryBindingExpression(
                binding.AggregateSource,
                binding.AggregateValue,
                routeParameters,
                Literal("index"));
            entityExpression = string.Equals(binding.EntitySource, "None", StringComparison.Ordinal)
                ? "null"
                : GetQueryBindingExpression(
                    binding.EntitySource,
                    binding.EntityValue ?? string.Empty,
                    routeParameters,
                    "null");
        }
        else
        {
            ImmutableArray<RestApiRouteParameterDescriptor> nonTenantParameters = GetNonTenantParameters(routeParameters);
            RestApiRouteParameterDescriptor? aggregateParameter = GetAggregateParameter(nonTenantParameters);
            RestApiRouteParameterDescriptor? entityParameter = GetEntityParameter(nonTenantParameters, aggregateParameter);
            aggregateExpression = aggregateParameter.HasValue
                ? ToStringExpression(aggregateParameter.Value.Identifier, Literal("index"))
                : Literal("index");
            entityExpression = entityParameter.HasValue
                ? ToStringExpression(entityParameter.Value.Identifier, "null")
                : "null";
        }

        builder.AppendLine();
        builder.Append("        string __hexalithAggregateId = ").Append(aggregateExpression).AppendLine(";");
        builder.Append("        string? __hexalithEntityId = ").Append(entityExpression).AppendLine(";");
    }

    private static void AppendHttpAttribute(StringBuilder builder, RestApiRouteDescriptor route)
    {
        if (!TryGetHttpAttributeName(route.Verb, out string httpAttributeName))
        {
            throw new InvalidOperationException("Unsupported REST verb '" + route.Verb + "'.");
        }

        builder.Append("    [").Append(httpAttributeName).Append('(')
            .Append(Literal(route.Template)).AppendLine(")]");
    }

    private static RestApiRouteDescriptor GetRoute(RestApiMessageDescriptor message)
    {
        if (message.Route.HasValue)
        {
            return message.Route.Value;
        }

        return new RestApiRouteDescriptor(message.IsCommand ? "Post" : "Get", string.Empty);
    }

    private static ImmutableArray<RestApiRouteParameterDescriptor> GetEffectiveRouteParameters(
        RestApiOptions options,
        RestApiRouteDescriptor route)
    {
        if (route.IsAbsolute)
        {
            return route.Parameters;
        }

        ImmutableArray<RestApiRouteParameterDescriptor> prefixParameters =
            RestApiRouteTemplateParser.ParseParameters(options.RoutePrefix);
        if (prefixParameters.Length == 0)
        {
            return route.Parameters;
        }

        if (route.Parameters.Length == 0)
        {
            return prefixParameters;
        }

        return prefixParameters.AddRange(route.Parameters);
    }

    private static bool TryFindRouteTemplateError(
        RestApiOptions options,
        RestApiRouteDescriptor route,
        out string template,
        out string reason)
    {
        if (route.TemplateError is not null)
        {
            template = route.Template;
            reason = route.TemplateError;
            return true;
        }

        string? prefixError = RestApiRouteTemplateParser.GetTemplateError(options.RoutePrefix);
        if (prefixError is not null)
        {
            template = options.RoutePrefix;
            reason = prefixError;
            return true;
        }

        template = string.Empty;
        reason = string.Empty;
        return false;
    }

    private static ImmutableArray<string> GetActionMethodNames(RestApiMessageDescriptor message)
    {
        var methodNames = ImmutableArray.CreateBuilder<string>(2);
        if (message.IsCommand)
        {
            methodNames.Add(GetCommandMethodName(message));
        }

        if (message.IsQuery)
        {
            methodNames.Add(GetQueryMethodName(message));
        }

        return methodNames.ToImmutable();
    }

    private static bool TryFindDuplicateActionMethodName(
        ImmutableArray<string> methodNames,
        HashSet<string> existingMethodNames,
        out string duplicateMethodName)
    {
        var candidateMethodNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (string methodName in methodNames)
        {
            if (!candidateMethodNames.Add(methodName) || existingMethodNames.Contains(methodName))
            {
                duplicateMethodName = methodName;
                return true;
            }
        }

        duplicateMethodName = string.Empty;
        return false;
    }

    private static ImmutableArray<string> GetActionRouteKeys(
        RestApiOptions options,
        RestApiMessageDescriptor message,
        RestApiRouteDescriptor route)
    {
        var routeKeys = ImmutableArray.CreateBuilder<string>(2);
        if (message.IsCommand)
        {
            routeKeys.Add(GetActionRouteKey(options, route));
        }

        if (message.IsQuery)
        {
            routeKeys.Add(GetActionRouteKey(options, route));
        }

        return routeKeys.ToImmutable();
    }

    private static bool TryFindDuplicateActionRoute(
        ImmutableArray<string> routeKeys,
        HashSet<string> existingRoutes,
        out string duplicateActionRoute)
    {
        var candidateRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string routeKey in routeKeys)
        {
            if (!candidateRoutes.Add(routeKey) || existingRoutes.Contains(routeKey))
            {
                duplicateActionRoute = routeKey;
                return true;
            }
        }

        duplicateActionRoute = string.Empty;
        return false;
    }

    private static string GetCommandMethodName(RestApiMessageDescriptor message)
        => RestApiNameSanitizer.ToIdentifier(message.SimpleTypeName + "Command", "Command", camelCase: false) + "Async";

    private static string GetQueryMethodName(RestApiMessageDescriptor message)
        => RestApiNameSanitizer.ToIdentifier(message.SimpleTypeName + "Query", "Query", camelCase: false) + "Async";

    private static string GetActionRouteKey(RestApiOptions options, RestApiRouteDescriptor route)
        => route.Verb + "|" + NormalizeRouteTemplateForKey(GetEffectiveRouteTemplate(options, route));

    private static string GetEffectiveRouteTemplate(RestApiOptions options, RestApiRouteDescriptor route)
    {
        if (route.IsAbsolute)
        {
            return route.Template;
        }

        string prefix = options.RoutePrefix.TrimEnd('/');
        string template = route.Template.TrimStart('/');
        if (prefix.Length == 0)
        {
            return template;
        }

        return template.Length == 0 ? prefix : prefix + "/" + template;
    }

    private static string NormalizeRouteTemplateForKey(string template)
    {
        string value = template.Trim();
        if (value.StartsWith("~/", StringComparison.Ordinal))
        {
            value = value.Substring(2);
        }

        value = value.Trim('/');
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        int index = 0;
        while (index < value.Length)
        {
            int open = value.IndexOf('{', index);
            if (open < 0)
            {
                builder.Append(value.Substring(index));
                break;
            }

            builder.Append(value.Substring(index, open - index));
            if (open + 1 < value.Length && value[open + 1] == '{')
            {
                builder.Append("{{");
                index = open + 2;
                continue;
            }

            int close = value.IndexOf('}', open + 1);
            if (close < 0)
            {
                builder.Append(value.Substring(open));
                break;
            }

            builder.Append(NormalizeRouteParameterForKey(value.Substring(open + 1, close - open - 1)));
            index = close + 1;
        }

        return builder.ToString();
    }

    private static string NormalizeRouteParameterForKey(string parameter)
    {
        string value = parameter.Trim();
        bool catchAll = false;
        while (value.StartsWith("*", StringComparison.Ordinal))
        {
            catchAll = true;
            value = value.Substring(1);
        }

        int terminator = value.IndexOfAny(new[] { ':', '=', '?' });
        string suffix = terminator >= 0 ? value.Substring(terminator) : string.Empty;
        return catchAll ? "{*" + suffix + "}" : "{" + suffix + "}";
    }

    private static bool TryFindDuplicateParameterIdentifier(
        RestApiMessageDescriptor message,
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters,
        out string duplicateParameter)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (string reserved in ReservedActionIdentifiers)
        {
            _ = identifiers.Add(reserved);
        }

        foreach (RestApiRouteParameterDescriptor parameter in routeParameters)
        {
            if (!identifiers.Add(parameter.Identifier))
            {
                duplicateParameter = parameter.Identifier;
                return true;
            }
        }

        if (message.IsQuery)
        {
            foreach (RestApiBindablePropertyDescriptor property in GetQueryStringProperties(message, routeParameters))
            {
                string identifier = RestApiNameSanitizer.ToIdentifier(property.Name, property.Name, camelCase: true);
                if (!identifiers.Add(identifier))
                {
                    duplicateParameter = identifier;
                    return true;
                }
            }
        }

        duplicateParameter = string.Empty;
        return false;
    }

    private static bool TryFindUnsupportedQueryParameter(
        RestApiMessageDescriptor message,
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters,
        out RestApiBindablePropertyDescriptor unsupportedProperty)
    {
        foreach (RestApiBindablePropertyDescriptor property in GetQueryStringProperties(message, routeParameters))
        {
            if (!property.CanBindFromQuery)
            {
                unsupportedProperty = property;
                return true;
            }
        }

        unsupportedProperty = default;
        return false;
    }

    private static bool TryFindDuplicateJsonName(RestApiMessageDescriptor message, out string duplicateJsonName)
    {
        var jsonNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (RestApiBindablePropertyDescriptor property in message.Properties)
        {
            if (!jsonNames.Add(property.JsonName))
            {
                duplicateJsonName = property.JsonName;
                return true;
            }
        }

        duplicateJsonName = string.Empty;
        return false;
    }

    private static bool TryFindUnmappedQueryBindingRouteParameter(
        RestApiMessageDescriptor message,
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters,
        out string routeParameterName)
    {
        if (!message.QueryBinding.HasValue)
        {
            routeParameterName = string.Empty;
            return false;
        }

        RestApiQueryBindingDescriptor binding = message.QueryBinding.Value;
        if (string.Equals(binding.AggregateSource, "Route", StringComparison.Ordinal)
            && FindRouteParameter(routeParameters, binding.AggregateValue) is null)
        {
            routeParameterName = binding.AggregateValue;
            return true;
        }

        if (string.Equals(binding.EntitySource, "Route", StringComparison.Ordinal)
            && FindRouteParameter(routeParameters, binding.EntityValue ?? string.Empty) is null)
        {
            routeParameterName = binding.EntityValue ?? string.Empty;
            return true;
        }

        routeParameterName = string.Empty;
        return false;
    }

    private static bool TryFindUnmappedCommandRouteParameter(
        RestApiMessageDescriptor message,
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters,
        out RestApiRouteParameterDescriptor unmappedParameter)
    {
        ImmutableArray<RestApiRouteParameterDescriptor> nonTenantParameters = GetNonTenantParameters(routeParameters);
        foreach (RestApiRouteParameterDescriptor parameter in nonTenantParameters)
        {
            if (string.Equals(parameter.Name, "aggregateId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryFindProperty(message, parameter.Name, out RestApiBindablePropertyDescriptor property))
            {
                if (property.CanBindFromQuery)
                {
                    continue;
                }

                unmappedParameter = parameter;
                return true;
            }

            if (nonTenantParameters.Length <= 1)
            {
                continue;
            }

            unmappedParameter = parameter;
            return true;
        }

        unmappedParameter = default;
        return false;
    }

    private static string GetControllerName(RestApiOptions options)
    {
        string source = !string.IsNullOrWhiteSpace(options.Tag)
            ? options.Tag
            : options.RoutePrefix;
        return RestApiNameSanitizer.ToTypeName(source, "RestApi") + "RestController";
    }

    private static string GetTag(RestApiOptions options)
        => !string.IsNullOrWhiteSpace(options.Tag)
            ? options.Tag
            : RestApiNameSanitizer.ToTypeName(options.RoutePrefix, "RestApi");

    private static bool IsSupportedRestVerb(string verb)
        => TryGetHttpAttributeName(verb, out _);

    private static bool TryGetHttpAttributeName(string verb, out string httpAttributeName)
    {
        switch (verb)
        {
            case "Delete":
                httpAttributeName = "HttpDelete";
                return true;
            case "Get":
                httpAttributeName = "HttpGet";
                return true;
            case "Patch":
                httpAttributeName = "HttpPatch";
                return true;
            case "Put":
                httpAttributeName = "HttpPut";
                return true;
            case "Post":
                httpAttributeName = "HttpPost";
                return true;
            default:
                httpAttributeName = string.Empty;
                return false;
        }
    }

    private static ImmutableArray<RestApiRouteParameterDescriptor> GetNonTenantParameters(
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters)
        => routeParameters
            .Where(static parameter => !IsTenantParameter(parameter))
            .ToImmutableArray();

    private static RestApiRouteParameterDescriptor? GetAggregateParameter(ImmutableArray<RestApiRouteParameterDescriptor> nonTenantParameters)
    {
        foreach (RestApiRouteParameterDescriptor parameter in nonTenantParameters)
        {
            if (string.Equals(parameter.Name, "aggregateId", StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
            }
        }

        return nonTenantParameters.Length == 1 ? nonTenantParameters[0] : null;
    }

    private static RestApiRouteParameterDescriptor? GetEntityParameter(
        ImmutableArray<RestApiRouteParameterDescriptor> nonTenantParameters,
        RestApiRouteParameterDescriptor? aggregateParameter)
    {
        foreach (RestApiRouteParameterDescriptor parameter in nonTenantParameters)
        {
            if (string.Equals(parameter.Name, "entityId", StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
            }
        }

        foreach (RestApiRouteParameterDescriptor parameter in nonTenantParameters)
        {
            if (!aggregateParameter.HasValue || !parameter.Equals(aggregateParameter.Value))
            {
                return parameter;
            }
        }

        return null;
    }

    private static bool IsAmbiguousQueryRoute(ImmutableArray<RestApiRouteParameterDescriptor> routeParameters)
    {
        ImmutableArray<RestApiRouteParameterDescriptor> nonTenantParameters = GetNonTenantParameters(routeParameters);
        if (nonTenantParameters.Length <= 1)
        {
            return false;
        }

        bool hasAggregateId = nonTenantParameters.Any(static parameter =>
            string.Equals(parameter.Name, "aggregateId", StringComparison.OrdinalIgnoreCase));
        bool hasEntityId = nonTenantParameters.Any(static parameter =>
            string.Equals(parameter.Name, "entityId", StringComparison.OrdinalIgnoreCase));
        if (!hasAggregateId)
        {
            return true;
        }

        int extraParameters = nonTenantParameters.Length - 1 - (hasEntityId ? 1 : 0);
        return hasEntityId ? extraParameters > 0 : extraParameters > 1;
    }

    private static ImmutableArray<RestApiBindablePropertyDescriptor> GetQueryStringProperties(
        RestApiMessageDescriptor message,
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters)
        => message.Properties
            .Where(property => !routeParameters.Any(routeParameter =>
                RouteParameterMatchesProperty(routeParameter.Name, property)))
            .ToImmutableArray();

    private static bool TryFindProperty(
        RestApiMessageDescriptor message,
        string routeParameterName,
        out RestApiBindablePropertyDescriptor property)
    {
        foreach (RestApiBindablePropertyDescriptor candidate in message.Properties)
        {
            if (RouteParameterMatchesProperty(routeParameterName, candidate))
            {
                property = candidate;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static bool RouteParameterMatchesProperty(string routeParameterName, RestApiBindablePropertyDescriptor property)
        => string.Equals(property.Name, routeParameterName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(property.JsonName, routeParameterName, StringComparison.Ordinal);

    private static string? FindRouteParameterIdentifier(
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters,
        string name)
        => FindRouteParameter(routeParameters, name)?.Identifier;

    private static RestApiRouteParameterDescriptor? FindRouteParameter(
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters,
        string name)
    {
        foreach (RestApiRouteParameterDescriptor parameter in routeParameters)
        {
            if (string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
            }
        }

        return null;
    }

    private static bool HasTenantRouteParameter(ImmutableArray<RestApiRouteParameterDescriptor> routeParameters)
        => routeParameters.Any(static parameter => IsTenantParameter(parameter));

    private static bool IsTenantParameter(RestApiRouteParameterDescriptor parameter)
        => string.Equals(parameter.Name, "tenant", StringComparison.OrdinalIgnoreCase)
            || string.Equals(parameter.Name, "tenantId", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresRouteTenant(RestApiOptions options)
        => string.Equals(options.TenantSource, "Route", StringComparison.Ordinal);

    private static string ToStringExpression(string identifier, string fallbackExpression)
        => "Convert.ToString(" + identifier + ", CultureInfo.InvariantCulture) ?? " + fallbackExpression;

    private static string GetQueryBindingExpression(
        string source,
        string value,
        ImmutableArray<RestApiRouteParameterDescriptor> routeParameters,
        string fallbackExpression)
    {
        if (string.Equals(source, "Constant", StringComparison.Ordinal))
        {
            return Literal(value);
        }

        if (string.Equals(source, "Route", StringComparison.Ordinal)
            && FindRouteParameter(routeParameters, value) is { } routeParameter)
        {
            return ToStringExpression(routeParameter.Identifier, fallbackExpression);
        }

        return fallbackExpression;
    }

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);
}
