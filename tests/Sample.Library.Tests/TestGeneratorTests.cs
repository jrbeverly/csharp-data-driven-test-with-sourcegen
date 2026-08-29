using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Sample.Library.Tests;

public class TestGeneratorTests
{
    private const string AttributeSource = """
        using System;

        namespace Testing.Abstractions;

        [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
        public sealed class GenerateTestsAttribute : Attribute
        {
            public string DatasetId { get; }
            public GenerateTestsAttribute(string datasetId) => DatasetId = datasetId;
        }
        """;

    [Fact]
    public void ValidMethodWithDatasetFile_ProducesNoDiagnostics()
    {
        var source = """
            namespace TestLib;

            public static class Calculator
            {
                [Testing.Abstractions.GenerateTests("calc/add")]
                public static int Add(int a, int b) => a + b;
            }
            """;

        var result = RunGenerator(AttributeSource, source,
            datasetFiles: [("datasets/calc/add/case1.json", "")]);

        Assert.Empty(result);
    }

    [Fact]
    public void ValidMethodWithoutDatasetFiles_ReportsTG009()
    {
        var source = """
            namespace TestLib;

            public static class Calculator
            {
                [Testing.Abstractions.GenerateTests("calc/add")]
                public static int Add(int a, int b) => a + b;
            }
            """;

        var result = RunGenerator(AttributeSource, source);

        Assert.Contains(result, d => d.Id == "TG009"
            && d.GetMessage().Contains("calc/add"));
    }

    [Fact]
    public void ValidMethod_DatasetIdCaseInsensitiveMatch_ProducesNoDiagnostics()
    {
        var source = """
            namespace TestLib;

            public static class Calculator
            {
                [Testing.Abstractions.GenerateTests("Calc/Add")]
                public static int Add(int a, int b) => a + b;
            }
            """;

        var result = RunGenerator(AttributeSource, source,
            datasetFiles: [("datasets/CALC/ADD/case1.json", "")]);

        Assert.Empty(result);
    }

    [Fact]
    public void ValidMethod_DatasetFileOutsideDatasetsPrefix_Ignored()
    {
        var source = """
            namespace TestLib;

            public static class Calculator
            {
                [Testing.Abstractions.GenerateTests("calc/add")]
                public static int Add(int a, int b) => a + b;
            }
            """;

        var result = RunGenerator(AttributeSource, source,
            datasetFiles: [("other/calc/add/case1.json", "")]);

        Assert.Contains(result, d => d.Id == "TG009");
    }

    [Fact]
    public void ValidMethod_DatasetFileDirectlyInDatasets_Ignored()
    {
        var source = """
            namespace TestLib;

            public static class Calculator
            {
                [Testing.Abstractions.GenerateTests("calc/add")]
                public static int Add(int a, int b) => a + b;
            }
            """;

        var result = RunGenerator(AttributeSource, source,
            datasetFiles: [("datasets/not-in-subdir.json", "")]);

        Assert.Contains(result, d => d.Id == "TG009");
    }

    [Fact]
    public void ValidMethod_PathWithBackslashes_MatchesDatasetId()
    {
        var source = """
            namespace TestLib;

            public static class Calculator
            {
                [Testing.Abstractions.GenerateTests("calc/add")]
                public static int Add(int a, int b) => a + b;
            }
            """;

        var result = RunGenerator(AttributeSource, source,
            datasetFiles: [("datasets\\calc\\add\\case1.json", "")]);

        Assert.Empty(result);
    }

    [Fact]
    public void NonPublicMethod_ReportsTG001()
    {
        var source = """
            namespace TestLib;

            public static class Calculator
            {
                [Testing.Abstractions.GenerateTests("calc/add")]
                private static int Add(int a, int b) => a + b;
            }
            """;

        var result = RunGenerator(AttributeSource, source);

        Assert.Contains(result, d => d.Id == "TG001"
            && d.GetMessage().Contains("not public"));
    }

    [Fact]
    public void InstanceMethod_ReportsTG002()
    {
        var source = """
            namespace TestLib;

            public class Calculator
            {
                [Testing.Abstractions.GenerateTests("calc/add")]
                public int Add(int a, int b) => a + b;
            }
            """;

        var result = RunGenerator(AttributeSource, source);

        Assert.Contains(result, d => d.Id == "TG002"
            && d.GetMessage().Contains("not static"));
    }

    [Fact]
    public void GenericMethod_ReportsTG003()
    {
        var source = """
            namespace TestLib;

            public static class Calculator
            {
                [Testing.Abstractions.GenerateTests("calc/identity")]
                public static T Identity<T>(T value) => value;
            }
            """;

        var result = RunGenerator(AttributeSource, source);

        Assert.Contains(result, d => d.Id == "TG003"
            && d.GetMessage().Contains("generic"));
    }

    [Fact]
    public void VoidMethod_ReportsTG004()
    {
        var source = """
            namespace TestLib;

            public static class Logger
            {
                [Testing.Abstractions.GenerateTests("log/write")]
                public static void Write(string message) { }
            }
            """;

        var result = RunGenerator(AttributeSource, source);

        Assert.Contains(result, d => d.Id == "TG004"
            && d.GetMessage().Contains("void"));
    }

    [Fact]
    public void UnsupportedReturnType_ReportsTG005()
    {
        var source = """
            using System;

            namespace TestLib;

            public static class Clock
            {
                [Testing.Abstractions.GenerateTests("clock/now")]
                public static DateTime GetNow() => DateTime.UtcNow;
            }
            """;

        var result = RunGenerator(AttributeSource, source);

        Assert.Contains(result, d => d.Id == "TG005"
            && d.GetMessage().Contains("DateTime"));
    }

    [Fact]
    public void UnsupportedParameterType_ReportsTG006()
    {
        var source = """
            using System;

            namespace TestLib;

            public static class Formatter
            {
                [Testing.Abstractions.GenerateTests("fmt/date")]
                public static string FormatDate(DateTime date) => date.ToString();
            }
            """;

        var result = RunGenerator(AttributeSource, source);

        Assert.Contains(result, d => d.Id == "TG006"
            && d.GetMessage().Contains("date"));
    }

    [Fact]
    public void NullableValueType_ReportsTG006()
    {
        var source = """
            namespace TestLib;

            public static class Counter
            {
                [Testing.Abstractions.GenerateTests("counter/get")]
                public static int? GetValue(int? input) => input;
            }
            """;

        var result = RunGenerator(AttributeSource, source);

        Assert.Contains(result, d => d.Id == "TG006");
    }

    [Fact]
    public void InvalidDatasetId_ReportsTG008()
    {
        var source = """
            namespace TestLib;

            public static class Calc
            {
                [Testing.Abstractions.GenerateTests("has space")]
                public static int Add(int a, int b) => a + b;
            }
            """;

        var result = RunGenerator(AttributeSource, source);

        Assert.Contains(result, d => d.Id == "TG008"
            && d.GetMessage().Contains("has space"));
    }

    [Fact]
    public void DuplicateDatasetId_ReportsTG007()
    {
        var source = """
            namespace TestLib;

            public static class CalcA
            {
                [Testing.Abstractions.GenerateTests("calc/math")]
                public static int Add(int a, int b) => a + b;
            }

            public static class CalcB
            {
                [Testing.Abstractions.GenerateTests("calc/math")]
                public static int Subtract(int a, int b) => a - b;
            }
            """;

        var result = RunGenerator(AttributeSource, source);

        Assert.Contains(result, d => d.Id == "TG007"
            && d.GetMessage().Contains("calc/math"));
    }

    [Fact]
    public void MultipleViolations_ReportsAllDiagnostics()
    {
        var source = """
            namespace TestLib;

            public class Calc
            {
                [Testing.Abstractions.GenerateTests("calc/bad")]
                public System.DateTime DoWork(int x) => System.DateTime.UtcNow;
            }
            """;

        var result = RunGenerator(AttributeSource, source);

        Assert.Contains(result, d => d.Id == "TG002"); // not static
        Assert.Contains(result, d => d.Id == "TG005"); // unsupported return
    }

    [Fact]
    public void AllSupportedPrimitiveTypes_WithDatasetFiles_ProduceNoDiagnostics()
    {
        var source = """
            namespace TestLib;

            public static class Primitives
            {
                [Testing.Abstractions.GenerateTests("p/bool")]
                public static bool Tb(bool x) => x;

                [Testing.Abstractions.GenerateTests("p/byte")]
                public static byte Tbyte(byte x) => x;

                [Testing.Abstractions.GenerateTests("p/sbyte")]
                public static sbyte Tsbyte(sbyte x) => x;

                [Testing.Abstractions.GenerateTests("p/short")]
                public static short Tshort(short x) => x;

                [Testing.Abstractions.GenerateTests("p/ushort")]
                public static ushort Tushort(ushort x) => x;

                [Testing.Abstractions.GenerateTests("p/int")]
                public static int Tint(int x) => x;

                [Testing.Abstractions.GenerateTests("p/uint")]
                public static uint Tuint(uint x) => x;

                [Testing.Abstractions.GenerateTests("p/long")]
                public static long Tlong(long x) => x;

                [Testing.Abstractions.GenerateTests("p/ulong")]
                public static ulong Tulong(ulong x) => x;

                [Testing.Abstractions.GenerateTests("p/char")]
                public static char Tchar(char x) => x;

                [Testing.Abstractions.GenerateTests("p/decimal")]
                public static decimal Tdecimal(decimal x) => x;

                [Testing.Abstractions.GenerateTests("p/string")]
                public static string Tstring(string x) => x;
            }
            """;

        var files = new (string, string)[]
        {
            ("datasets/p/bool/case1.json", ""),
            ("datasets/p/byte/case1.json", ""),
            ("datasets/p/sbyte/case1.json", ""),
            ("datasets/p/short/case1.json", ""),
            ("datasets/p/ushort/case1.json", ""),
            ("datasets/p/int/case1.json", ""),
            ("datasets/p/uint/case1.json", ""),
            ("datasets/p/long/case1.json", ""),
            ("datasets/p/ulong/case1.json", ""),
            ("datasets/p/char/case1.json", ""),
            ("datasets/p/decimal/case1.json", ""),
            ("datasets/p/string/case1.json", ""),
        };

        var result = RunGenerator(AttributeSource, source, datasetFiles: files);

        Assert.Empty(result);
    }

    [Fact]
    public void GeneratorSeesDiagnostics_Canary()
    {
        var source = """
            namespace TestLib;

            public static class Calc
            {
                [Testing.Abstractions.GenerateTests("calc/add")]
                public static int Add(int a, int b) => a + b;

                [Testing.Abstractions.GenerateTests("calc/bad")]
                public System.DateTime DoWork(int x) => System.DateTime.UtcNow;
            }
            """;

        var result = RunGenerator(AttributeSource, source,
            datasetFiles: [("datasets/calc/add/case1.json", "")]);

        Assert.Contains(result, d => d.Id == "TG005");
        Assert.DoesNotContain(result, d => d.Id == "TG001");
        Assert.DoesNotContain(result, d => d.Id == "TG009");
    }

    private static ImmutableArray<Diagnostic> RunGenerator(
        string attributeSource,
        string testSource,
        (string Path, string Content)[]? datasetFiles = null)
    {
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(attributeSource),
            CSharpSyntaxTree.ParseText(testSource)
        };

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(
                Path.Combine(
                    Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                    "System.Runtime.dll"))
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new Testing.Generator.TestGenerator();
        var additionalTexts = datasetFiles is not null
            ? datasetFiles.Select(f => (AdditionalText)new TestAdditionalText(f.Path, f.Content)).ToArray()
            : [];

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            additionalTexts: additionalTexts);

        driver = driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();
        return runResult.Diagnostics;
    }

    private sealed class TestAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public override string Path { get; }

        public TestAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content);
        }

        public override SourceText GetText(CancellationToken ct = default) => _text;
    }
}
