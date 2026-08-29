using System.Text.Json;
using Testing.Abstractions;
using Testing.Runtime;

namespace Sample.Library.Tests;

public class DatasetLoaderTests
{
    // ResolveDirectory

    [Fact]
    public void ResolveDirectory_ValidId_ReturnsExpectedPath()
    {
        var result = DatasetLoader.ResolveDirectory("sample-math/add");

        Assert.EndsWith(
            Path.Combine("datasets", "sample-math", "add"),
            result);
    }

    [Fact]
    public void ResolveDirectory_ValidId_StartsWithBaseDirectory()
    {
        var result = DatasetLoader.ResolveDirectory("my-dataset");

        Assert.StartsWith(AppContext.BaseDirectory, result);
    }

    [Fact]
    public void ResolveDirectory_UpperCaseId_NormalizesToLower()
    {
        var result = DatasetLoader.ResolveDirectory("SAMPLE-MATH/ADD");

        Assert.EndsWith(
            Path.Combine("datasets", "sample-math", "add"),
            result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/starts-with-slash")]
    [InlineData("has space")]
    public void ResolveDirectory_InvalidId_ThrowsArgumentException(string? id)
    {
        var ex = Assert.Throws<ArgumentException>(() => DatasetLoader.ResolveDirectory(id!));
        Assert.Contains(id ?? "", ex.Message);
    }

    // EnumerateCaseFiles

    [Fact]
    public void EnumerateCaseFiles_ReturnsJsonFilesInOrdinalOrder()
    {
        var dir = CreateTempDir();
        try
        {
            WriteFile(dir, "c.json", "{}");
            WriteFile(dir, "a.json", "{}");
            WriteFile(dir, "b.json", "{}");
            WriteFile(dir, "readme.txt", "not json");

            var files = DatasetLoader.EnumerateCaseFiles(dir);

            Assert.Equal(3, files.Count);
            Assert.EndsWith("a.json", files[0]);
            Assert.EndsWith("b.json", files[1]);
            Assert.EndsWith("c.json", files[2]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void EnumerateCaseFiles_EmptyDirectory_ReturnsEmptyList()
    {
        var dir = CreateTempDir();
        try
        {
            var files = DatasetLoader.EnumerateCaseFiles(dir);
            Assert.Empty(files);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void EnumerateCaseFiles_DirectoryNotFound_Throws()
    {
        var ex = Assert.Throws<DirectoryNotFoundException>(
            () => DatasetLoader.EnumerateCaseFiles("/nonexistent/path"));
        Assert.Contains("/nonexistent/path", ex.Message);
    }

    // LoadCaseEnvelope - success cases

    [Fact]
    public void LoadCaseEnvelope_SuccessCase_ReturnsEnvelope()
    {
        var file = CreateTempFile("""
        {
          "description": "Add two positive numbers",
          "inputs": [2, 3],
          "expect": {
            "value": 5
          }
        }
        """);

        try
        {
            var result = DatasetLoader.LoadCaseEnvelope(file);

            Assert.Equal(file, result.FilePath);
            Assert.Equal("Add two positive numbers", result.Description);

            using var inputsDoc = JsonDocument.Parse(result.Inputs);
            var inputs = inputsDoc.RootElement;
            Assert.Equal(JsonValueKind.Array, inputs.ValueKind);
            Assert.Equal(2, inputs.GetArrayLength());

            Assert.NotNull(result.Expect.Value);
            Assert.Equal("5", result.Expect.Value);
            Assert.Null(result.Expect.ExceptionType);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void LoadCaseEnvelope_ExceptionCase_ReturnsEnvelope()
    {
        var file = CreateTempFile("""
        {
          "description": "Rejects invalid range",
          "inputs": [10, 0],
          "expect": {
            "exception": "System.ArgumentException"
          }
        }
        """);

        try
        {
            var result = DatasetLoader.LoadCaseEnvelope(file);

            Assert.Equal(file, result.FilePath);
            Assert.Equal("Rejects invalid range", result.Description);
            Assert.Equal("System.ArgumentException", result.Expect.ExceptionType);
            Assert.Null(result.Expect.Value);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void LoadCaseEnvelope_NoDescription_ReturnsNullDescription()
    {
        var file = CreateTempFile("""
        {
          "inputs": [1],
          "expect": {
            "value": 2
          }
        }
        """);

        try
        {
            var result = DatasetLoader.LoadCaseEnvelope(file);
            Assert.Null(result.Description);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void LoadCaseEnvelope_StringInputs_PreservesRawJson()
    {
        var file = CreateTempFile("""
        {
          "inputs": ["hello", "world"],
          "expect": {
            "value": "helloworld"
          }
        }
        """);

        try
        {
            var result = DatasetLoader.LoadCaseEnvelope(file);

            using var inputsDoc = JsonDocument.Parse(result.Inputs);
            var arr = inputsDoc.RootElement.EnumerateArray().ToArray();
            Assert.Equal("hello", arr[0].GetString());
            Assert.Equal("world", arr[1].GetString());
            Assert.Equal("\"helloworld\"", result.Expect.Value);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void LoadCaseEnvelope_NullValue_PreservesNull()
    {
        var file = CreateTempFile("""
        {
          "inputs": [null],
          "expect": {
            "value": null
          }
        }
        """);

        try
        {
            var result = DatasetLoader.LoadCaseEnvelope(file);

            Assert.NotNull(result.Expect.Value);
            Assert.Equal("null", result.Expect.Value);
        }
        finally
        {
            File.Delete(file);
        }
    }

    // LoadCaseEnvelope - failure cases

    [Fact]
    public void LoadCaseEnvelope_FileNotFound_ThrowsWithPath()
    {
        var path = "/nonexistent/file.json";
        var ex = Assert.Throws<DatasetLoadException>(() => DatasetLoader.LoadCaseEnvelope(path));
        Assert.Contains(path, ex.Message);
        Assert.Equal(path, ex.FilePath);
    }

    [Fact]
    public void LoadCaseEnvelope_InvalidJson_ThrowsWithPath()
    {
        var file = CreateTempFile("not valid json {{{");

        try
        {
            var ex = Assert.Throws<DatasetLoadException>(() => DatasetLoader.LoadCaseEnvelope(file));
            Assert.Contains(file, ex.Message);
            Assert.Equal(file, ex.FilePath);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void LoadCaseEnvelope_RootNotObject_ThrowsWithPath()
    {
        var file = CreateTempFile("[1, 2, 3]");

        try
        {
            var ex = Assert.Throws<DatasetLoadException>(() => DatasetLoader.LoadCaseEnvelope(file));
            Assert.Contains(file, ex.Message);
            Assert.Contains("object", ex.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void LoadCaseEnvelope_MissingInputs_ThrowsWithPath()
    {
        var file = CreateTempFile("""
        {
          "expect": {
            "value": 5
          }
        }
        """);

        try
        {
            var ex = Assert.Throws<DatasetLoadException>(() => DatasetLoader.LoadCaseEnvelope(file));
            Assert.Contains(file, ex.Message);
            Assert.Contains("inputs", ex.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void LoadCaseEnvelope_InputsNotArray_ThrowsWithPath()
    {
        var file = CreateTempFile("""
        {
          "inputs": "not-an-array",
          "expect": {
            "value": 5
          }
        }
        """);

        try
        {
            var ex = Assert.Throws<DatasetLoadException>(() => DatasetLoader.LoadCaseEnvelope(file));
            Assert.Contains(file, ex.Message);
            Assert.Contains("array", ex.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void LoadCaseEnvelope_MissingExpect_ThrowsWithPath()
    {
        var file = CreateTempFile("""
        {
          "inputs": [1, 2]
        }
        """);

        try
        {
            var ex = Assert.Throws<DatasetLoadException>(() => DatasetLoader.LoadCaseEnvelope(file));
            Assert.Contains(file, ex.Message);
            Assert.Contains("expect", ex.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void LoadCaseEnvelope_ExpectNotObject_ThrowsWithPath()
    {
        var file = CreateTempFile("""
        {
          "inputs": [1, 2],
          "expect": 42
        }
        """);

        try
        {
            var ex = Assert.Throws<DatasetLoadException>(() => DatasetLoader.LoadCaseEnvelope(file));
            Assert.Contains(file, ex.Message);
            Assert.Contains("object", ex.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void LoadCaseEnvelope_BothValueAndException_ThrowsWithPath()
    {
        var file = CreateTempFile("""
        {
          "inputs": [1, 2],
          "expect": {
            "value": 5,
            "exception": "System.ArgumentException"
          }
        }
        """);

        try
        {
            var ex = Assert.Throws<DatasetLoadException>(() => DatasetLoader.LoadCaseEnvelope(file));
            Assert.Contains(file, ex.Message);
            Assert.Contains("Exactly one", ex.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void LoadCaseEnvelope_NeitherValueNorException_ThrowsWithPath()
    {
        var file = CreateTempFile("""
        {
          "inputs": [1, 2],
          "expect": {
          }
        }
        """);

        try
        {
            var ex = Assert.Throws<DatasetLoadException>(() => DatasetLoader.LoadCaseEnvelope(file));
            Assert.Contains(file, ex.Message);
            Assert.Contains("Exactly one", ex.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void LoadCaseEnvelope_EmptyException_ThrowsWithPath()
    {
        var file = CreateTempFile("""
        {
          "inputs": [1, 2],
          "expect": {
            "exception": ""
          }
        }
        """);

        try
        {
            var ex = Assert.Throws<DatasetLoadException>(() => DatasetLoader.LoadCaseEnvelope(file));
            Assert.Contains(file, ex.Message);
            Assert.Contains("empty", ex.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    // Helpers

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }

    private static void WriteFile(string directory, string fileName, string content)
    {
        File.WriteAllText(Path.Combine(directory, fileName), content);
    }
}
