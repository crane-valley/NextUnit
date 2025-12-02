# NextUnit Coding Standards

## Language Requirements

### English-Only Policy

**All code, comments, and documentation must be written in English.**

This includes:
- ? **Code comments** - All inline comments, XML documentation comments, TODO markers
- ? **Documentation files** - README.md, PLANS.md, DEVLOG.md, all .md files
- ? **Commit messages** - Git commit messages and pull request descriptions
- ? **Variable/method names** - All identifiers must use English words
- ? **String literals** - User-facing messages and error messages
- ? **Test names** - Test method names and test descriptions

**Rationale:**
- International collaboration and open-source contribution
- Consistency with .NET ecosystem conventions
- Better tooling support (IntelliSense, code analyzers, etc.)
- Easier code reviews and maintenance

**Examples:**

? **Bad** (Japanese comments):
```csharp
// 依存関係をチェック
if (!completed.Contains(depId))
{
    return true; // スキップ
}
```

? **Good** (English comments):
```csharp
// Check if all dependencies have completed
if (!completed.Contains(depId))
{
    return true; // Skip this test
}
```

## Code Style

### C# Conventions

Follow standard .NET coding conventions:
- Use PascalCase for public members
- Use camelCase for private fields with `_` prefix
- Use meaningful, descriptive names
- Prefer explicit types over `var` when clarity is important

### XML Documentation

All public APIs must have XML documentation:

```csharp
/// <summary>
/// Executes a test method asynchronously.
/// </summary>
/// <param name="test">The test case descriptor.</param>
/// <param name="instance">The test class instance.</param>
/// <param name="cancellationToken">A cancellation token to observe.</param>
/// <returns>A task representing the asynchronous operation.</returns>
public static async Task InvokeTestAsync(
    TestCaseDescriptor test,
    object instance,
    CancellationToken cancellationToken)
{
    // Implementation
}
```

### TODO Comments

TODO comments must:
- Be written in English
- Reference the milestone (e.g., `TODO M1:`, `TODO M2:`)
- Describe what needs to be done
- Be actionable

**Examples:**

? **Good TODO comments:**
```csharp
// TODO M1: Remove this reflection fallback before v1.0
// TODO M2: Implement Assembly-scoped lifecycle hooks
// TODO M3: Enforce ParallelLimit constraints in scheduler
```

? **Bad TODO comments:**
```csharp
// TODO: 将来の実装で使用 (Japanese)
// TODO: fix this (too vague)
// TODO (no description)
```

## File Organization

### Project Structure

```
NextUnit/
├── src/
│   ├── NextUnit.Core/          # Core framework (attributes, assertions, execution)
│   ├── NextUnit.Generator/     # Source generator
│   └── NextUnit.Platform/      # Microsoft.Testing.Platform integration
├── samples/
│   └── NextUnit.SampleTests/   # Sample tests for validation
├── docs/                       # Documentation files
├── README.md                   # Project overview (English only)
├── PLANS.md                    # Implementation roadmap (English only)
├── DEVLOG.md                   # Development log (English only)
└── CODING_STANDARDS.md         # This file
```

### File Headers

All source files should start with:

```csharp
namespace NextUnit.Internal;

/// <summary>
/// Brief description of the class.
/// </summary>
public sealed class ClassName
{
    // Implementation
}
```

## Git Conventions

### Commit Messages

Commit messages must be in English and follow this format:

```
<type>: <short summary in English>

<optional detailed description in English>
```

**Types:**
- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation changes
- `refactor:` - Code refactoring
- `test:` - Test additions/changes
- `chore:` - Maintenance tasks

**Examples:**

? **Good commit messages:**
```
feat: Add source generator delegate emission
fix: Correct ParallelInfo serialization in generator
docs: Update README with M1 progress
refactor: Remove reflection from execution engine
```

? **Bad commit messages:**
```
update (too vague)
修正 (Japanese)
WIP (not descriptive)
```

## Documentation Standards

### Markdown Files

All .md files must:
- Use English for all content
- Use proper markdown formatting
- Include clear section headers
- Have a table of contents for long documents
- Use code blocks with syntax highlighting

**Example:**

```markdown
# Feature Name

## Overview

Brief description in English.

## Usage

```csharp
// English code comments
public void ExampleMethod()
{
    // Implementation
}
```

## See Also

- [Related Document](link)
```

### Code Examples

Code examples in documentation must:
- Be compilable and runnable
- Include English comments explaining the code
- Demonstrate best practices
- Show expected output

## Review Checklist

Before submitting code, verify:

- [ ] All comments are in English
- [ ] All TODO markers reference a milestone and use English
- [ ] XML documentation is complete for public APIs
- [ ] Variable/method names use English words
- [ ] Commit messages are in English
- [ ] No Japanese characters in code or comments
- [ ] Documentation files are in English
- [ ] Code follows .NET conventions

## Automated Checks

### Future Enhancements

Consider adding:
- Roslyn analyzer to detect non-English comments
- Git pre-commit hooks to check commit messages
- CI pipeline checks for documentation language
- Code review checklist automation

## References

- [.NET Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [XML Documentation Comments](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/)
- [Microsoft.Testing.Platform Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-platform-intro)

---

**Last Updated**: 2025-12-02  
**Applies To**: All NextUnit projects and contributions
