using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Testing.Generator;

[Generator]
public sealed class TestGenerator : IIncrementalGenerator
{
    private static readonly HashSet<SpecialType> SupportedSpecialTypes =
    [
        SpecialType.System_Boolean,
        SpecialType.System_Byte,
        SpecialType.System_SByte,
        SpecialType.System_Int16,
        SpecialType.System_UInt16,
        SpecialType.System_Int32,
        SpecialType.System_UInt32,
        SpecialType.System_Int64,
        SpecialType.System_UInt64,
        SpecialType.System_Char,
        SpecialType.System_Decimal,
        SpecialType.System_String,
    ];

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var datasetIdsFromFiles = context.AdditionalTextsProvider
            .Select((text, ct) => ExtractDatasetIdFromPath(text.Path))
            .Where(id => id is not null)
            .Collect()
            .Select((ids, ct) =>
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var id in ids)
                {
                    if (id is not null)
                        set.Add(id);
                }
                return set;
            });

        var discovery = context.CompilationProvider.Select(
            (compilation, ct) => DiscoverAndValidate(compilation));

        var combined = discovery.Combine(datasetIdsFromFiles)
            .Select((pair, ct) =>
            {
                var (result, knownIds) = pair;
                var allDiagnostics = new List<Diagnostic>(result.Diagnostics);

                foreach (var kvp in result.TargetLocations)
                {
                    if (!knownIds.Contains(kvp.Key))
                    {
                        allDiagnostics.Add(Diagnostic.Create(
                            Diagnostics.NoDatasetFiles, kvp.Value, kvp.Key));
                    }
                }

                return new DiscoveryResult(
                    result.Targets.ToList(), allDiagnostics, result.TargetLocations);
            });

        context.RegisterSourceOutput(combined, (spc, result) =>
        {
            foreach (var diagnostic in result.Diagnostics)
                spc.ReportDiagnostic(diagnostic);

            foreach (var target in result.Targets)
                EmitSource(spc, target);
        });
    }

    private static DiscoveryResult DiscoverAndValidate(Compilation compilation)
    {
        var attributeSymbol = compilation.GetTypeByMetadataName(
            "Testing.Abstractions.GenerateTestsAttribute");

        if (attributeSymbol is null)
            return DiscoveryResult.Empty;

        var targets = new List<MethodTarget>();
        var diagnostics = new List<Diagnostic>();
        var datasetIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var targetLocations = new Dictionary<string, Location>(StringComparer.OrdinalIgnoreCase);

        var assemblies = GetAssembliesToSearch(compilation);
        foreach (var assembly in assemblies)
        {
            DiscoverInNamespace(
                assembly.GlobalNamespace, attributeSymbol, diagnostics, datasetIdMap,
                targets, targetLocations);
        }

        return new DiscoveryResult(targets, diagnostics, targetLocations);
    }

    private static IEnumerable<IAssemblySymbol> GetAssembliesToSearch(Compilation compilation)
    {
        yield return compilation.Assembly;
        foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
            yield return referencedAssembly;
    }

    private static void DiscoverInNamespace(
        INamespaceSymbol namespaceSymbol,
        INamedTypeSymbol attributeSymbol,
        List<Diagnostic> diagnostics,
        Dictionary<string, string> datasetIdMap,
        List<MethodTarget> targets,
        Dictionary<string, Location> targetLocations)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
            {
                ProcessMethod(method, attributeSymbol, diagnostics, datasetIdMap,
                    targets, targetLocations);
            }

            DiscoverInNestedTypes(type, attributeSymbol, diagnostics, datasetIdMap,
                targets, targetLocations);
        }

        foreach (var childNs in namespaceSymbol.GetNamespaceMembers())
        {
            DiscoverInNamespace(childNs, attributeSymbol, diagnostics, datasetIdMap,
                targets, targetLocations);
        }
    }

    private static void DiscoverInNestedTypes(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol attributeSymbol,
        List<Diagnostic> diagnostics,
        Dictionary<string, string> datasetIdMap,
        List<MethodTarget> targets,
        Dictionary<string, Location> targetLocations)
    {
        foreach (var nestedType in typeSymbol.GetTypeMembers())
        {
            foreach (var method in nestedType.GetMembers().OfType<IMethodSymbol>())
            {
                ProcessMethod(method, attributeSymbol, diagnostics, datasetIdMap,
                    targets, targetLocations);
            }

            DiscoverInNestedTypes(nestedType, attributeSymbol, diagnostics, datasetIdMap,
                targets, targetLocations);
        }
    }

    private static void ProcessMethod(
        IMethodSymbol method,
        INamedTypeSymbol attributeSymbol,
        List<Diagnostic> diagnostics,
        Dictionary<string, string> datasetIdMap,
        List<MethodTarget> targets,
        Dictionary<string, Location> targetLocations)
    {
        var attributeData = method.GetAttributes().FirstOrDefault(
            a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeSymbol));

        if (attributeData is null)
            return;

        var location = GetDiagnosticLocation(attributeData, method);
        var preCount = diagnostics.Count;

        var datasetId = ExtractDatasetId(attributeData, method, location, diagnostics);

        if (datasetId is null)
            return;

        ValidateMethodShape(method, datasetId, location, diagnostics);

        if (diagnostics.Count > preCount)
            return;

        var qualifiedMethodName = FormatQualifiedName(method);

        if (datasetIdMap.TryGetValue(datasetId, out var existing))
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.DuplicateDatasetId, location, datasetId, existing));
            return;
        }

        datasetIdMap[datasetId] = qualifiedMethodName;
        targetLocations[datasetId] = location;

        targets.Add(new MethodTarget
        {
            MethodName = method.Name,
            TypeName = method.ContainingType.Name,
            Namespace = method.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            DatasetId = datasetId,
            Parameters = new EquatableArray<MethodParameterTarget>(
                method.Parameters.Select(p => new MethodParameterTarget
                {
                    Name = p.Name,
                    TypeName = MapTypeToKeyword(p.Type)
                }).ToArray()),
            ReturnTypeName = MapTypeToKeyword(method.ReturnType)
        });
    }

    private static string? ExtractDatasetId(
        AttributeData attributeData,
        IMethodSymbol method,
        Location location,
        List<Diagnostic> diagnostics)
    {
        if (attributeData.ConstructorArguments.Length == 0)
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.InvalidDatasetId, location, "<missing>"));
            return null;
        }

        var arg = attributeData.ConstructorArguments[0];
        if (arg.Value is not string rawId || string.IsNullOrEmpty(rawId))
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.InvalidDatasetId, location,
                arg.Value?.ToString() ?? "<null>"));
            return null;
        }

        var normalized = NormalizeDatasetId(rawId);
        if (normalized is null)
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.InvalidDatasetId, location, rawId));
            return null;
        }

        return normalized;
    }

    private static void ValidateMethodShape(
        IMethodSymbol method,
        string datasetId,
        Location location,
        List<Diagnostic> diagnostics)
    {
        if (method.DeclaredAccessibility != Accessibility.Public)
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.MethodNotPublic, location, FormatQualifiedName(method)));
        }

        if (!method.IsStatic)
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.MethodNotStatic, location, FormatQualifiedName(method)));
        }

        if (method.IsGenericMethod)
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.MethodIsGeneric, location, FormatQualifiedName(method)));
        }

        if (method.ReturnsVoid)
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.MethodReturnsVoid, location, FormatQualifiedName(method)));
        }
        else if (!IsSupportedType(method.ReturnType))
        {
            diagnostics.Add(Diagnostic.Create(
                Diagnostics.UnsupportedReturnType, location,
                FormatQualifiedName(method),
                method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }

        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None)
            {
                diagnostics.Add(Diagnostic.Create(
                    Diagnostics.UnsupportedParameterType, location,
                    parameter.Name, FormatQualifiedName(method),
                    parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
            else if (!IsSupportedType(parameter.Type))
            {
                diagnostics.Add(Diagnostic.Create(
                    Diagnostics.UnsupportedParameterType, location,
                    parameter.Name, FormatQualifiedName(method),
                    parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }
    }

    private static bool IsSupportedType(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None
            && SupportedSpecialTypes.Contains(type.SpecialType))
            return true;

        if (type is INamedTypeSymbol namedType
            && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return false;

        return false;
    }

    private static string MapTypeToKeyword(ITypeSymbol type)
    {
        if (!IsSupportedType(type))
            return type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        return type.SpecialType switch
        {
            SpecialType.System_Boolean => "bool",
            SpecialType.System_Byte => "byte",
            SpecialType.System_SByte => "sbyte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            SpecialType.System_Char => "char",
            SpecialType.System_Decimal => "decimal",
            SpecialType.System_String => "string",
            _ => type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
        };
    }

    private static string? NormalizeDatasetId(string datasetId)
    {
        if (string.IsNullOrEmpty(datasetId))
            return null;

        if (datasetId[0] == '/' || datasetId[datasetId.Length - 1] == '/')
            return null;

        foreach (char c in datasetId)
        {
            if (!IsAllowedIdChar(c))
                return null;
        }

        if (datasetId.Contains("//"))
            return null;

        var segments = datasetId.Split('/');
        foreach (var segment in segments)
        {
            if (segment == "." || segment == "..")
                return null;
        }

        return datasetId.ToLowerInvariant();
    }

    private static bool IsAllowedIdChar(char c)
    {
        return (c >= 'a' && c <= 'z')
            || (c >= 'A' && c <= 'Z')
            || (c >= '0' && c <= '9')
            || c == '-'
            || c == '_'
            || c == '/';
    }

    private static string? ExtractDatasetIdFromPath(string path)
    {
        path = path.Replace('\\', '/');

        const string prefix = "datasets/";
        var prefixIndex = path.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (prefixIndex < 0)
            return null;

        var relative = path.Substring(prefixIndex + prefix.Length);
        var lastSlash = relative.LastIndexOf('/');
        if (lastSlash < 0)
            return null;

        var datasetId = relative.Substring(0, lastSlash);
        if (datasetId.Length == 0)
            return null;

        return NormalizeDatasetId(datasetId);
    }

    private static string FormatQualifiedName(IMethodSymbol method)
    {
        var type = method.ContainingType;
        var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        return $"{typeName}.{method.Name}";
    }

    private static Location GetDiagnosticLocation(
        AttributeData attributeData,
        IMethodSymbol method)
    {
        var syntaxRef = attributeData.ApplicationSyntaxReference;
        if (syntaxRef is not null)
        {
            var syntax = syntaxRef.GetSyntax();
            return syntax.GetLocation();
        }

        return method.Locations.FirstOrDefault() ?? Location.None;
    }

    private static void EmitSource(SourceProductionContext spc, MethodTarget target)
    {
        var source = BuildSource(target);
        var hintName = $"{target.TypeName}_{target.MethodName}_Tests.g.cs";
        spc.AddSource(hintName, source);
    }

    private static string BuildSource(MethodTarget target)
    {
        var caseTypeName = $"{target.TypeName}_{target.MethodName}_Case";
        var testClassName = $"{target.TypeName}_{target.MethodName}_Tests";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using Testing.Runtime;");
        sb.AppendLine();

        if (target.Namespace.Length > 0)
            sb.AppendLine($"namespace {target.Namespace};");
        sb.AppendLine();

        // Case wrapper record
        sb.AppendLine($"public sealed record {caseTypeName}");
        sb.AppendLine("{");
        foreach (var param in target.Parameters)
            sb.AppendLine($"    public required {param.TypeName} {Capitalize(param.Name)} {{ get; init; }}");
        sb.AppendLine("    public required CaseExpect Expect { get; init; }");
        sb.AppendLine("    public required string FilePath { get; init; }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Test class
        sb.AppendLine($"public sealed class {testClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    public static IEnumerable<object[]> Data()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var dir = DatasetLoader.ResolveDirectory(\"{target.DatasetId}\");");
        sb.AppendLine("        var files = DatasetLoader.EnumerateCaseFiles(dir);");
        sb.AppendLine("        foreach (var file in files)");
        sb.AppendLine("        {");
        sb.AppendLine("            var envelope = DatasetLoader.LoadCaseEnvelope(file);");
        sb.AppendLine("            using var inputsDoc = JsonDocument.Parse(envelope.Inputs);");
        sb.AppendLine("            var arr = inputsDoc.RootElement.EnumerateArray().ToArray();");
        sb.AppendLine();
        sb.AppendLine("            yield return new object[]");
        sb.AppendLine("            {");
        sb.AppendLine($"                new {caseTypeName}");
        sb.AppendLine("                {");

        for (int i = 0; i < target.Parameters.Count; i++)
        {
            var param = target.Parameters[i];
            sb.AppendLine($"                    {Capitalize(param.Name)} = JsonSerializer.Deserialize<{param.TypeName}>(arr[{i}].GetRawText())!,");
        }

        sb.AppendLine("                    Expect = envelope.Expect,");
        sb.AppendLine("                    FilePath = file");
        sb.AppendLine("                }");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        var qualifiedName = target.Namespace.Length > 0
            ? $"{target.Namespace}.{target.TypeName}.{target.MethodName}"
            : $"{target.TypeName}.{target.MethodName}";

        var argList = string.Join(", ",
            target.Parameters.Select(p => $"case_.{Capitalize(p.Name)}"));

        sb.AppendLine("    [Theory]");
        sb.AppendLine("    [MemberData(nameof(Data))]");
        sb.AppendLine($"    public void {target.MethodName}({caseTypeName} case_)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (case_.Expect.ExceptionType is not null)");
        sb.AppendLine("        {");
        sb.AppendLine("            AssertionHelper.AssertThrows(");
        sb.AppendLine("                case_.Expect.ExceptionType,");
        sb.AppendLine($"                () => {target.TypeName}.{target.MethodName}({argList}),");
        sb.AppendLine($"                \"{qualifiedName}\",");
        sb.AppendLine("                case_.FilePath);");
        sb.AppendLine("        }");
        sb.AppendLine("        else");
        sb.AppendLine("        {");
        sb.AppendLine("            var actual = AssertionHelper.Invoke(");
        sb.AppendLine($"                () => {target.TypeName}.{target.MethodName}({argList}),");
        sb.AppendLine($"                \"{qualifiedName}\",");
        sb.AppendLine("                case_.FilePath);");
        sb.AppendLine("            AssertionHelper.AssertValue(");
        sb.AppendLine("                case_.Expect.Value!,");
        sb.AppendLine("                actual,");
        sb.AppendLine($"                \"{qualifiedName}\",");
        sb.AppendLine("                case_.FilePath);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string Capitalize(string name) =>
        name.Length > 0 ? char.ToUpper(name[0]) + name.Substring(1) : name;

    private sealed class DiscoveryResult
    {
        public EquatableArray<MethodTarget> Targets { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        public Dictionary<string, Location> TargetLocations { get; }

        public DiscoveryResult(
            List<MethodTarget> targets,
            List<Diagnostic> diagnostics,
            Dictionary<string, Location> targetLocations)
        {
            Targets = new EquatableArray<MethodTarget>(targets.ToArray());
            Diagnostics = diagnostics;
            TargetLocations = targetLocations;
        }

        public static DiscoveryResult Empty { get; } =
            new([], [], new());
    }
}
