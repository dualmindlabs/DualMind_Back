-- =============================================================================
-- SEED LEADERBOARD FOR CLOUDFLARE WORKERS AI
--
-- This script initializes the 'model_leaderboard' table for all active 
-- Cloudflare models that don't already have an entry.
--
-- Note: Initial ELO scores are randomized between 950 and 1050 to give a 
-- realistic starting spread as requested.
-- =============================================================================

BEGIN;

DO $$
BEGIN
    -- Informational notice
    RAISE NOTICE 'Seeding model_leaderboard for Cloudflare provider...';

    -- Insert missing entries for all 'cloudflare' models in 'ai_models'
    -- Using explicit column list and ON CONFLICT for safety
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
        (950 + (random() * 100))::INTEGER as initial_elo, -- Random score between 950 and 1050
        0 as total_wins,
        0 as total_losses,
        0 as total_ties,
        0 as total_comparisons,
        now() as updated_at
    FROM public.ai_models m
    WHERE m.provider_name = 'cloudflare'
      AND m.status = 'active'
      -- Only insert if they don't already exist in the leaderboard
      AND NOT EXISTS (
          SELECT 1 
          FROM public.model_leaderboard ml 
          WHERE ml.model_id = m.model_id
      )
    ON CONFLICT (model_id) DO NOTHING;

    RAISE NOTICE 'Cloudflare leaderboard seeding complete.';
END $$;

COMMIT;
