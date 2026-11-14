using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

/// <summary>
/// Story 2.17 (NFR13/OR26) conformance gate over the REST generator's emitted sources. Each rule fails
/// closed and has a seeded-violation test that mutates real generated text.
/// </summary>
public sealed class RestApiGeneratedCodeQualityTests
{
    private const string HappyPathFixture = "happy-path";
    private const string BoundQueryFixture = "bound-query";
    private const string RouteTenantFixture = "route-tenant";
    private const string ControllerHintSuffix = ".Controller.g.cs";
    private const string ControllerAttributeAnchor = "[ApiController]";
    private const string SubmitCommandAnchor = "var __hexalithRequest = new SubmitCommandRequest(";

    private static readonly string[] GuidParsingMethodNames =
    [
        nameof(Guid.Parse),
        nameof(Guid.TryParse),
        nameof(Guid.ParseExact),
        nameof(Guid.TryParseExact),
    ];

    // Attributes that make compilers or analyzers skip the code they decorate.
    private static readonly string[] AnalyzerBlindingAttributeTypeNames =
    [
        "System.CodeDom.Compiler.GeneratedCodeAttribute",
        "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute",
        "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute",
    ];

    [Theory]
    [InlineData(HappyPathFixture)]
    [InlineData(BoundQueryFixture)]
    [InlineData(RouteTenantFixture)]
    public void GeneratedSources_ReportNoWarningUnderWarningsAsErrors(string fixture)
    {
        (CSharpCompilation compilation, ImmutableArray<SyntaxTree> generatedTrees) = Generate(fixture);

        string[] violations = FindWarningViolations(compilation, generatedTrees);

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [InlineData(HappyPathFixture)]
    [InlineData(BoundQueryFixture)]
    [InlineData(RouteTenantFixture)]
    public void GeneratedSources_DeclareNullableEnable(string fixture)
    {
        (_, ImmutableArray<SyntaxTree> generatedTrees) = Generate(fixture);

        string[] violations = FindNullableDirectiveViolations(generatedTrees);

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [InlineData(HappyPathFixture)]
    [InlineData(BoundQueryFixture)]
    [InlineData(RouteTenantFixture)]
    public void GeneratedSources_ConfigureEveryAwaitWithFalse(string fixture)
    {
        (_, ImmutableArray<SyntaxTree> generatedTrees) = Generate(fixture);

        string[] violations = FindConfigureAwaitViolations(generatedTrees, out int scannedAwaits);

        scannedAwaits.ShouldBeGreaterThan(0, "The await check must scan at least one await, otherwise it proves nothing.");
        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [InlineData(HappyPathFixture)]
    [InlineData(BoundQueryFixture)]
    [InlineData(RouteTenantFixture)]
    public void GeneratedSources_NeverBindToGuidParsing(string fixture)
    {
        (CSharpCompilation compilation, ImmutableArray<SyntaxTree> generatedTrees) = Generate(fixture);

        string[] violations = FindGuidParsingViolations(compilation, generatedTrees, out int scannedInvocations);

        scannedInvocations.ShouldBeGreaterThan(0, "The Guid-parsing check must bind at least one invocation, otherwise it proves nothing.");
        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void SeededUnusedLocal_IsReportedByTheWarningCheck()
    {
        (CSharpCompilation compilation, ImmutableArray<SyntaxTree> generatedTrees) = SeedController(
            Generate(HappyPathFixture),
            SubmitCommandAnchor,
            "int __hexalithSeededUnused = 0; " + SubmitCommandAnchor);

        string[] violations = FindWarningViolations(compilation, generatedTrees);

        violations.ShouldContain(
            violation => violation.Contains("CS0219", StringComparison.Ordinal),
            "The warning check must name the seeded unused-local diagnostic. Found: " + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void SeededMissingNullableDirective_IsReportedByTheDirectiveAndWarningChecks()
    {
        (CSharpCompilation compilation, ImmutableArray<SyntaxTree> generatedTrees) = SeedController(
            Generate(HappyPathFixture),
            "#nullable enable",
            string.Empty);
        string controllerFileName = Path.GetFileName(GetController(generatedTrees).FilePath);

        string[] directiveViolations = FindNullableDirectiveViolations(generatedTrees);
        string[] warningViolations = FindWarningViolations(compilation, generatedTrees);

        directiveViolations.ShouldHaveSingleItem().ShouldContain(controllerFileName);
        warningViolations.ShouldContain(
            violation => violation.Contains("CS8669", StringComparison.Ordinal),
            "Removing the directive must surface CS8669 in the generated tree. Found: " + string.Join(Environment.NewLine, warningViolations));
    }

    [Fact]
    public void SeededMissingConfigureAwait_IsReportedWithItsMember()
    {
        (CSharpCompilation original, ImmutableArray<SyntaxTree> originalTrees) = Generate(HappyPathFixture);
        AwaitExpressionSyntax firstAwait = GetController(originalTrees)
            .GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<AwaitExpressionSyntax>()
            .First();
        string expectedMember = DescribeMember(firstAwait);
        (_, ImmutableArray<SyntaxTree> generatedTrees) = SeedController(
            (original, originalTrees),
            ".ConfigureAwait(false)",
            string.Empty);

        string[] violations = FindConfigureAwaitViolations(generatedTrees, out int scannedAwaits);

        scannedAwaits.ShouldBeGreaterThan(1);
        violations.ShouldHaveSingleItem().ShouldContain("'" + expectedMember + "'");
    }

    [Theory]
    [InlineData("#pragma warning disable\n", "'#pragma warning'")]
    [InlineData("[System.CodeDom.Compiler.GeneratedCode(\"seed\", \"1.0\")] ", "System.CodeDom.Compiler.GeneratedCodeAttribute")]
    [InlineData("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Reliability\", \"CA2007\")] ", "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute")]
    [InlineData("[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"Reliability\", \"CA2007\")] ", "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute")]
    public void SeededAnalyzerBlinding_IsReportedByTheWarningCheck(string seed, string expectedViolation)
    {
        (CSharpCompilation compilation, ImmutableArray<SyntaxTree> generatedTrees) = SeedController(
            Generate(HappyPathFixture),
            ControllerAttributeAnchor,
            seed + ControllerAttributeAnchor);

        string[] violations = FindWarningViolations(compilation, generatedTrees);

        violations.ShouldHaveSingleItem().ShouldContain(expectedViolation);
    }

    // The parse call is assembled with nameof so this test source never reads as a Guid validator.
    [Theory]
    [InlineData("_ = Guid." + nameof(Guid.TryParse) + "(messageId, out _);", "Guid." + nameof(Guid.TryParse) + "(messageId, out _)")]
    [InlineData("_ = new Guid(messageId);", "new Guid(messageId)")]
    [InlineData("Guid __hexalithSeededGuid = new(messageId);", "new(messageId)")]
    public void SeededGuidParsing_IsReportedWithTheCall(string seededStatement, string expectedCall)
    {
        (CSharpCompilation compilation, ImmutableArray<SyntaxTree> generatedTrees) = SeedController(
            Generate(HappyPathFixture),
            SubmitCommandAnchor,
            "string messageId = UniqueIdHelper.GenerateSortableUniqueStringId(); " + seededStatement + " " + SubmitCommandAnchor);

        string[] violations = FindGuidParsingViolations(compilation, generatedTrees, out _);

        violations.ShouldHaveSingleItem().ShouldContain(expectedCall);
    }

    private static (CSharpCompilation Compilation, ImmutableArray<SyntaxTree> GeneratedTrees) Generate(string fixture)
    {
        CSharpCompilation input = fixture switch
        {
            HappyPathFixture => RestApiGeneratorTestHarness.CreateCompilation(
                RestApiControllerGenerationTests.HappyPathControllerSource),
            BoundQueryFixture => RestApiGeneratorTestHarness.CreateCompilation(
                [RestApiGeneratorTestHarness.EmitToMetadataReference(
                    RestApiGeneratorTestHarness.CreateCompilation(RestApiControllerGenerationTests.ReferencedBoundTenantsQuerySource))],
                RestApiControllerGenerationTests.ReferencedTenantsContractHostSource),
            RouteTenantFixture => RestApiGeneratorTestHarness.CreateCompilation(
                RestApiControllerGenerationTests.RouteTenantSource),
            _ => throw new ArgumentOutOfRangeException(nameof(fixture), fixture, "Unknown generated-code fixture."),
        };

        CSharpCompilation output = RestApiGeneratorTestHarness.RunAndUpdateCompilation(
            input,
            out GeneratorDriverRunResult runResult,
            out ImmutableArray<Diagnostic> generatorDiagnostics);
        Diagnostic[] generatorWarnings = generatorDiagnostics
            .Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .ToArray();
        generatorWarnings.ShouldBeEmpty(
            $"Fixture '{fixture}' must generate without Warning+ generator diagnostics: "
            + string.Join(Environment.NewLine, generatorWarnings.Select(static diagnostic => diagnostic.ToString())));
        HashSet<string> generatedPaths = runResult.GeneratedTrees
            .Select(static tree => tree.FilePath)
            .ToHashSet(StringComparer.Ordinal);

        // Use the compilation's own tree instances so seeded replacements and diagnostics line up.
        ImmutableArray<SyntaxTree> generatedTrees = output.SyntaxTrees
            .Where(tree => generatedPaths.Contains(tree.FilePath))
            .ToImmutableArray();

        generatedTrees.ShouldNotBeEmpty($"Fixture '{fixture}' must produce generated sources, otherwise every check is vacuous.");
        generatedTrees.Length.ShouldBe(generatedPaths.Count);
        _ = GetController(generatedTrees);
        return (output, generatedTrees);
    }

    private static (CSharpCompilation Compilation, ImmutableArray<SyntaxTree> GeneratedTrees) SeedController(
        (CSharpCompilation Compilation, ImmutableArray<SyntaxTree> GeneratedTrees) generated,
        string anchor,
        string replacement)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SyntaxTree controller = GetController(generated.GeneratedTrees);
        string source = controller.GetText(cancellationToken).ToString();
        int index = source.IndexOf(anchor, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0, $"The seed anchor '{anchor}' must occur in the real generated controller.");

        string seeded = string.Concat(source.AsSpan(0, index), replacement, source.AsSpan(index + anchor.Length));
        SyntaxTree seededTree = CSharpSyntaxTree.ParseText(
            SourceText.From(seeded, Encoding.UTF8),
            (CSharpParseOptions)controller.Options,
            controller.FilePath,
            cancellationToken);

        return (
            generated.Compilation.ReplaceSyntaxTree(controller, seededTree),
            generated.GeneratedTrees.Replace(controller, seededTree));
    }

    private static SyntaxTree GetController(ImmutableArray<SyntaxTree> generatedTrees)
        => generatedTrees
            .Where(static tree => tree.FilePath.EndsWith(ControllerHintSuffix, StringComparison.Ordinal))
            .ShouldHaveSingleItem("Each fixture must generate exactly one controller.");

    private static string[] FindWarningViolations(CSharpCompilation compilation, ImmutableArray<SyntaxTree> generatedTrees)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CSharpCompilation strict = compilation.WithOptions(compilation.Options
            .WithWarningLevel(9999)
            .WithGeneralDiagnosticOption(ReportDiagnostic.Error));
        HashSet<string> generatedPaths = generatedTrees
            .Select(static tree => tree.FilePath)
            .ToHashSet(StringComparer.Ordinal);

        List<string> violations = strict
            .GetDiagnostics(cancellationToken)
            .Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning || diagnostic.IsWarningAsError)
            .Where(diagnostic => diagnostic.Location.SourceTree is { } tree && generatedPaths.Contains(tree.FilePath))
            .Select(static diagnostic => diagnostic.ToString())
            .ToList();

        // A pragma or a blinding attribute in generated text would hide it from the compiler or the analyzers.
        foreach (SyntaxTree tree in generatedTrees)
        {
            SyntaxNode root = tree.GetRoot(cancellationToken);
            if (root.DescendantTrivia().Any(static trivia => trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia)))
            {
                violations.Add(Path.GetFileName(tree.FilePath) + ": generated code must not contain '#pragma warning' directives.");
            }

            SemanticModel model = strict.GetSemanticModel(tree);
            foreach (AttributeSyntax attribute in root.DescendantNodes().OfType<AttributeSyntax>())
            {
                string? blindingType = GetBoundSymbols(model.GetSymbolInfo(attribute, cancellationToken))
                    .Select(static symbol => symbol.ContainingType?.ToDisplayString())
                    .FirstOrDefault(static typeName => typeName is not null
                        && AnalyzerBlindingAttributeTypeNames.Contains(typeName, StringComparer.Ordinal));
                if (blindingType is not null)
                {
                    int line = tree.GetLineSpan(attribute.Span, cancellationToken).StartLinePosition.Line + 1;
                    violations.Add(
                        Path.GetFileName(tree.FilePath) + ":" + line + ": attribute '" + attribute + "' binds to "
                        + blindingType + ", which hides generated code from analyzers.");
                }
            }
        }

        return [.. violations];
    }

    private static ImmutableArray<ISymbol> GetBoundSymbols(SymbolInfo symbolInfo)
        => symbolInfo.Symbol is { } symbol ? [symbol] : symbolInfo.CandidateSymbols;

    private static string[] FindNullableDirectiveViolations(ImmutableArray<SyntaxTree> generatedTrees)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var violations = new List<string>();
        foreach (SyntaxTree tree in generatedTrees)
        {
            SyntaxNode root = tree.GetRoot(cancellationToken);
            bool declaredAtFileStart = root
                .GetFirstToken(includeZeroWidth: true)
                .LeadingTrivia
                .Any(static trivia => trivia.GetStructure() is NullableDirectiveTriviaSyntax directive
                    && directive.SettingToken.IsKind(SyntaxKind.EnableKeyword)
                    && directive.TargetToken.IsKind(SyntaxKind.None));
            int nullableDirectiveCount = root
                .DescendantTrivia()
                .Count(static trivia => trivia.IsKind(SyntaxKind.NullableDirectiveTrivia));

            if (!declaredAtFileStart || nullableDirectiveCount != 1)
            {
                violations.Add(
                    Path.GetFileName(tree.FilePath)
                    + ": generated file must declare a single '#nullable enable' before its first token (found "
                    + nullableDirectiveCount
                    + " nullable directive(s)).");
            }
        }

        return [.. violations];
    }

    private static string[] FindConfigureAwaitViolations(ImmutableArray<SyntaxTree> generatedTrees, out int scannedAwaits)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var violations = new List<string>();
        scannedAwaits = 0;
        foreach (SyntaxTree tree in generatedTrees)
        {
            foreach (SyntaxNode node in tree.GetRoot(cancellationToken).DescendantNodes())
            {
                foreach (ExpressionSyntax? operand in GetAwaitedOperands(node))
                {
                    scannedAwaits++;
                    if (!IsConfigureAwaitFalse(operand))
                    {
                        int line = tree.GetLineSpan(node.Span, cancellationToken).StartLinePosition.Line + 1;
                        violations.Add(
                            Path.GetFileName(tree.FilePath) + ":" + line + ": await in '" + DescribeMember(node)
                            + "' does not end in .ConfigureAwait(false): " + (operand?.ToString() ?? node.ToString()));
                    }
                }
            }
        }

        return [.. violations];
    }

    private static IEnumerable<ExpressionSyntax?> GetAwaitedOperands(SyntaxNode node)
    {
        switch (node)
        {
            case AwaitExpressionSyntax awaitExpression:
                yield return awaitExpression.Expression;
                break;
            case CommonForEachStatementSyntax forEach when forEach.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword):
                yield return forEach.Expression;
                break;
            case UsingStatementSyntax usingStatement when usingStatement.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword):
                if (usingStatement.Expression is not null)
                {
                    yield return usingStatement.Expression;
                }

                foreach (VariableDeclaratorSyntax variable in usingStatement.Declaration?.Variables ?? default)
                {
                    yield return variable.Initializer?.Value;
                }

                break;
            case LocalDeclarationStatementSyntax localDeclaration when localDeclaration.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword):
                foreach (VariableDeclaratorSyntax variable in localDeclaration.Declaration.Variables)
                {
                    yield return variable.Initializer?.Value;
                }

                break;
        }
    }

    private static bool IsConfigureAwaitFalse(ExpressionSyntax? expression)
        => expression is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: nameof(Task.ConfigureAwait) },
            ArgumentList.Arguments: [{ Expression: LiteralExpressionSyntax argument }],
        }
        && argument.IsKind(SyntaxKind.FalseLiteralExpression);

    private static string[] FindGuidParsingViolations(
        CSharpCompilation compilation,
        ImmutableArray<SyntaxTree> generatedTrees,
        out int scannedInvocations)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        INamedTypeSymbol guidType = compilation.GetTypeByMetadataName("System.Guid")
            ?? throw new InvalidOperationException("System.Guid must resolve, otherwise the Guid-parsing check is vacuous.");
        var violations = new List<string>();
        scannedInvocations = 0;
        foreach (SyntaxTree tree in generatedTrees)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            SyntaxNode root = tree.GetRoot(cancellationToken);
            scannedInvocations += root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Count(invocation => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol);

            // Names cover invocations, method groups and using-static calls alike.
            foreach (SimpleNameSyntax name in root.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                IMethodSymbol? parsingMethod = GetBoundSymbols(model.GetSymbolInfo(name, cancellationToken))
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(method => SymbolEqualityComparer.Default.Equals(method.ContainingType, guidType)
                        && GuidParsingMethodNames.Contains(method.Name, StringComparer.Ordinal));
                if (parsingMethod is null)
                {
                    continue;
                }

                SyntaxNode call = (SyntaxNode?)name.FirstAncestorOrSelf<InvocationExpressionSyntax>() ?? name;
                int line = tree.GetLineSpan(call.Span, cancellationToken).StartLinePosition.Line + 1;
                violations.Add(
                    Path.GetFileName(tree.FilePath) + ":" + line + ": '" + call + "' binds to System.Guid."
                    + parsingMethod.Name + "; EventStore identifiers are ULIDs.");
            }

            // The Guid(string) and Guid(ReadOnlySpan<char>) constructors parse their argument too.
            foreach (BaseObjectCreationExpressionSyntax creation in root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                bool parsesText = GetBoundSymbols(model.GetSymbolInfo(creation, cancellationToken))
                    .OfType<IMethodSymbol>()
                    .Any(constructor => SymbolEqualityComparer.Default.Equals(constructor.ContainingType, guidType)
                        && constructor.Parameters.Length > 0
                        && IsTextType(constructor.Parameters[0].Type));
                if (parsesText)
                {
                    int line = tree.GetLineSpan(creation.Span, cancellationToken).StartLinePosition.Line + 1;
                    violations.Add(
                        Path.GetFileName(tree.FilePath) + ":" + line + ": '" + creation
                        + "' parses text into System.Guid; EventStore identifiers are ULIDs.");
                }
            }
        }

        return [.. violations];
    }

    private static bool IsTextType(ITypeSymbol type)
        => type.SpecialType == SpecialType.System_String
        || (type is INamedTypeSymbol { Name: "ReadOnlySpan", TypeArguments: [{ SpecialType: SpecialType.System_Char }] } span
            && span.ContainingNamespace.ToDisplayString() == "System");

    private static string DescribeMember(SyntaxNode node)
        => node.AncestorsAndSelf()
            .Select(static ancestor => ancestor switch
            {
                LocalFunctionStatementSyntax localFunction => localFunction.Identifier.ValueText,
                MethodDeclarationSyntax method => method.Identifier.ValueText,
                ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
                PropertyDeclarationSyntax property => property.Identifier.ValueText,
                _ => null,
            })
            .FirstOrDefault(static memberName => memberName is not null)
            ?? "<unknown member>";
}
