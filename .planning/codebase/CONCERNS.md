# Codebase Concerns

**Analysis Date:** 2026-02-27

## Tech Debt

**Error Handling & Fallbacks in ArenaController:**
- Issue: Deeply nested try-catch blocks and manual fallback logic in `ExecuteWithFallbackAsync`. Hardcoded fallback to `"groq"` and `EnvConfig.DefaultGroqModel` instead of a configurable fallback chain.
- Files: `src/DualMind.API/Controllers/Api/ArenaController.cs`
- Impact: Makes testing and modifying the fallback logic difficult. If Groq goes down or is removed, the fallback will break or require code changes.
- Fix approach: Implement a more robust Chain of Responsibility or Strategy pattern for AI provider fallbacks, configured via DB or appsettings rather than hardcoded strings.

**Admin Auth Bypass:**
- Issue: Admin controllers exist but lack explicit authorization checks on the controllers themselves (as noted in `CLAUDE.md`).
- Files: `src/DualMind.API/Controllers/Admin/*` (e.g. `ProvidersController.cs`)
- Impact: Potential unauthorized access to admin operations, including reading provider API keys.
- Fix approach: Add `[Authorize(Roles = "admin")]` (or similar policy) to all admin controllers and ensure the JWT token includes role claims.

**Direct DB Context in Services:**
- Issue: `IAdminSupabaseClient` and `ISupabaseService` are used directly in services and controllers (e.g., `ProvidersController`, `ThreadsService`, `ModelStatsService`) with raw string queries and table names.
- Files: `src/DualMind.API/Core/Services/*`, `src/DualMind.API/Controllers/*`
- Impact: Prone to SQL-like injection (via PostgREST filter strings), tight coupling to Supabase schema, and difficult unit testing without mocking the entire HTTP client/Supabase layer.
- Fix approach: Implement the Repository pattern. Create specific interfaces like `IProviderRepository`, `IThreadRepository`, etc., that encapsulate the Supabase PostgREST calls.

**JWT Validation Bypass for Development:**
- Issue: In `Program.cs`, if `JWT_SECRET` is missing, it falls back to a mode that completely bypasses signature and audience validation, trusting any token.
- Files: `src/DualMind.API/Program.cs`
- Impact: Huge security risk if deployed to production without `JWT_SECRET` configured properly. It would allow anyone to forge a token and act as any user.
- Fix approach: Restrict this fallback strictly to `env.IsDevelopment()` environment checks, and fail fast in production if `JWT_SECRET` is missing.

## Known Bugs

**Race Conditions in User Sync:**
- Symptoms: `EnsureUserExistsAsync` uses a basic `UpsertAsync` but relies on JWT claims that might be stale or missing. If multiple concurrent requests hit this for a new user, it might lead to duplicate attempts or overwrites.
- Files: `src/DualMind.API/Core/Services/UserSyncService.cs`
- Trigger: Rapid successive requests from a newly authenticated user before their record is fully synchronized.
- Workaround: Supabase `Upsert` handles the DB level, but the API might still experience transient errors or unnecessary DB calls.

## Security Considerations

**API Key Exposure in Admin Endpoints:**
- Risk: `GetProviderKeys` in `ProvidersController` returns the raw `ApiKey` to the client.
- Files: `src/DualMind.API/Controllers/Admin/ProvidersController.cs`
- Current mitigation: None (other than the implicit assumption that only admins can access it, which is currently flawed due to missing `[Authorize]` attributes).
- Recommendations: Never return the full raw API key to the client after it's been created. Only return the `DisplayMask`.

**CORS Policy:**
- Risk: The CORS policy is globally set to `AllowAll` (`AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`).
- Files: `src/DualMind.API/Program.cs`
- Current mitigation: None.
- Recommendations: Restrict CORS origins to the known frontend URLs (e.g., the Vercel/Netlify frontend domains) via configuration.

## Performance Bottlenecks

**Model Selection Cache:**
- Problem: `ModelSelector` uses an in-memory cache expiring every 5 minutes. If an admin updates a model status, it takes up to 5 minutes to reflect across the application.
- Files: `src/DualMind.API/Core/Services/ModelSelector.cs`
- Cause: Time-based expiration without a cache invalidation mechanism.
- Improvement path: Implement a cache invalidation signal (e.g., when `AdminAIModelsController` updates a model, it should clear or update the `IMemoryCache` entry).

**Sequential HTTP Calls for Dual Chat Fallbacks:**
- Problem: In `ArenaController.DualChat`, `Task.WhenAll` is used, but if both models fail and trigger the Groq fallback, they might hit the Groq API concurrently and hit rate limits, or delay the response significantly.
- Files: `src/DualMind.API/Controllers/Api/ArenaController.cs`
- Cause: Fallback logic is executed within the individual task execution path.
- Improvement path: Optimize fallback routing and consider short-circuiting if the primary provider is known to be down globally.

## Fragile Areas

**Supabase PostgREST String Filters:**
- Files: Throughout `src/DualMind.API/Core/Services/*` (e.g., `$"comparison_id=eq.{comparisonId}"`)
- Why fragile: String interpolation for query filters is brittle and susceptible to syntax errors if IDs contain unexpected characters (though GUIDs are generally safe, string IDs would be dangerous).
- Safe modification: Use a typed query builder or ensure rigorous validation of all inputs before interpolating them into PostgREST filter strings.
- Test coverage: Low. Most services lack comprehensive unit tests mocking these specific string filters.

**Provider Key Rotation:**
- Files: `src/DualMind.API/Core/Services/ProviderConfigService.cs`
- Why fragile: The key rotation and selection logic relies heavily on an in-memory cache `_keysCache` combined with database updates. Concurrent requests across multiple scaled instances (e.g., in Azure App Service) will have disjointed caches and might select the same key, leading to rate limit spikes on a single key despite "rotation".
- Safe modification: Move key rate limiting and rotation tracking to a distributed cache (like Redis) or rely more strictly on database atomic operations for key checkout.

## Scaling Limits

**In-Memory Caching:**
- Current capacity: Single instance.
- Limit: As the application scales horizontally in Azure App Service, the `IMemoryCache` in `ModelSelector` and the static dictionaries in `ProviderConfigService` will cause inconsistent states between instances (e.g., one instance thinks a provider key is rate-limited, another doesn't).
- Scaling path: Migrate from `IMemoryCache` to a distributed cache like Redis (`IDistributedCache`) for model definitions, provider configs, and rate limit tracking.

## Test Coverage Gaps

**Core Logic & Fallbacks:**
- What's not tested: The complex provider fallback logic, rate limit handling, and key rotation in `GroqService` and `ArenaController`.
- Files: `src/DualMind.API/AI/Providers/GroqService.cs`, `src/DualMind.API/Controllers/Api/ArenaController.cs`
- Risk: Changes to provider APIs or error codes might break the fallback chain silently, resulting in degraded user experience.
- Priority: High

---

*Concerns audit: 2026-02-27*