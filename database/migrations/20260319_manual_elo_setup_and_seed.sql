-- =============================================================================
-- DUALMIND ELO SYSTEM & MANUAL CLOUDFLARE MODEL SEEDING
-- =============================================================================
-- This script:
-- 1. Ensures the ELO rating infrastructure exists (table, function, view)
-- 2. Manually seeds the specific list of Cloudflare models with randomized ELOs
-- =============================================================================

BEGIN;

-- 1. Ensure the leaderboard table exists
CREATE TABLE IF NOT EXISTS public.model_leaderboard (
    model_id uuid PRIMARY KEY REFERENCES public.ai_models(model_id) ON DELETE CASCADE,
    elo_score integer DEFAULT 1000 NOT NULL,
    total_wins integer DEFAULT 0,
    total_losses integer DEFAULT 0,
    total_ties integer DEFAULT 0,
    total_comparisons integer DEFAULT 0,
    updated_at timestamp with time zone DEFAULT now()
);

-- 2. Create the ELO update helper function (the "ELO rating query")
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

-- 3. Create OR REPLACE the view used by the backend service
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

-- 4. MANUALLY SEED the specific Cloudflare models you provided
-- Initial scores are randomized between 950 and 1100 to give them a baseline.
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
    (950 + (random() * 150))::INTEGER as initial_elo,
    0, 0, 0, 0, now()
FROM public.ai_models m
WHERE m.model_name IN (
    '@cf/meta/llama-3.1-8b-instruct-fast',
    '@cf/nvidia/nemotron-3-120b-a12b',
    '@cf/zai-org/glm-4.7-flash',
    '@cf/ibm-granite/granite-4.0-h-micro',
    '@cf/aisingapore/gemma-sea-lion-v4-27b-it',
    '@cf/qwen/qwen3-30b-a3b-fp8',
    '@cf/mistralai/mistral-small-3.1-24b-instruct',
    '@cf/qwen/qwq-32b',
    '@cf/qwen/qwen2.5-coder-32b-instruct',
    '@cf/meta/llama-guard-3-8b',
    '@cf/deepseek-ai/deepseek-r1-distill-qwen-32b',
    '@cf/meta/llama-3.3-70b-instruct-fp8-fast',
    '@cf/meta/llama-3.2-1b-instruct',
    '@cf/meta/llama-3.2-3b-instruct',
    '@cf/meta/llama-3.2-11b-vision-instruct',
    '@cf/meta/llama-3.1-8b-instruct-awq',
    '@cf/meta/llama-3.1-8b-instruct-fp8',
    '@cf/meta/llama-3.1-8b-instruct',
    '@hf/meta-llama/meta-llama-3-8b-instruct',
    '@cf/meta/llama-3-8b-instruct-awq',
    '@cf/meta/llama-3-8b-instruct',
    '@hf/mistral/mistral-7b-instruct-v0.2',
    '@cf/google/gemma-7b-it-lora',
    '@cf/google/gemma-2b-it-lora',
    '@cf/meta-llama/llama-2-7b-chat-hf-lora',
    '@hf/google/gemma-7b-it',
    '@hf/nousresearch/hermes-2-pro-mistral-7b',
    '@cf/mistral/mistral-7b-instruct-v0.2-lora',
    '@cf/defog/sqlcoder-7b-2',
    '@cf/microsoft/phi-2',
    '@cf/meta/llama-2-7b-chat-fp16',
    '@cf/mistral/mistral-7b-instruct-v0.1',
    '@cf/meta/llama-2-7b-chat-int8',
    '@cf/meta/llama-3.1-70b-instruct'
)
-- Only seed if they don't already have an entry
-- If you want to force-overwrite, change 'DO NOTHING' to 'DO UPDATE'
ON CONFLICT (model_id) DO NOTHING;

COMMIT;
