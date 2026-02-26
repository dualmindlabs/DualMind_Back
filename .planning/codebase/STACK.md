# Technology Stack

**Analysis Date:** 2026-02-27

## Languages

**Primary:**
- C# 12 - Core language for ASP.NET Core Backend

**Secondary:**
- SQL - Referenced in `Backup/schema_v1_original.sql` (legacy reference)

## Runtime

**Environment:**
- .NET 8.0 (`net8.0`)

**Package Manager:**
- NuGet
- Lockfile: missing (relies on `.csproj` versions)

## Frameworks

**Core:**
- ASP.NET Core 8.0 - Main web framework

**Testing:**
- Not strictly defined in `.csproj`, but `dotnet test` is mentioned in `CLAUDE.md`.

**Build/Dev:**
- Docker - Containerization for build and run (`Dockerfile`)

## Key Dependencies

**Critical:**
- `Microsoft.AspNetCore.Mvc.NewtonsoftJson` (8.0.2) - Core serialization for Web API.
- `Newtonsoft.Json` (13.0.4) - Used explicitly with `JObject` to handle raw JSON mappings (Supabase REST APIs).
- `DotNetEnv` (3.1.1) - Environment variable management and loading `.env` files.

**Infrastructure:**
- `Microsoft.AspNetCore.Authentication.JwtBearer` (8.0.0) - Handles auth with Supabase JWT tokens.
- `Swashbuckle.AspNetCore` (6.5.0) - Swagger UI for API documentation and testing.

## Configuration

**Environment:**
- Configured via `appsettings.json`, `appsettings.Development.json`, and `.env` files via `DotNetEnv`.
- Requires `SUPABASE_URL`, `SUPABASE_KEY` / `SUPABASE_ANON_KEY`, `SUPABASE_SERVICE_KEY`, `GROQ_API_KEY`, `JWT_SECRET`.
- Config loading wrapped in `Infrastructure/Configuration/EnvConfig.cs`.

**Build:**
- `DualMind.API.csproj` for core package definitions.

## Platform Requirements

**Development:**
- .NET 8 SDK
- Local run profile: `http://localhost:5079`

**Production:**
- Target: Azure App Service (deployed via GitHub Actions `.github/workflows/master_dualmind-arena.yml`).
- Docker container runs on `mcr.microsoft.com/dotnet/aspnet:8.0`.

---

*Stack analysis: 2026-02-27*