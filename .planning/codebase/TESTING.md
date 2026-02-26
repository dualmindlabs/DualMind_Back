# Testing Patterns

**Analysis Date:** 2026-02-27

## Test Framework

**Runner:**
- The codebase explicitly defines the command `dotnet test` in `CLAUDE.md`.
- **Note:** An extensive recursive search found no existing `.Tests` or `.Test` C# projects in the current repository path (`C:\Users\Harshu\source\repos\dualmind_back\`). Therefore, testing is currently undocumented in source or not included in this repository structure.

**Assertion Library:**
- Not applicable (No unit tests discovered).

**Run Commands:**
```bash
dotnet test              # Run all tests (as per CLAUDE.md)
```

## Test File Organization

**Location:**
- No testing structure exists in the current repository layout.

**Naming:**
- Not applicable.

**Structure:**
- Not applicable.

## Test Structure

**Suite Organization:**
```csharp
// No current test patterns detected.
```

**Patterns:**
- No setup, teardown, or assertion patterns are currently active.

## Mocking

**Framework:** None detected.

**Patterns:**
```csharp
// No current mocking patterns detected.
```

**What to Mock:**
- Generally, if tests are written, `ISupabaseService`, `IAdminSupabaseClient`, `IChatProvider`, and `ILogger` should be mocked as they communicate directly with external APIs (Supabase PostgREST, Groq API).

**What NOT to Mock:**
- DTOs and Data models.

## Fixtures and Factories

**Test Data:**
```csharp
// No current fixture patterns detected.
```

**Location:**
- Not applicable.

## Coverage

**Requirements:** None enforced.

**View Coverage:**
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

## Test Types

**Unit Tests:**
- Not currently used or detected.

**Integration Tests:**
- Not currently used or detected.

**E2E Tests:**
- Not currently used or detected.

## Common Patterns

**Async Testing:**
```csharp
// Not detected. Recommended pattern when implemented:
[Fact]
public async Task Sample_Async_Test()
{
    // Arrange
    // Act
    // Assert
}
```

**Error Testing:**
```csharp
// Not detected. Recommended pattern when implemented:
await Assert.ThrowsAsync<ProviderExhaustedException>(() => _service.CallAsync());
```

---

*Testing analysis: 2026-02-27*