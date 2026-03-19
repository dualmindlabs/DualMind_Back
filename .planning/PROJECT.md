# DualMind Arena Backend

## What This Is

The backend infrastructure for DualMind Arena, a platform that benchmarks AI models based on human blind voting. It serves as the core API for chat completions, blind battles, gamified energy wagers, leaderboard tracking, and community threads, using ASP.NET Core and Supabase.

## Core Value

Accurately evaluating and ranking AI models through impartial, double-blind human voting while keeping users engaged via a gamified energy system.

## Requirements

### Validated

- ✓ **Core Chat** — Users can chat with multiple AI models simultaneously (Core endpoints)
- ✓ **Blind Battles** — Models generate side-by-side responses and users vote on the best one
- ✓ **Gamified Energy** — Energy consumption per battle, wagers on the community favorite, and daily/video refill mechanics
- ✓ **Leaderboards** — Elo rankings and model win-rates based on historical blind votes
- ✓ **Thread Management** — Users can save, share, and manage privacy (public/unlisted/private) of their AI battles
- ✓ **Provider Rotation** — Load balancing and failover across multiple AI providers (Groq) to prevent exhaustion
- ✓ **Admin Controls** — Admin API endpoints for managing models, keys, users, threads, and dashboard statistics

### Active

- [ ] (No new requirements defined yet — currently mapping existing state)

### Out of Scope

- [Direct Database Access] — Use Supabase PostgREST layer for all data transactions.
- [EF Core] — Not used in this project per architecture design.

## Context

- **Tech Stack:** ASP.NET Core Web API, Supabase PostgREST for data access.
- **AI Gateway:** Custom ChatProviderFactory routing requests currently to Groq.
- **Security:** JWT authentication via Supabase, specific `[Authorize]` attributes on endpoints.
- **Recent Work:** Implemented "High Stakes" Energy Wagering to let users bet on community favorite models in blind battles.

## Constraints

- **Architecture**: Must use `ISupabaseService` for general queries and `IAdminSupabaseClient` for admin tasks. No EF Core.
- **Provider Reliability**: Must implement automatic failovers when provider limits are reached.
- **Race Conditions**: Energy transactions (wagers, battle consumption) must use atomic operations (RPCs) to prevent TOCTOU bugs.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Route AI through custom gateway | Enables easy fallback and load balancing between keys/providers | ✓ Good |
| Supabase for DB | Fast deployment, built-in auth, easy REST querying | ✓ Good |
| Post-vote wager credits | Prevents double-voting exploits by inserting DB rows before rewarding energy | ✓ Good |

---
*Last updated: 2026-03-02 after initialization from existing codebase*