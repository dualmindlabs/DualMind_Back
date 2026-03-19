# DualMind_Back — AGENTS.md

Guidance for Codex when working in this ASP.NET Core backend repository.
Keep this file accurate — update it whenever schema, patterns, or architecture changes.

---

## Commands

```bash
dotnet build src/DualMind.API/DualMind.API.csproj

dotnet run --project src/DualMind.API/DualMind.API.csproj

dotnet test
```

Local run profile defaults to `http://localhost:5079` (`src/DualMind.API/Properties/launchSettings.json`).

**Deployment**: Azure App Service via GitHub Actions on push to `master` (`.github/workflows/master_dualmind-arena.yml`).
**Backend URL**: `https://dualmind-arena-cgh0cvdfhkbgatba.uaenorth-01.azurewebsites.net`

---

## Architecture

**ASP.NET Core Web API** + **Supabase PostgREST** (no EF Core, no direct SQL in app layer).

### Data Access Layers

- `ISupabaseService` / `SupabaseService` (`src/DualMind.API/Infrastructure/Data/SupabaseService.cs`)
  Main app queries (typed JSON DTO/JObject wrappers).
- `IAdminSupabaseClient` / `AdminSupabaseClient` (`src/DualMind.API/Infrastructure/Data/AdminSupabaseClient.cs`)
  Admin panel CRUD/query client (raw JSON/string and `HttpResponseMessage` style methods).

### Request Flow

```text
Client → Controller → Core Service → SupabaseService/AdminSupabaseClient → Supabase PostgREST → PostgreSQL
```

---

## AI Runtime (Current)

### Active provider stack

- Only **GroqService** is active (`src/DualMind.API/AI/Providers/GroqService.cs`).
- `BytezService` has been removed.
- `ChatProviderFactory` currently routes all provider requests to Groq (`src/DualMind.API/AI/Gateway/ChatProviderFactory.cs`).

### Capabilities

- Chat completions: `/openai/v1/chat/completions`
- Streaming: SSE (`SupportsStreaming = true`)
- Speech generation: `/openai/v1/audio/speech` via `GroqService.GenerateSpeechAsync`

### API key behavior

Priority order in `GroqService`:
1. `GROQ_API_KEY` from env (`EnvConfig.GroqApiKey`)
2. DB key rotation through `IProviderConfigService.GetNextKeyAsync("groq")`

### Startup behavior

On app start, model cache warm-up runs in background by calling `IModelSelector.GetRandomModelAsync()` (`Program.cs`).

---

## Key Files Map

| File | Purpose |
|------|---------|
| `src/DualMind.API/Controllers/Api/ArenaController.cs` | Main arena APIs (`/api/arena/chat`, `/dualchat`, `/chat/stream`, ping/test) |
| `src/DualMind.API/Controllers/Api/BlindBattleController.cs` | Blind battle endpoint (`/api/arena/blind-battle`) |
| `src/DualMind.API/Controllers/VotesController.cs` | Vote submit + leaderboard read |
| `src/DualMind.API/Controllers/ModelsController.cs` | Authenticated active model list (`/api/models`) |
| `src/DualMind.API/Controllers/ThreadsController.cs` | Thread CRUD, messages, visibility/public sharing access logic |
| `src/DualMind.API/Controllers/SettingsController.cs` | Feature flag read endpoint |
| `src/DualMind.API/Controllers/SpeechController.cs` | TTS generation endpoint |
| `src/DualMind.API/Controllers/Api/EnergyController.cs` | Energy balance and video claim endpoints |
| `src/DualMind.API/Core/Services/ModelSelector.cs` | Active model cache + random model/pair selection |
| `src/DualMind.API/Core/Services/LeaderboardModelSelector.cs` | Topper-vs-random pairing strategy |
| `src/DualMind.API/Core/Services/ModelStatsService.cs` | Reads `v_leaderboard`, writes votes, reveal flow |
| `src/DualMind.API/Core/Services/WagerService.cs` | High stakes energy wagering and result calculation |
| `src/DualMind.API/Core/Services/ThreadMessagesService.cs` | Thread message persistence + vote state hydration |
| `src/DualMind.API/Core/Services/ThreadsService.cs` | Thread CRUD + visibility updates |
| `src/DualMind.API/Core/Services/ProviderConfigService.cs` | Provider/key cache, key rotation and cooldown tracking |
| `src/DualMind.API/Core/Services/UserSyncService.cs` | Upserts users into `public.users` |
| `src/DualMind.API/Core/Services/EnergyService.cs` | Handles gamified energy state, RPC additions, and battle consumption |
| `src/DualMind.API/Infrastructure/Data/SupabaseService.cs` | Generic PostgREST service client |
| `src/DualMind.API/Infrastructure/Data/AdminSupabaseClient.cs` | Admin PostgREST client |
| `src/DualMind.API/Core/Models/AdminModels.cs` | Admin-facing DTOs for users/models/comparisons/votes/threads/messages |
| `src/DualMind.API/Core/Models/ThreadModels.cs` | Thread DTOs including visibility/message_count/position/vote info |
| `src/DualMind.API/Core/Models/ProviderModels.cs` | Provider + provider key DTOs |
| `Backup/schema_v1_original.sql` | Legacy schema reference only — do not run |

---

## API Endpoints (Current)

### Public + arena/user/thread APIs

| Method | Path | Auth |
|---|---|---|
| GET | `/health` | AllowAnonymous |
| GET | `/api/health` | AllowAnonymous |
| GET | `/api/ping` | No `[Authorize]` on controller |
| GET | `/api/ping/health` | No `[Authorize]` on controller |
| GET | `/api/arena/ping` | AllowAnonymous |
| GET | `/api/arena/test` | Requires auth (`ArenaController` has `[Authorize]`) |
| POST | `/api/arena/chat` | Requires auth |
| POST | `/api/arena/dualchat` | Requires auth |
| POST | `/api/arena/chat/stream` | Requires auth |
| POST | `/api/arena/blind-battle` | No `[Authorize]` on controller |
| POST | `/api/arena/model-vote` | Requires auth |
| GET | `/api/arena/model-stats` | Requires auth |
| GET | `/api/models` | Requires auth |
| POST | `/api/users/sync` | No `[Authorize]` on controller |
| GET | `/api/threads` | Requires auth |
| POST | `/api/threads` | Requires auth |
| GET | `/api/threads/{threadId}` | AllowAnonymous + visibility/feature-flag checks |
| GET | `/api/threads/{threadId}/messages` | AllowAnonymous + visibility/feature-flag checks |
| PATCH | `/api/threads/{threadId}` | Requires auth + ownership |
| PATCH | `/api/threads/{threadId}/visibility` | Requires auth + ownership |
| DELETE | `/api/threads/{threadId}` | Requires auth + ownership |
| GET | `/api/settings/feature-flag/{key}` | AllowAnonymous |
| POST | `/api/speech/generate` | No `[Authorize]` on controller |
| GET | `/api/energy/balance` | Requires auth |
| POST | `/api/energy/claim-video` | Requires auth |
| POST | `/api/arena/wager-vote` | Requires auth |

### Admin APIs

> Admin controllers are under `src/DualMind.API/Controllers/Admin/*` with `api/admin/*` routes.
> **Note:** they currently do not enforce explicit admin auth checks in controller attributes.

| Route group | Purpose |
|---|---|
| `/api/admin/models` | AI model CRUD/search/status/active listing |
| `/api/admin/comparisons` | Comparison listing/filter/search/delete |
| `/api/admin/dashboard` | Aggregated stats, recent activity, provider/key metrics, health |
| `/api/admin/votes` | Vote listing/filter/create/delete/stats |
| `/api/admin/messages` | Thread message listing/filter/create/delete |
| `/api/admin/threads` | Thread listing/filter/create/update/delete |
| `/api/admin/users` | User listing/filter/create/update/delete/role update |
| `/api/admin/providers` + `/api/admin/keys/*` | Provider config + provider key management |

---

## Database Schema Expectations (as used by current code)

### Tables actively used

- `providers`
- `provider_api_keys`
- `ai_models`
- `users`
- `threads`
- `thread_messages`
- `comparisons`
- `model_votes`
- `system_settings`

### Views actively used

- `v_leaderboard` (model stats)
- `v_comparisons_masked` (blind-vote reads)

### Column assumptions currently hardcoded

#### `ai_models`
`model_id`, `model_name`, `display_name`, `provider_name`, `is_free`, `status`, `created_at`

#### `users`
Includes new energy system columns: `energy_balance`, `last_energy_refill_date`, `has_claimed_demo_video`

#### `threads`
`thread_id`, `user_id`, `title`, `mode`, `visibility`, `message_count`, `created_at`, `updated_at`

#### `thread_messages`
`message_id`, `thread_id`, `comparison_id`, `prompt_text`, `model1_name`, `model2_name`, `model1_response`, `model2_response`, `model1_time_ms`, `model2_time_ms`, `position`, `created_at`

#### `comparisons`
`comparison_id`, `user_id`, `prompt_text`, `model1_id`, `model2_id`, `model1_response`, `model2_response`, `model1_time_ms`, `model2_time_ms`, `is_revealed`, `created_at`

#### `model_votes`
`vote_id`, `user_id`, `comparison_id`, `winner_model_id`, `vote_choice`, `vote_duration_ms`, `voted_at`, `revealed_at`

#### `provider_api_keys` (current code expectation)
`key_id`, `provider_name`, `api_key`, `display_mask`, `is_active`, `failure_count`, `total_calls`, `last_used_at`, `cooldown_until`, `last_error_type`, `last_error_category`, `created_at`, `updated_at`

#### `system_settings`
Service currently reads `key` + `is_enabled` (with fallback parsing from `value` if present).

---

## Critical Rules & Behavior

1. **`provider_name` must be lowercase** when writing `ai_models` / provider-linked records.
2. **Blind vote read path** uses `v_comparisons_masked` before vote write.
3. **Vote timestamp column is `voted_at`** (not `created_at`).
4. **Wager flow and DB operations** must execute `InsertAsync` and `UpdateAsync` on database rows *before* adding any `AddEnergyAsync` rewards, to prevent double-voting/farming exploits on failure conditions. Queries reading comparisons for wager checks must always include `&user_id=eq.{userId}&is_revealed=eq.false`.
5. **Thread messages use denormalized model names** (`model1_name` / `model2_name`) and `position` ordering.
6. **Model selection is currently in-memory random from cached active models** (5-minute cache), not pair-stat SQL matchmaking.
7. **Topper mode** = highest `win_rate` from `v_leaderboard` + one random other model.
8. **Thread sharing logic**:
   - Feature flag key: `public_sharing`
   - Visibility values: `private`, `public`, `unlisted`
   - Anonymous access allowed for `GetThread` + `GetThreadMessages` only when sharing enabled and visibility is public/unlisted.

---

## Security/Operational Notes (Current State)

- JWT auth is configured in `Program.cs`; if `JWT_SECRET` is missing, code enables a development fallback that bypasses signature validation.
- CORS policy is currently `AllowAll`.
- Admin/provider APIs currently include endpoints that can return provider key material (`api_key`) to admin UI consumers.
- `ComparisonLogger` and admin model creation normalize `provider_name` to lowercase before write.

---

## Code Patterns

### Vote by choice (blind flow)

```csharp
var comps = await _supabase.SelectAsync<JObject>(
    "v_comparisons_masked",
    "comparison_id,model1_id,model2_id",
    $"comparison_id=eq.{comparisonId}&user_id=eq.{userId}&is_revealed=eq.false"
);

await _supabase.InsertAsync<object>("model_votes", new {
    user_id = userId,
    comparison_id = comparisonId,
    winner_model_id = winnerModelIdOrNull,
    vote_choice = voteChoice,
    vote_duration_ms = voteDurationMs,
    voted_at = DateTime.UtcNow
});

await _supabase.UpdateAsync<object>("comparisons",
    new { is_revealed = true },
    $"comparison_id=eq.{comparisonId}");

// IF wager flow -> add energy rewards ONLY HERE (after db commits succeed).
```

### Read leaderboard

```csharp
var rows = await _supabase.SelectAsync<JObject>("v_leaderboard", "*", "");
```

### Insert thread message with ordering

```csharp
var position = await GetNextPositionAsync(threadId);
await _supabase.InsertAsync<object>("thread_messages", new {
    thread_id = threadId,
    prompt_text = prompt,
    model1_name = model1,
    model2_name = model2,
    model1_response = response1,
    model2_response = response2,
    position = position
});
```
