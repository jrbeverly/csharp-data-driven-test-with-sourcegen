# Technical

## Language and Platform

The implementation should be built in C#.

The system should heavily leverage C# source generators for compile-time code synthesis.

The initial generated test host should use xUnit, as its theory-based data model is a strong fit for generated dataset providers and yield-based test data exposure.

Generated tests must live in a separate dedicated test assembly. Production code and generated test code should not be mixed in the same assembly.

## Initial Development Workflow

The project should begin with:

1. A small deterministic utility library
2. Traditional handwritten tests
3. Validation that the testing workflow operates correctly

Only after validating the baseline workflow should the system evolve into generated infrastructure.

That generated infrastructure should be introduced inside the dedicated test assembly rather than inside the production assembly.

## Deterministic Function Detection

The system should identify deterministic or pure methods for automatic test generation.

Potential approaches include:

- Explicit attributes such as:
  - `[Deterministic]`
  - `[GenerateTests]`
- Static analysis heuristics
- Signature inspection
- Configuration-driven discovery

The initial implementation should prioritize explicit attribute-based discovery and explicit opt-in over heuristic purity detection.

## Supported Method Characteristics

The generated testing system should target methods that are:

- Deterministic
- Hermetic
- Pure or effectively pure
- Operating on primitive input/output types

The first implementation should assume a deliberately narrow surface area:

- Primitive parameters
- Primitive return values
- Primitive exception scenarios
- Preferably static utility-style methods

The broader problem space is especially suited for:

- Math functions
- Parsing
- String manipulation
- Serialization
- Validation logic
- Utility operations
- Small algorithmic functions

However, the MVP should stay narrower than that full long-term space until the scaffolding model is proven.

## Dataset-Driven Testing

Test cases should be externally defined using structured datasets.

The initial implementation should assume JSON datasets organized under directories associated with generated tests.

Future formats may include:

- CSV
- Other structured tabular or object formats

Datasets should define:

- Inputs
- Expected outputs
- Potentially expected exceptions
- Constraint metadata

The JSON files remain part of the runtime test invocation flow.

The generator is expected to create the infrastructure that points at dataset directories, enumerates files, deserializes them, and exposes rows through a yield-based mechanism compatible with the test framework.

## Generated Infrastructure

Source generators should synthesize:

- Test classes
- Test methods
- Theory-compatible dataset provider members or classes
- Dataset directory enumeration and deserialization plumbing
- Assertion scaffolding
- Boilerplate plumbing

Developers should not manually create repetitive test harness infrastructure.

## Parameterized Test Integration

The generated tests should integrate with parameterized testing patterns.

The initial implementation should target xUnit theories.

Likely mechanisms include:

- `[Theory]`
- `MemberData` or equivalent framework-native data hooks
- `yield return`
- Generated enumerable dataset rows

The important distinction is that the source generator should produce scaffolding and providers, not fully materialized concrete test cases for every dataset file at compile time.

## Advanced Generated Testing

The system should eventually support automatic generation of:

- Fuzz tests
- Boundary tests
- Exception tests
- Invalid input tests
- Nullability tests
- Overflow tests
- Range exploration tests

These should be treated as future expansion areas rather than MVP requirements.

If fuzz-style exploration is introduced, non-deterministic exploration is acceptable.

## Semantic Inference

The source generator should derive testing strategies from:

- Method signatures
- Parameter types
- Nullability metadata
- Attributes
- Constraints
- Configuration rules

Examples:

- Numeric parameters imply boundary exploration.
- Nullable parameters imply null-path testing.
- Declared ranges imply fuzz exploration ranges.
- Declared exceptions imply invalid-state tests.

The generator should infer only mechanical testing strategies from that metadata. It should not be expected to infer full business correctness or rich semantic properties automatically.

## Compile-Time Synthesis

The source generator should operate as a compile-time test synthesis engine.

Generation responsibilities include:

- Discovery
- Analysis
- Dataset provider synthesis
- Test synthesis
- Assertion generation
- Build-time diagnostics

Runtime responsibilities should still include:

- Enumerating dataset files
- Deserializing dataset content
- Yielding rows into the test framework

The generated output should become normal compiled test code.

## Architectural Direction

Tests should be modelled as declarative semantic metadata rather than imperative handwritten logic.

The architecture should prioritize:

- Mechanical generation
- Semantic inference
- Structured metadata
- Runtime dataset loading through generated providers
- Consistency
- Repeatability
- Coverage synthesis

Over manual test authoring patterns.
