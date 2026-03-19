-- =============================================================================
-- DUALMIND ELO SYSTEM & CLOUDFARE LEADERBOARD INITIALIZATION
-- =============================================================================
-- This script ensures the ELO rating infrastructure exists and initializes
-- the leaderboard for newly added Cloudflare models.

BEGIN;

-- 1. ENSURE leaderboard table exists
CREATE TABLE IF NOT EXISTS public.model_leaderboard (
    model_id uuid PRIMARY KEY REFERENCES public.ai_models(model_id) ON DELETE CASCADE,
    elo_score integer DEFAULT 1000 NOT NULL,
    total_wins integer DEFAULT 0,
    total_losses integer DEFAULT 0,
    total_ties integer DEFAULT 0,
    total_comparisons integer DEFAULT 0,
    updated_at timestamp with time zone DEFAULT now()
);

-- 2. CREATE ELO update helper function
-- Standard Elo formula: NewRating = OldRating + K * (ActualScore - ExpectedScore)
CREATE OR REPLACE FUNCTION public.calculate_elo_update(
    winner_elo integer,
    loser_elo integer,
    is_tie boolean DEFAULT false,
    k_factor integer DEFAULT 32
) RETURNS TABLE (new_winner_elo integer, new_loser_elo integer, elo_delta integer) AS $$
DECLARE
    expected_winner float;
    expected_loser float;
    score_winner float;
    score_loser float;
    delta_w integer;
    delta_l integer;
BEGIN
    expected_winner := 1.0 / (1.0 + pow(10, (loser_elo - winner_elo)::float / 400.0));
    expected_loser := 1.0 / (1.0 + pow(10, (winner_elo - loser_elo)::float / 400.0));
    
    IF is_tie THEN
        score_winner := 0.5;
        score_loser := 0.5;
    ELSE
        score_winner := 1.0;
        score_loser := 0.0;
    END IF;

    delta_w := round(k_factor * (score_winner - expected_winner))::integer;
    delta_l := round(k_factor * (score_loser - expected_loser))::integer;

    RETURN QUERY SELECT 
        winner_elo + delta_w, 
        loser_elo + delta_l,
        delta_w;
END;
$$ LANGUAGE plpgsql;

-- 3. CREATE OR REPLACE the view referenced by the backend
CREATE OR REPLACE VIEW public.v_leaderboard AS
SELECT 
    m.model_id,
    m.model_name,
    m.display_name,
    m.provider_name,
    ml.elo_score,
    ml.total_wins,
    ml.total_losses,
    ml.total_ties,
    ml.total_comparisons,
    CASE 
        WHEN ml.total_comparisons = 0 THEN 0.0
        ELSE ROUND((ml.total_wins::float / ml.total_comparisons::float) * 100, 2)
    END as win_rate,
    RANK() OVER (ORDER BY ml.elo_score DESC) as elo_rank
FROM public.ai_models m
JOIN public.model_leaderboard ml ON m.model_id = ml.model_id
WHERE m.status = 'active';

-- 4. SEED NEW MODELS (Cloudflare only)
-- Note: As requested, we use randomized starting scores between 950 and 1050.
INSERT INTO public.model_leaderboard (
    model_id,
    elo_score,
    total_wins,
    total_losses,
    total_ties,
    total_comparisons,
    updated_at
)
SELECT 
    m.model_id,
    (950 + (random() * 100))::INTEGER as initial_elo,
    0, 0, 0, 0, now()
FROM public.ai_models m
WHERE m.provider_name = 'cloudflare'
  AND m.status = 'active'
-- Idempotency: don't overwrite existing stats if manually edited
ON CONFLICT (model_id) DO NOTHING;

COMMIT;
