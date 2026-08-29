# High-Level Design

## Status

This document is the definitive architectural and implementation-direction document for the project.

It supersedes `MOCK-DESIGN.md` as the authoritative description of how the system should be built.

## Objective

Build a C# testing system that uses source generation to synthesize production-style xUnit test scaffolding for deterministic utility methods, while keeping the actual datasets in JSON files that remain part of runtime test execution.

The system is intended to remove handwritten test boilerplate, not to invent behavioral truth. Behavioral truth continues to come from the implementation contract and the datasets.

## Final Design Summary

The implementation direction is:

- Product code opts methods into generation with a single explicit attribute.
- Generated tests live only in dedicated test assemblies.
- The source generator runs at compile time and emits xUnit theory scaffolding plus dataset-provider code.
- JSON dataset files are not expanded into compile-time test attributes or hard-coded concrete test cases.
- Generated providers enumerate dataset files at runtime, deserialize them, and yield test rows into xUnit.
- The first implementation is primitives-first and intentionally narrow.
- Advanced semantic inference, fuzzing, property testing, and richer type systems are extension areas, not v1 requirements.

## Architectural Principles

- Explicit opt-in over heuristic discovery
- Separate production code from test code
- Keep compile-time generation and runtime execution responsibilities distinct
- Treat datasets as runtime artifacts, not code-generated literals
- Prefer deterministic, traceable behavior over aggressive inference
- Keep the v1 semantic surface small enough to implement confidently

## In Scope for v1

- C#
- Incremental source generation
- Dedicated xUnit v3 test assemblies
- Public static deterministic methods
- Primitive-only method signatures
- JSON dataset files
- Generated theory scaffolding
- Generated runtime dataset yielders
- Example-based success and expected-exception test cases

## Out of Scope for v1

- Tests compiled into production assemblies
- Automatic purity inference
- Instance methods
- Generic methods
- Async methods
- `void` return methods
- Object graphs and custom serialization contracts
- Enums, `Guid`, `DateTime`, `TimeSpan`, and other non-primitive domain types
- `float` and `double` support
- Property-style testing
- Fuzz testing
- Multi-framework support
- Compile-time parsing and validation of dataset JSON contents
- Automatic synthetic boundary-case generation

## Definitive Technology Choices

- Language: C#
- Generation model: Roslyn incremental source generators
- Test framework: xUnit v3
- JSON runtime serialization: `System.Text.Json`
- Dataset exposure model: generated `MemberData`-backed runtime enumerators

These choices are now locked for the first implementation.

## System Boundaries

The system is composed of five distinct concerns.

### 1. Product Library

This contains the methods under test.

Responsibilities:

- Implement business logic
- Reference the testing abstractions package
- Explicitly opt eligible methods into generated testing

The product library does not contain test code.

### 2. Testing Abstractions

This contains the user-facing attribute contract used by product code.

Responsibilities:

- Define the single opt-in attribute for generation
- Define any small, stable compile-time contract types needed by the generator

The v1 abstractions surface is intentionally minimal.

### 3. Testing Generator

This is the compile-time synthesis engine.

Responsibilities:

- Discover eligible target methods from referenced assemblies
- Validate that methods fit the supported v1 contract
- Resolve method-to-dataset mappings
- Verify the existence of dataset directories and files through compile-time file metadata
- Emit generated xUnit scaffolding
- Emit generated dataset-provider code
- Emit build diagnostics for unsupported or ambiguous configurations

The generator does not parse dataset JSON contents in v1.

### 4. Testing Runtime

This is a normal runtime library used by generated tests.

Responsibilities:

- Resolve dataset directories at execution time
- Enumerate dataset files
- Deserialize JSON files
- Normalize runtime load failures into clear test failures
- Execute shared assertion logic

This library exists so the generator does not need to duplicate all runtime behavior into every generated file.

### 5. Dedicated Test Assembly

This hosts both handwritten tests and generated tests.

Responsibilities:

- Reference the product library
- Reference xUnit v3
- Reference the testing runtime package
- Reference the generator as a source generator/analyzer
- Contain the dataset files
- Compile the generated test code

This test assembly is the only place generated tests are allowed to exist.

## Repository Direction

The intended repository layout is:

```text
/src
  /Testing.Abstractions
  /Testing.Generator
  /Testing.Runtime
  /Sample.Library
/tests
  /Sample.Library.Tests
    /datasets
/docs
  PROBLEM.md
  TECHNICAL.md
  VISION.md
  MOCK-DESIGN.md
  HLD.md
```

The exact project names may change, but the separation of responsibilities must not.

## Opt-In Contract

The v1 system uses one required method-level attribute:

- `[GenerateTests("dataset-id")]`

`dataset-id` is a logical identifier and the authoritative binding key between a method and its dataset directory.

In v1, `dataset-id` must follow these rules:

- use `/` as the logical separator
- contain only letters, digits, `-`, `_`, and `/`
- not start or end with `/`
- not contain empty path segments
- not contain `.` or `..` path segments

Dataset ID comparison is case-insensitive in v1 after separator normalization.

This resolves all of the previously open mapping questions:

- Overloads are allowed because the dataset mapping is explicit.
- Method renames do not require dataset moves if the logical dataset ID remains the same.
- Dataset lookup is stable and not inferred from method names.

The attribute also serves as the developer's assertion that the method is appropriate for deterministic generated testing.

No separate `[Deterministic]` attribute is required in v1.

## Dataset Mapping Model

The dataset root is fixed in v1:

- `datasets/`

Each opt-in method maps to exactly one dataset directory:

- `datasets/<dataset-id>/`

Each dataset directory contains one or more `.json` files.

Each `.json` file represents exactly one test case.

Dataset directory enumeration is non-recursive in v1.

This means the binding rules are:

- One method maps to one dataset ID
- One dataset ID maps to one directory
- One directory contains many case files
- One case file yields one theory row

Duplicate use of the same `dataset-id` in the same test assembly is a build error.

Missing dataset directories are build errors.

Empty dataset directories are build errors.

## Supported Method Contract

Eligible methods in v1 must satisfy all of the following:

- `public`
- `static`
- synchronous
- non-generic
- non-`void`
- declared in a referenced product assembly
- annotated with `[GenerateTests("dataset-id")]`
- all parameters use supported v1 types
- return type uses a supported v1 type

Supported parameter and return types in v1 are:

- `bool`
- `byte`
- `sbyte`
- `short`
- `ushort`
- `int`
- `uint`
- `long`
- `ulong`
- `char`
- `decimal`
- `string`

Additional type rules:

- `string` may be nullable if the method signature allows it
- nullable value types are not supported
- `float` and `double` are not supported
- extension methods are not supported in v1

This primitives-first surface is intentionally narrow and is now locked for the first implementation.

## Dataset File Contract

The runtime JSON contract for each dataset file is:

- `inputs`: ordered array of argument values
- `expect`: object containing either `value` or `exception`
- `description`: optional human-readable description

The `inputs` array must match method parameter order.

The `expect` object must contain exactly one of:

- `value`
- `exception`

`exception` must be the exact CLR type name expected at runtime, for example `System.ArgumentException`.

Exception matching is exact-type matching in v1.

An illustrative example:

```json
{
  "description": "Clamps values below the minimum",
  "inputs": [-1, 0, 10],
  "expect": {
    "value": 0
  }
}
```

An expected-exception example:

```json
{
  "description": "Rejects an invalid range",
  "inputs": [10, 0],
  "expect": {
    "exception": "System.ArgumentException"
  }
}
```

The JSON files are runtime inputs. They are not converted into compile-time constants.

## Compile-Time File Participation

Dataset files must participate in the build in two ways:

- As `AdditionalFiles`, so the generator can validate dataset presence and mapping
- As content copied to test output, so runtime execution can load them from disk

This is a critical part of the design.

Compile-time visibility is used for structure validation only.

Runtime file access is used for actual case loading and execution.

## Compile-Time Generation Flow

The compile-time orchestration flow is:

1. The test assembly references the product library, testing abstractions, testing runtime, xUnit v3, and the generator.
2. The test assembly includes `datasets/**/*.json` as `AdditionalFiles` and as copied runtime content.
3. The generator inspects referenced assemblies for methods annotated with `[GenerateTests("dataset-id")]`.
4. The generator validates each discovered method against the supported v1 method contract.
5. The generator groups compile-time dataset files by `dataset-id`.
6. The generator verifies that each target method has a matching non-empty dataset directory.
7. The generator emits one generated test class per target method.
8. The generator emits one dataset case wrapper type per target method.
9. The generator emits one static provider member per target method.
10. The generator emits one xUnit theory method per target method.

No compile-time dataset content parsing occurs in v1.

## Generated Code Shape

For each target method, the generator emits:

- a generated test class
- a generated case model type
- a generated static dataset-provider member
- a generated theory method
- generated direct invocation code for the target method

The generated theory method uses xUnit theory execution with discovery-time data enumeration disabled.

The generator should prefer a shape equivalent to:

- `[Theory(DisableDiscoveryEnumeration = true)]`
- `[MemberData(nameof(GeneratedProvider))]`

This is the preferred model because dataset loading requires runtime file I/O and should not occur during test discovery.

The generated provider yields one row per dataset file.

Each row should carry a single strongly typed generated case object rather than expanding method parameters directly into the theory signature.

This keeps generated data flow stable regardless of target method arity and keeps runtime error reporting easier to control.

## Runtime Execution Flow

The runtime execution flow is:

1. xUnit discovers the generated theory.
2. xUnit defers data enumeration until execution.
3. The generated provider resolves the runtime dataset directory using `AppContext.BaseDirectory` and the fixed `datasets/<dataset-id>` path.
4. The runtime library enumerates `.json` files in ordinal-stable order.
5. Each file is deserialized with `System.Text.Json` into the generated case model for that target method.
6. The provider yields one row per successfully loaded file.
7. The generated theory receives the generated case object.
8. The generated invocation code calls the target method directly with the deserialized primitive arguments.
9. The runtime assertion helper verifies either the expected return value or the expected exception.
10. Any failure reports the target method identity and dataset file path.

This is the canonical execution model for the project.

## Assertion Model

The v1 assertion model is intentionally simple.

Success cases:

- Compare actual return value to expected value using exact equality

Expected-exception cases:

- Assert that execution throws the exact exception type declared in the dataset file

Assertion rules are not pluggable in v1.

This is an intentional scope decision that keeps primitives-first testing straightforward.

## Determinism Contract

The system assumes the developer only annotates methods that are safe for deterministic repeated execution.

The system does not attempt to prove purity.

By opting a method into generation, the developer is asserting that the method:

- does not depend on ambient time
- does not depend on randomness
- does not depend on process-global mutable state
- does not depend on environment-specific side effects
- is safe to invoke repeatedly in any order

Violations of this contract are user errors and are expected to surface as flaky or invalid tests.

## Error and Diagnostic Policy

The system divides problems into build-time diagnostics and runtime test failures.

Build-time diagnostics:

- unsupported method shape
- unsupported parameter or return type
- duplicate dataset ID usage
- missing dataset directory
- empty dataset directory

Runtime test failures:

- malformed JSON
- wrong input count
- wrong JSON primitive type
- unresolved exception type
- target method throwing an unexpected exception
- target method returning an unexpected value

This split is intentional.

The generator validates architecture and wiring.

The runtime validates actual dataset payloads.

## Extension Points

The v1 architecture is intentionally narrow, but it is designed to grow in controlled ways.

Planned extension points are:

- richer primitive and near-primitive type codecs
- optional synthetic boundary-case providers
- optional invalid-input providers
- additional dataset formats
- alternate assertion strategies
- alternate test-framework adapters

These are future extension points only.

They are not part of the locked v1 implementation surface.

## Explicit v1 Non-Goals for Semantic Inference

The generator will not, in v1:

- infer correctness properties from code alone
- infer domain-specific invariants
- infer valid numeric ranges automatically
- synthesize fuzz cases
- synthesize property tests
- inspect method bodies for purity

The current architecture supports adding these later, but implementation should not be delayed for them.

## Implementation Sequence

Implementation should proceed in this order:

1. Build `Testing.Abstractions` with the single `[GenerateTests("dataset-id")]` attribute.
2. Build `Testing.Runtime` with dataset path resolution, JSON loading, case normalization, and assertion helpers.
3. Build `Testing.Generator` as an incremental source generator targeting referenced product assemblies.
4. Build a sample product library with eligible public static primitive methods.
5. Build a sample dedicated xUnit v3 test assembly with `datasets/` content configured as both `AdditionalFiles` and copied runtime files.
6. Validate end-to-end generation and runtime execution with example-based success and expected-exception datasets.

This sequence is the intended production implementation path.

## Final Architectural Decisions

The following decisions are now final:

- Generated tests live only in dedicated test assemblies
- xUnit v3 is the only supported framework in v1
- The generator uses a dataset-yielder model, not pre-expanded static test attributes
- The generator does not parse JSON contents at compile time
- The runtime loads JSON from disk during test execution
- The mapping key is an explicit logical `dataset-id`
- The v1 method surface is public static synchronous non-generic primitive-only methods
- The v1 dataset format is one JSON file per case under `datasets/<dataset-id>/`
- The v1 semantic contract is example-based datasets plus expected exceptions

These decisions should be treated as locked implementation direction.
