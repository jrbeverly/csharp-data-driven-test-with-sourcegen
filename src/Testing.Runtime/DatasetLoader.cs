using System.Text.Json;
using Testing.Abstractions;

namespace Testing.Runtime;

public static class DatasetLoader
{
    public static string ResolveDirectory(string datasetId)
    {
        var normalized = DatasetId.Normalize(datasetId);
        if (normalized is null)
            throw new ArgumentException(
                $"Dataset ID '{datasetId}' is not a valid dataset identifier.", nameof(datasetId));

        return Path.Combine(AppContext.BaseDirectory, "datasets", normalized);
    }

    public static IReadOnlyList<string> EnumerateCaseFiles(string datasetDirectory)
    {
        if (!Directory.Exists(datasetDirectory))
            throw new DirectoryNotFoundException(
                $"Dataset directory not found: '{datasetDirectory}'.");

        var files = Directory.GetFiles(datasetDirectory, "*.json");
        Array.Sort(files, StringComparer.Ordinal);
        return files;
    }

    public static CaseEnvelope LoadCaseEnvelope(string filePath)
    {
        var json = ReadFile(filePath);
        using var doc = ParseJson(json, filePath);
        return BuildEnvelope(doc.RootElement, filePath);
    }

    private static string ReadFile(string filePath)
    {
        try
        {
            return File.ReadAllText(filePath);
        }
        catch (FileNotFoundException)
        {
            throw new DatasetLoadException(
                $"Dataset file not found: '{filePath}'.", filePath);
        }
        catch (DirectoryNotFoundException)
        {
            throw new DatasetLoadException(
                $"Dataset file not found: '{filePath}'.", filePath);
        }
    }

    private static JsonDocument ParseJson(string json, string filePath)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new DatasetLoadException(
                $"Dataset file '{filePath}' contains invalid JSON: {ex.Message}", filePath, ex);
        }
    }

    private static CaseEnvelope BuildEnvelope(JsonElement root, string filePath)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new DatasetLoadException(
                $"Dataset file '{filePath}' must contain a JSON object at the root.", filePath);

        var description = ReadDescription(root);
        var inputs = ReadInputs(root, filePath);
        var expect = ReadExpect(root, filePath);

        return new CaseEnvelope
        {
            FilePath = filePath,
            Description = description,
            Inputs = inputs,
            Expect = expect
        };
    }

    private static string? ReadDescription(JsonElement root)
    {
        if (root.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String)
            return desc.GetString();

        return null;
    }

    private static string ReadInputs(JsonElement root, string filePath)
    {
        if (!root.TryGetProperty("inputs", out var inputs))
            throw new DatasetLoadException(
                $"Dataset file '{filePath}' is missing the required 'inputs' property.", filePath);

        if (inputs.ValueKind != JsonValueKind.Array)
            throw new DatasetLoadException(
                $"Dataset file '{filePath}' has an 'inputs' property that is not an array.", filePath);

        return inputs.GetRawText();
    }

    private static CaseExpect ReadExpect(JsonElement root, string filePath)
    {
        if (!root.TryGetProperty("expect", out var expect))
            throw new DatasetLoadException(
                $"Dataset file '{filePath}' is missing the required 'expect' property.", filePath);

        if (expect.ValueKind != JsonValueKind.Object)
            throw new DatasetLoadException(
                $"Dataset file '{filePath}' has an 'expect' property that is not an object.", filePath);

        var hasValue = expect.TryGetProperty("value", out var valueElement);
        var hasException = expect.TryGetProperty("exception", out var exceptionElement);

        if (hasValue && hasException)
            throw new DatasetLoadException(
                $"Dataset file '{filePath}' has both 'value' and 'exception' in 'expect'. Exactly one is required.", filePath);

        if (!hasValue && !hasException)
            throw new DatasetLoadException(
                $"Dataset file '{filePath}' has neither 'value' nor 'exception' in 'expect'. Exactly one is required.", filePath);

        if (hasException)
        {
            var exceptionType = exceptionElement.GetString();
            if (string.IsNullOrWhiteSpace(exceptionType))
                throw new DatasetLoadException(
                    $"Dataset file '{filePath}' has an empty 'exception' value. A CLR type name is required.", filePath);

            return new CaseExpect { ExceptionType = exceptionType };
        }

        return new CaseExpect { Value = valueElement.GetRawText() };
    }
}
