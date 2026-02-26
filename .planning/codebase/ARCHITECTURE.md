# Architecture

**Analysis Date:** 2026-02-27

## Pattern Overview

**Overall:** Service-Oriented Web API layered over Supabase PostgREST

**Key Characteristics:**
- Controller-Service-Data structure but no traditional ORM (no Entity Framework)
- Supabase acts as both database and REST data layer
- Abstraction over multiple AI provider integrations via Factory pattern
- Centralized model and API key rotation mechanisms

## Layers

**Controllers:**
- Purpose: Handle HTTP requests, auth assertions, response formatting
- Location: `src/DualMind.API/Controllers/`
- Contains: ASP.NET Core MVC Controllers (`ArenaController`, `ThreadsController`, etc.)
- Depends on: Core Services, Models
- Used by: External clients

**Core Services:**
- Purpose: Business logic, orchestration between database and AI APIs
- Location: `src/DualMind.API/Core/Services/`
- Contains: Business logic classes (`ThreadsService`, `ArenaService`, etc.)
- Depends on: Data layer, AI layer
- Used by: Controllers

**AI Layer:**
- Purpose: Manage abstraction to multiple AI providers
- Location: `src/DualMind.API/AI/`
- Contains: Providers (`GroqService`), Gateway (`ChatProviderFactory`), Contracts
- Depends on: External LLM APIs
- Used by: Core Services, Controllers

**Data Access:**
- Purpose: Direct communication with Supabase PostgREST endpoints
- Location: `src/DualMind.API/Infrastructure/Data/`
- Contains: `SupabaseService`, `AdminSupabaseClient`
- Depends on: `HttpClient`, PostgREST API
- Used by: Core Services

## Data Flow

**Standard AI Chat Request (`/api/arena/chat`):**

1. Client sends request to `ArenaController.Chat`
2. `ModelSelector` caches/fetches active AI models and selects one
3. `ChatProviderFactory` returns appropriate AI provider client
4. Selected AI Provider executes HTTP request to LLM
5. Response mapped to standard `ChatResponse` format
6. `UserSyncService` upserts the current user using `SupabaseService`
7. `ThreadMessagesService` logs the interaction using `SupabaseService`
8. Response returned to client

**State Management:**
- Application state is predominantly stateless
- Memory caching is used heavily for `ai_models` (`ModelSelector`) and provider configurations (`ProviderConfigService`) to reduce database latency for frequent data points

## Key Abstractions

**AI Provider Abstraction:**
- Purpose: Allow drop-in replacement or routing among different LLM providers
- Examples: `src/DualMind.API/AI/Contracts/IChatProvider.cs`, `src/DualMind.API/AI/Gateway/ChatProviderFactory.cs`
- Pattern: Factory Pattern + Standard Interface

**Database Client Abstraction:**
- Purpose: Standardize dynamic JSON payloads to PostgREST
- Examples: `src/DualMind.API/Infrastructure/Data/ISupabaseService.cs`, `src/DualMind.API/Infrastructure/Data/SupabaseService.cs`
- Pattern: Thin wrapper over `HttpClient` utilizing NewtonSoft `JObject` and generic types

## Entry Points

**API Controllers:**
- Location: `src/DualMind.API/Controllers/Api/ArenaController.cs`
- Triggers: HTTP REST calls from frontend/clients
- Responsibilities: Main application logic execution

**Program.cs:**
- Location: `src/DualMind.API/Program.cs`
- Triggers: Process startup
- Responsibilities: Dependency Injection setup, configuration loading, middleware pipeline

## Error Handling

**Strategy:** Global Exception Middleware with explicit fallback mechanisms for critical paths (like AI execution)

**Patterns:**
- **Global Error Handler**: Catches all unhandled exceptions in `Program.cs` and returns structured `ProblemDetails` JSON (`application/problem+json`).
- **AI Fallbacks**: In `ArenaController`, `ExecuteWithFallbackAsync` catches `ProviderExhaustedException` and timeouts, falling back to a default Groq model gracefully.

## Cross-Cutting Concerns

**Logging:** Uses standard `Microsoft.Extensions.Logging`. Added structured request logging middleware in `Program.cs` using correlation IDs.
**Validation:** Basic null/empty string checking in controllers before processing requests.
**Authentication:** Validates Supabase JWTs. Contains a fallback mechanism bypassing signature validation if `JWT_SECRET` is missing in development (`Program.cs`).

---

*Architecture analysis: 2026-02-27*