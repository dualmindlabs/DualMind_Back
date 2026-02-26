# Coding Conventions

**Analysis Date:** 2026-02-27

## Naming Patterns

**Files:**
- PascalCase for all C# files: `ArenaController.cs`, `ModelStatsService.cs`, `IChatProvider.cs`

**Functions:**
- PascalCase for all methods: `GetModelStatsAsync()`, `RecordVoteAsync()`, `SelectAsync<T>()`
- `Async` suffix is consistently applied to asynchronous methods (e.g., `ExecuteWithFallbackAsync`).

**Variables:**
- camelCase for local variables: `sanitizedModelName`, `startTime`, `sessionId`
- `_camelCase` with an underscore prefix for private readonly fields (dependency injection): `_supabase`, `_logger`, `_modelSelector`

**Types:**
- Interfaces use the `I` prefix: `IModelStatsService`, `IChatProvider`, `ISupabaseService`
- PascalCase for classes, records, and DTOs: `ModelStatsDto`, `ChatRequest`, `GroqResponse`

## Code Style

**Formatting:**
- 4 spaces for indentation.
- Allman style braces (braces on a new line).
- Standard C# naming and styling conventions observed.

## Import Organization

**Order:**
Generally unstructured but typically follows:
1. System imports: `System`, `System.Threading.Tasks`, `System.Collections.Generic`
2. Microsoft packages: `Microsoft.AspNetCore.Mvc`, `Microsoft.Extensions.Logging`
3. Internal domain imports: `DualMind.API.Core.Models`, `DualMind.API.Core.Services`
4. Third-party packages: `Newtonsoft.Json`, `Newtonsoft.Json.Linq`

## Error Handling

**Patterns:**
- Extensive use of `try/catch` blocks surrounding core service and controller operations.
- Specific exception throwing with descriptive messages: `throw new ArgumentException("Winner model name cannot be empty", nameof(winnerModelName));`
- Custom exceptions are used for domain-specific errors: `catch (ProviderExhaustedException pex)`
- Controllers return specific HTTP status codes with structured JSON error objects on exceptions (e.g., returning HTTP 500 with `@object: "ai.error"` and a descriptive message).
- Fallback logic is heavily used in AI provider failures, automatically switching to default models (e.g., `FallbackToGroqAsync`).

## Logging

**Framework:**
- `Microsoft.Extensions.Logging.ILogger<T>` injected via DI.

**Patterns:**
- Errors are logged with the exception object and a descriptive message: `_logger.LogError(ex, "Failed to get model stats");`
- Contextual information is passed as structured arguments: `_logger.LogError(ex, "Failed to record vote for comparison {ComparisonId}", comparisonId);`
- Information and warnings are used for application state tracking, such as provider fallbacks: `_logger.LogWarning($"Provider '{providerName}' timed out...");`

## Comments

**When to Comment:**
- Primarily used to explain non-obvious business logic, workarounds, or important behavioral notes.
- E.g., `// Fetch comparison through masked view — enforces blind vote`
- E.g., `// 🚨 Ensure public.users row exists before linking messages`

**JSDoc/TSDoc / XML Docs:**
- XML Documentation (`///`) is not widely used or enforced in the current source files. Most context is inferred from naming or inline comments.

## Function Design

**Size:**
- Ranging from small utility methods to large orchestration methods (e.g., `DualChat` in `ArenaController` is over 200 lines).

**Parameters:**
- Frequently accepts simple scalar values and DTOs.
- Uses nullable types (`Guid?`, `int?`, `string?`) heavily to handle optional input gracefully.

**Return Values:**
- Usually wrapped in `Task<T>` for async operations.
- Controllers use `IActionResult` returning standard `Ok()` or `StatusCode()` objects wrapping structured anonymous types.

## Architecture Patterns

- **Dependency Injection (DI):** Heavily utilized. All services, repositories, and loggers are injected via constructors.
- **DTOs:** Strong boundary between database representation (`JObject` from Supabase) and application/API layers (`ModelStatsDto`, `ChatResponse`).
- **Data Access:** No Entity Framework. The application communicates with PostgreSQL directly via Supabase PostgREST HTTP endpoints (`SupabaseService`), wrapping calls in `HttpClient`.

---

*Convention analysis: 2026-02-27*