# External Integrations

**Analysis Date:** 2026-02-27

## APIs & External Services

**AI Inference:**
- Groq - Fast AI inference API used by `src/DualMind.API/AI/Providers/GroqService.cs`.
  - SDK/Client: Direct `HttpClient` REST calls (e.g., `/openai/v1/chat/completions`).
  - Auth: `GROQ_API_KEY` (from `.env` or dynamically loaded via `provider_api_keys` table).
  - Webhooks/SSE: Supports Server-Sent Events (SSE) for streaming responses.

**Text-To-Speech (TTS):**
- Groq Speech - Audio generation API used by `src/DualMind.API/Controllers/SpeechController.cs`.
  - Endpoint: `/openai/v1/audio/speech` via `GroqService.GenerateSpeechAsync`.

## Data Storage

**Databases:**
- Supabase (PostgreSQL) - Core relational database.
  - Connection: REST API (PostgREST), configured via `SUPABASE_URL`.
  - Client: `Infrastructure/Data/SupabaseService.cs` and `Infrastructure/Data/AdminSupabaseClient.cs` using generic `HttpClient` calls (no EF Core).
  - Auth for Client: Uses `SUPABASE_SERVICE_ROLE_KEY` or `SUPABASE_KEY`.

**File Storage:**
- None detected natively in code (audio responses stream directly to client).

**Caching:**
- Memory Cache - Used for AI Model configurations and fallback selection (`Microsoft.Extensions.Caching.Memory` via `ModelSelector.cs`).

## Authentication & Identity

**Auth Provider:**
- Supabase Auth - Issues JWTs for user authentication.
  - Implementation: Validated in `Program.cs` via `Microsoft.AspNetCore.Authentication.JwtBearer`.
  - Token issuer: `{SUPABASE_URL}/auth/v1`.
  - Secret: Uses `JWT_SECRET` for HS256 validation.

## Monitoring & Observability

**Error Tracking:**
- None detected explicitly beyond core ASP.NET logging.

**Logs:**
- Standard ASP.NET `ILogger` implementations logging to Console (`Program.cs` handles correlation IDs via `X-Correlation-Id`).

## CI/CD & Deployment

**Hosting:**
- Azure App Service - Deployed via container.
- URL: `https://dualmind-arena-cgh0cvdfhkbgatba.uaenorth-01.azurewebsites.net`

**CI Pipeline:**
- GitHub Actions - Triggered on push to `master` (`.github/workflows/master_dualmind-arena.yml`).

## Environment Configuration

**Required env vars:**
- `SUPABASE_URL` - Supabase project URL
- `SUPABASE_ANON_KEY` / `SUPABASE_KEY` - Supabase anonymous client key
- `SUPABASE_SERVICE_ROLE_KEY` / `SUPABASE_SERVICE_KEY` - Supabase admin bypass key
- `GROQ_API_KEY` - Groq API Access token
- `JWT_SECRET` - Secret for parsing/validating Supabase Auth tokens
- `DEFAULT_GROQ_MODEL` - Default fallback model if cache misses (`llama-3.3-70b-versatile`)

**Secrets location:**
- Local: `.env` file via `DotNetEnv`
- Production: Azure App Service configuration

## Webhooks & Callbacks

**Incoming:**
- None detected natively.

**Outgoing:**
- External REST calls out to Supabase and Groq.
- Streams output to clients via Server-Sent Events (`/api/arena/chat/stream`).

---

*Integration audit: 2026-02-27*