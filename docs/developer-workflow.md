# Developer Authoring Workflow and v1 Extension Boundaries

**Status:** AUTHORITATIVE — This document defines the supported v1 workflow and is the first place contributors should look when adding data-driven tests.

**Audience:** Developers adding generated tests, reviewers, and anyone extending the system.

---

## Quick Start

1. Annotate a `public static` method with `[GenerateTests("dataset-id")]` in your production library.
2. Create a `datasets/<dataset-id>/` directory in your **dedicated test assembly**.
3. Add one `.json` file per test case inside that directory.
4. Build. The generator validates the wiring at compile time.
5. Run tests. The generated infrastructure loads datasets at runtime and executes them.

---

## 1. Opting a Method into Generated Testing

Add `[GenerateTests("dataset-id")]` to a `public static` method in any assembly that references `Testing.Abstractions`.

```csharp
using Testing.Abstractions;

namespace MyLib;

public static class Calculator
{
    [GenerateTests("calc/add")]
    public static int Add(int a, int b) => a + b;
}
```

The method does **not** need to be in the test assembly. It belongs in your production library. The generator discovers it by scanning referenced assemblies at compile time.

A method must satisfy **all** of the following to be eligible:

| Requirement | Diagnostic if violated |
|---|---|
| `public` | TG001 — Method must be public |
| `static` | TG002 — Method must be static |
| Synchronous (not `async`) | (compile error from C#) |
| Non-generic | TG003 — Method must not be generic |
| Non-`void` return | TG004 — Method must not return void |
| All parameters use v1 supported types | TG006 — Unsupported parameter type |
| Return type uses a v1 supported type | TG005 — Unsupported return type |

Violating any of these produces a build error. You cannot ship with broken generation wiring.

---

## 2. Choosing and Naming a Dataset ID

The dataset ID is the logical binding key between a method and its dataset directory. It appears in both the attribute and the filesystem path.

### Rules

- Use `/` as the logical separator (maps to filesystem path segments)
- Characters allowed: letters (`a–z`, `A–Z`), digits (`0–9`), `-`, `_`, `/`
- Must **not** start or end with `/`
- Must **not** contain empty segments (`//`)
- Must **not** contain `.` or `..` as path segments
- Comparison is case-insensitive after normalization (stored lowercase)

### Valid examples

```
calc/add
sample-math/clamp
math/clamp
string-utils/to-upper
parsing/ipv4/validate
```

### Invalid examples

```
/has-leading-slash     — starts with /
trailing-slash/        — ends with /
double//slash          — contains empty segment
a/./b                  — contains . segment
has space              — contains invalid character
```

### Choosing a good ID

Prefer descriptive, hierarchical names that group related functions:

```
calc/add
calc/subtract
calc/multiply
```

The dataset ID is **decoupled from method names**. Renaming a method does not require moving dataset files, and overloads can coexist with different dataset IDs.

Each dataset ID must be **unique** within a test assembly. Reusing the same ID for two methods in the same assembly produces diagnostic TG007 at build time.

---

## 3. Placing Datasets

Datasets live in the **test assembly**, not the production library. The fixed root is:

```
tests/<TestProject>/datasets/
```

### Directory layout

```
tests/Sample.Library.Tests/
  datasets/
    sample-math/
      add/
        positive-numbers.json
        negative-numbers.json
        mixed-sign.json
      subtract/
        positive-result.json
        negative-result.json
        identical-operands.json
      clamp/
        value-in-range.json
        value-below-min.json
        value-above-max.json
        min-equals-max.json
        min-greater-than-max.json
```

Each dataset ID maps to exactly one directory: `datasets/<dataset-id>/`. Each `.json` file in that directory represents one test case.

### Required project configuration

The test project `.csproj` must configure dataset files two ways:

```xml
<!-- Compile-time visibility: lets the generator validate directories exist -->
<ItemGroup>
  <AdditionalFiles Include="datasets\**\*.json" />
</ItemGroup>

<!-- Runtime visibility: copies files to test output -->
<ItemGroup>
  <Content Include="datasets\**\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

Both are required. Without `AdditionalFiles`, the generator cannot verify dataset presence at build time. Without `Content` + `CopyToOutputDirectory`, the runtime cannot load files during test execution.

### Dataset file format

Each `.json` file has this structure:

```json
{
  "description": "Optional human-readable description of the case",
  "inputs": [2, 3],
  "expect": {
    "value": 5
  }
}
```

For expected exceptions:

```json
{
  "description": "Rejects invalid range",
  "inputs": [5, 10, 0],
  "expect": {
    "exception": "System.ArgumentException"
  }
}
```

**Rules:**
- `inputs` must be a JSON array whose elements match the method parameter order
- `expect` must contain exactly one of `value` or `exception`
- `exception` must be the exact CLR type name (e.g., `System.ArgumentException`)
- `description` is optional
- Exception matching is exact-type matching (inheritance is not checked)

---

## 4. Build-Time vs Runtime Validation

The system splits validation into two phases.

### Build-time diagnostics (errors prevent compilation)

| Diagnostic | What it catches |
|---|---|
| TG001 | Method is not `public` |
| TG002 | Method is not `static` |
| TG003 | Method is generic |
| TG004 | Method returns `void` |
| TG005 | Unsupported return type |
| TG006 | Unsupported parameter type |
| TG007 | Duplicate dataset ID in the same test assembly |
| TG008 | Invalid dataset ID format |
| TG009 | Dataset ID has no matching directory or directory is empty |

Build-time diagnostics validate **architecture and wiring**. They ensure the method shape is correct, the dataset ID is valid, and the dataset directory exists and contains at least one `.json` file.

### Runtime test failures (reported as test failures)

| Failure | What it means |
|---|---|
| Dataset directory not found | The directory was present at build time but is missing at execution time (deleted or not copied to output) |
| Invalid JSON | A `.json` file contains malformed JSON |
| Missing `inputs` | A `.json` file does not have the required `inputs` property |
| `inputs` not an array | The `inputs` property is not a JSON array |
| Missing `expect` | A `.json` file does not have the required `expect` property |
| `expect` not an object | The `expect` property is not a JSON object |
| Both `value` and `exception` present | `expect` must contain exactly one |
| Neither `value` nor `exception` present | `expect` must contain exactly one |
| Empty `exception` value | The exception type name is empty or whitespace |
| Wrong input count | The JSON array length does not match the method parameter count |
| Wrong JSON value type | A JSON value does not match the expected primitive type |
| Unexpected return value | The method returned a value that does not match the expected value |
| Expected exception not thrown | The dataset expects an exception but the method completed normally |
| Wrong exception type | The method threw an exception of a different type than expected |
| Unexpected exception | The method threw an exception when a return value was expected |

Runtime validation covers **data payload correctness**. These failures appear as test failures in the test runner.

---

## 5. Interpreting Generated Test Failures

Generated test failures include the method name and the dataset file path. A typical failure message:

```
Method 'Sample.Library.MathUtils.Clamp' returned an unexpected value for dataset
'/path/to/tests/Sample.Library.Tests/bin/Debug/net10.0/datasets/sample-math/clamp/value-in-range.json'.
Expected: 7, Actual: 5.
```

To debug:
1. Look at the dataset file named in the failure.
2. Verify the `inputs` array matches the method parameter order and count.
3. Verify the `expect.value` or `expect.exception` matches the expected behavior.
4. Confirm the method is deterministic with those inputs.

If the method threw an unexpected exception, the failure includes the exception type and message:

```
Method 'Sample.Library.MathUtils.Clamp' threw an unexpected exception for dataset
'/path/to/datasets/sample-math/clamp/value-in-range.json':
System.ArgumentException: min must be less than or equal to max
```

### Common causes

- **Wrong input order**: The JSON array at `inputs` must match the C# parameter order. `[2, 3]` for `Add(int a, int b)` means `a=2, b=3`.
- **Wrong JSON type**: `"5"` (string) is not `5` (number). The JSON type must correspond to the C# parameter type.
- **Exception type mismatch**: Use the exact CLR type name (`System.ArgumentException`, not just `ArgumentException`).
- **Non-deterministic behavior**: The method is not deterministic. See the determinism contract below.

---

## 6. Dedicated Test Assembly Boundary

Generated tests **must live in a dedicated test assembly**. They cannot exist in the production library.

### Why

- Production code and generated test code must not be mixed in the same assembly.
- The generator emits xUnit scaffolding, which requires xUnit references — those should not be production dependencies.
- Dataset files are test artifacts, not production artifacts.
- Build-time validation depends on `AdditionalFiles` configured in the test project.

### Required test project setup

Your test `.csproj` must:

1. Reference the production library (the one containing annotated methods)
2. Reference `Testing.Runtime` (for dataset loading and assertion helpers)
3. Reference `Testing.Generator` as an analyzer:
   ```xml
   <ProjectReference Include="..\..\src\Testing.Generator\Testing.Generator.csproj"
                     OutputItemType="Analyzer"
                     ReferenceOutputAssembly="true" />
   ```
4. Reference `xunit.v3` (test framework)
5. Include dataset files as both `AdditionalFiles` and `Content` (see section 3)

---

## 7. v1 Supported Types

The v1 implementation supports exactly these primitive types for both parameters and return values:

| C# keyword | .NET type |
|---|---|
| `bool` | `System.Boolean` |
| `byte` | `System.Byte` |
| `sbyte` | `System.SByte` |
| `short` | `System.Int16` |
| `ushort` | `System.UInt16` |
| `int` | `System.Int32` |
| `uint` | `System.UInt32` |
| `long` | `System.Int64` |
| `ulong` | `System.UInt64` |
| `char` | `System.Char` |
| `decimal` | `System.Decimal` |
| `string` | `System.String` |

`string` may be nullable where the method signature allows it. Nullable value types (`int?`, `bool?`) are **not** supported.

---

## 8. v1 Unsupported Types and Method Categories

The following are intentionally **out of scope for v1** and will produce build errors or simply not be matched by the generator:

### Unsupported types

- `float` and `double` (floating-point equality is non-trivial)
- `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan`
- Enums of any kind
- Nullable value types (`int?`, `bool?`, etc.)
- Custom structs, classes, records, or object graphs
- Collections, arrays, or generics of any kind

### Unsupported method categories

- Instance methods (must be `static`)
- Generic methods
- `async` methods
- `void`-returning methods
- Extension methods
- Methods with `ref`, `out`, or `in` parameters
- Methods in assemblies not referenced by the test project

### Unsupported dataset patterns

- Nested subdirectories (enumeration is non-recursive)
- Multiple dataset files representing one case (one file = one case)
- Custom serialization contracts (only `System.Text.Json` defaults are used)

---

## 9. Determinism Contract

By annotating a method with `[GenerateTests]`, the developer asserts that the method:

- Does **not** depend on the system clock (`DateTime.UtcNow`, `DateTimeOffset.Now`, etc.)
- Does **not** depend on randomness (`Random`, `Guid.NewGuid`, etc.)
- Does **not** depend on process-global mutable state
- Does **not** depend on environment-specific side effects (file system, network, environment variables)
- Is safe to invoke repeatedly, in any order, with the same inputs producing the same outputs

The system **does not verify purity**. It trusts the attribute.

Violating this contract produces flaky or non-reproducible tests. The generated infrastructure does not retry, fuzz, or isolate non-determinism. If a test fails intermittently, the first thing to check is whether the method violates the determinism contract.

---

## 10. Locked v1 Behavior vs Future Extension Areas

### Locked (will not change without a coordinated design update)

- xUnit v3 as the test framework
- `System.Text.Json` for dataset serialization
- `[GenerateTests("dataset-id")]` as the opt-in attribute
- Dataset ID rules and normalization
- One JSON file per test case
- Non-recursive dataset directory enumeration
- `[Theory]` + `[MemberData]` as the generated test shape
- Exact-type exception matching
- Direct invocation of the target method (no proxy or wrapper)
- Separate test assembly requirement

### Future extension areas (explicitly not implemented in v1)

These are documented in `HLD.md` and `VISION.md` as planned extensions. They are **not available now** and attempting to use them will not work:

- `float` and `double` support (requires equality tolerance configuration)
- Richer type codecs: `Guid`, `DateTime`, enums, nullable value types
- Instance method support
- Generic method support
- Async method support
- Recursive dataset directory enumeration
- Additional dataset formats beyond JSON (CSV, etc.)
- Compile-time JSON content validation
- Synthetic boundary-case generation (min/max/zero/null for numeric types)
- Fuzz testing and property-based testing
- Semantic inference from method signatures
- Alternate assertion strategies (approximate equality, custom comparers)
- Alternate test framework adapters (NUnit, MSTest)
- Custom serialization contracts
- Multi-framework support

**Contributors should not implement these extensions without first updating the design documents and getting agreement on the approach.** The v1 surface is intentionally narrow to keep the system provably correct before expanding.

---

## 11. Step-by-Step: Adding a New Generated Test

1. **Write the method** in your production library:
   ```csharp
   [GenerateTests("math/max")]
   public static int Max(int a, int b) => a > b ? a : b;
   ```

2. **Create the dataset directory**: `tests/<TestProject>/datasets/math/max/`

3. **Add dataset files**. For example, `a-greater.json`:
   ```json
   {
     "description": "First argument is greater",
     "inputs": [10, 3],
     "expect": { "value": 10 }
   }
   ```
   And `b-greater.json`:
   ```json
   {
     "description": "Second argument is greater",
     "inputs": [3, 10],
     "expect": { "value": 10 }
   }
   ```

4. **Ensure `.csproj` wiring**: The file patterns `datasets\**\*.json` must be present as both `AdditionalFiles` and `Content` with `CopyToOutputDirectory`.

5. **Build**. If the build fails with a TGxxx diagnostic, fix the issue before proceeding.

6. **Run tests**. The generated test class `Math_Max_Tests` appears in the test runner alongside handwritten tests.

7. **Fix any runtime failures** by correcting the dataset files.

---

## Related Documents

- **[HLD.md](/HLD.md)** — Locked architectural decisions, full design rationale
- **[PROBLEM.md](/PROBLEM.md)** — Problem statement and success criteria
- **[VISION.md](/VISION.md)** — Long-term vision and desired workflow
- **[TECHNICAL.md](/TECHNICAL.md)** — Technical direction and implementation guidance
