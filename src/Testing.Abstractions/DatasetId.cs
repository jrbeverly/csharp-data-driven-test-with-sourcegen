using System;

namespace Testing.Abstractions;

public static class DatasetId
{
    public static string? Normalize(string datasetId)
    {
        if (string.IsNullOrEmpty(datasetId))
            return null;

        if (datasetId[0] == '/' || datasetId[datasetId.Length - 1] == '/')
            return null;

        foreach (char c in datasetId)
        {
            if (!IsAllowedChar(c))
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

    private static bool IsAllowedChar(char c)
    {
        return (c >= 'a' && c <= 'z')
            || (c >= 'A' && c <= 'Z')
            || (c >= '0' && c <= '9')
            || c == '-'
            || c == '_'
            || c == '/';
    }
}
