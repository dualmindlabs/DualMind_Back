# Requirements: DualMind Arena

**Defined:** 2026-03-02
**Core Value:** Accurately evaluating and ranking AI models through impartial, double-blind human voting while keeping users engaged via a gamified energy system.

## v1 Requirements

### Core Battles & Voting

- ✓ **BTL-01**: User can chat with an AI and receive an anonymous response
- ✓ **BTL-02**: User can participate in Blind Battles (two anonymous models compete side-by-side)
- ✓ **BTL-03**: User can submit a vote ("left", "right", "tie", "both bad") on a Blind Battle
- ✓ **BTL-04**: Database securely records human blind votes uniquely via `model_votes`

### Energy System & Gamification

- ✓ **NRG-01**: User consumes energy (e.g., 3 units) per battle via atomic DB operations
- ✓ **NRG-02**: User receives a daily energy refill when logging in on a new UTC day
- ✓ **NRG-03**: User can claim a one-time energy bonus by watching a demo video
- ✓ **NRG-04**: User can wager their energy balance on the community favorite in a Blind Battle
- ✓ **NRG-05**: Wager wins double the bet amount, wager losses subtract the bet amount

### Leaderboards & Models

- ✓ **LDR-01**: System calculates and serves global model Win Rates and Elo Scores (`v_leaderboard`)
- ✓ **LDR-02**: "Topper Mode" automatically pairs the highest win-rate model with a random model
- ✓ **LDR-03**: Active models are randomly selected from an in-memory 5-minute cache of the database

### User Threads & Context

- ✓ **THD-01**: Thread messages store denormalized model names, positional ordering, and generation times
- ✓ **THD-02**: Users can toggle thread visibility between private, public, and unlisted
- ✓ **THD-03**: Public/unlisted threads can be viewed anonymously if the `public_sharing` feature flag is enabled

### Providers & Fallbacks

- ✓ **PRV-01**: System routes chat requests dynamically via a `ChatProviderFactory`
- ✓ **PRV-02**: System automatically fails over to a fallback model if the requested provider is exhausted or times out
- ✓ **PRV-03**: Database tracks provider keys, rotation, and cool-down periods

## v2 Requirements

*(No deferred requirements defined yet)*

## Out of Scope

| Feature | Reason |
|---------|--------|
| EF Core (Entity Framework) | Performance overhead and architectural decision to use Supabase PostgREST layer |
| Direct raw SQL in the API | Security and architectural constraint; use PostgREST or dedicated RPCs |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| BTL-01 | Phase 1 | Complete |
| BTL-02 | Phase 1 | Complete |
| BTL-03 | Phase 1 | Complete |
| BTL-04 | Phase 1 | Complete |
| NRG-01 | Phase 1 | Complete |
| NRG-02 | Phase 1 | Complete |
| NRG-03 | Phase 1 | Complete |
| NRG-04 | Phase 1 | Complete |
| NRG-05 | Phase 1 | Complete |
| LDR-01 | Phase 1 | Complete |
| LDR-02 | Phase 1 | Complete |
| LDR-03 | Phase 1 | Complete |
| THD-01 | Phase 1 | Complete |
| THD-02 | Phase 1 | Complete |
| THD-03 | Phase 1 | Complete |
| PRV-01 | Phase 1 | Complete |
| PRV-02 | Phase 1 | Complete |
| PRV-03 | Phase 1 | Complete |

**Coverage:**
- v1 requirements: 18 total
- Mapped to phases: 18
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-02*
*Last updated: 2026-03-02 after initial definition*