# Vision

Build a C# source-generation-based testing system where deterministic function testing becomes largely automatic inside a dedicated test assembly.

The core idea is to treat tests as data and semantics rather than handwritten imperative scaffolding.

## Desired Developer Workflow

The ideal workflow is:

1. Write a deterministic method.
2. Annotate it with metadata such as `[Deterministic]` or `[GenerateTests]`.
3. Provide structured JSON datasets in a directory associated with the generated test.
4. Allow source generators to synthesize the xUnit-based testing infrastructure automatically.

Developers should primarily maintain:

- The implementation
- Input/output datasets
- Semantic constraints and metadata

Everything else should be generated.

## Generated Testing Infrastructure

The generated system should automatically create:

- Test classes
- Test methods
- xUnit theory scaffolding
- Dataset loading infrastructure
- Yield-based dataset providers
- Assertions
- Boilerplate plumbing

The goal is to eliminate repetitive handwritten test scaffolding.

The generator should not pre-expand every JSON file into concrete compile-time test cases. Instead, it should generate the scaffolding that locates dataset files, deserializes them, and yields rows into the test framework during test execution.

## Semantic Test Synthesis

The testing system should reason about functions semantically.

Instead of manually specifying every test, developers should declare properties such as:

- Deterministic behavior
- Allowed ranges
- Nullability
- Expected exceptions
- Input constraints
- Dataset expectations

The system should infer appropriate mechanical testing strategies from that metadata where practical.

## Automatic Edge-Case Exploration

The system should progressively evolve beyond simple input/output datasets.

Eventually, the generator may support synthesis of:

- Boundary tests
- Invalid input tests
- Nullability tests
- Overflow tests
- Range exploration tests
- Fuzz tests

These capabilities are secondary to the dataset-driven scaffolding model and should be introduced only after the core workflow is solid.

If fuzz-style exploration is added, non-deterministic exploration is acceptable.

## Dataset-Centric Testing

Datasets should become the primary representation of expected behavior.

Rather than writing imperative tests, developers should define:

- Inputs
- Expected outputs
- Constraints
- Expected failures
- Semantic behaviors

The infrastructure should mechanically derive executable tests and dataset providers from those definitions.

The JSON remains part of the actual test invocation flow. The generated code exists to expose that data cleanly to the test framework.

## Compile-Time Testing Model

The source generator should function as a compile-time test synthesis engine.

During compilation, the system should:

- Discover deterministic methods
- Analyze signatures and metadata
- Generate executable tests
- Generate dataset-loading infrastructure
- Generate edge-case coverage scaffolding where appropriate

During test execution, the generated providers should:

- Locate dataset files
- Deserialize dataset content
- Yield rows into the theory-based test framework

The final generated tests should behave like normal compiled tests inside the standard test ecosystem.

## Long-Term Direction

The long-term direction is a semantic, data-driven testing architecture where:

- Tests are declarative
- Coverage generation is systematic
- Boilerplate is minimized
- Edge cases are inferred where feasible
- Deterministic utility testing becomes nearly automatic

The result should significantly reduce repetitive testing work while improving consistency, maintainability, and coverage quality.
