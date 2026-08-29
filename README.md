# Data-Driven Testing with C# Source Generators

Generates xUnit theory scaffolding from annotated deterministic methods and JSON datasets.

```csharp
[GenerateTests("sample-math/clamp")]
public static int Clamp(int value, int min, int max)
```

One case file, `datasets/sample-math/clamp/value-above-max.json`:

```json
{ "description": "Clamps a value above the maximum", "inputs": [15, 0, 10], "expect": { "value": 10 } }
```

Generated `MathUtils_Clamp_Tests.g.cs`:

```csharp
public sealed record MathUtils_Clamp_Case
{
    public required int Value { get; init; }
    public required int Min { get; init; }
    public required int Max { get; init; }
    public required CaseExpect Expect { get; init; }
    public required string FilePath { get; init; }
}

public sealed class MathUtils_Clamp_Tests
{
    [Theory]
    [MemberData(nameof(Data))]
    public void Clamp(MathUtils_Clamp_Case case_)
    {
        // ...
        var actual = AssertionHelper.Invoke(
            () => MathUtils.Clamp(case_.Value, case_.Min, case_.Max),
            "Sample.Library.MathUtils.Clamp",
            case_.FilePath);
        AssertionHelper.AssertValue(
            case_.Expect.Value!,
            actual,
            "Sample.Library.MathUtils.Clamp",
            case_.FilePath);
    }
}
```

```sh
make build
make test
```

## Notes

- `[GenerateTests("dataset-id")]` binds a method to a dataset directory through a stable logical identifier.
- The generator accepts a deliberately narrow set of public static methods with primitive parameters and return values.
- Dataset files are `AdditionalFiles` during compilation and copied content during test execution.
- Build diagnostics cover invalid method shapes and dataset wiring; runtime failures cover malformed cases and incorrect results.
