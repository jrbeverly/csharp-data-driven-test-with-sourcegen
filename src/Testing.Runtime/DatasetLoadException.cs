namespace Testing.Runtime;

public sealed class DatasetLoadException : Exception
{
    public string FilePath { get; }

    public DatasetLoadException(string message, string filePath, Exception? inner = null)
        : base(message, inner)
    {
        FilePath = filePath;
    }
}
