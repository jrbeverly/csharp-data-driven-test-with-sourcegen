using Microsoft.CodeAnalysis;

namespace Testing.Generator;

internal static class Diagnostics
{
    private const string Category = "TestGeneration";

    internal static readonly DiagnosticDescriptor MethodNotPublic = new(
        id: "TG001",
        title: "Method must be public",
        messageFormat: "Method '{0}' is not public. Methods annotated with [GenerateTests] must be public.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor MethodNotStatic = new(
        id: "TG002",
        title: "Method must be static",
        messageFormat: "Method '{0}' is not static. Methods annotated with [GenerateTests] must be static.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor MethodIsGeneric = new(
        id: "TG003",
        title: "Method must not be generic",
        messageFormat: "Method '{0}' is generic. Generic methods are not supported by generated testing in v1.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor MethodReturnsVoid = new(
        id: "TG004",
        title: "Method must not return void",
        messageFormat: "Method '{0}' returns void. Methods with [GenerateTests] must return a supported primitive type.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor UnsupportedReturnType = new(
        id: "TG005",
        title: "Unsupported return type",
        messageFormat: "Method '{0}' has unsupported return type '{1}'. In v1, return types must be one of: bool, byte, sbyte, short, ushort, int, uint, long, ulong, char, decimal, string.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor UnsupportedParameterType = new(
        id: "TG006",
        title: "Unsupported parameter type",
        messageFormat: "Parameter '{0}' of method '{1}' has unsupported type '{2}'. In v1, parameter types must be one of: bool, byte, sbyte, short, ushort, int, uint, long, ulong, char, decimal, string.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor DuplicateDatasetId = new(
        id: "TG007",
        title: "Duplicate dataset ID",
        messageFormat: "Dataset ID '{0}' is used by multiple methods in this test assembly. Each dataset ID must be unique. Already used by method '{1}'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidDatasetId = new(
        id: "TG008",
        title: "Invalid dataset ID",
        messageFormat: "Dataset ID '{0}' is not valid. Dataset IDs must use only letters, digits, '-', '_', and '/', and must not start or end with '/', contain '//', or contain '.' or '..' segments.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor NoDatasetFiles = new(
        id: "TG009",
        title: "No dataset files found",
        messageFormat: "No dataset files found for dataset ID '{0}'. Expected a non-empty directory at 'datasets/{0}/' under the test project.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
