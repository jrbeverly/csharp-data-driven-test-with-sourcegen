namespace Testing.Runtime;

public sealed record CaseExpect
{
    public string? Value { get; init; }
    public string? ExceptionType { get; init; }
}
