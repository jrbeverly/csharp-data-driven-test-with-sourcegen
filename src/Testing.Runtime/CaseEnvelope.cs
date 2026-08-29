namespace Testing.Runtime;

public sealed record CaseEnvelope
{
    public required string FilePath { get; init; }
    public string? Description { get; init; }
    public required string Inputs { get; init; }
    public required CaseExpect Expect { get; init; }
}
