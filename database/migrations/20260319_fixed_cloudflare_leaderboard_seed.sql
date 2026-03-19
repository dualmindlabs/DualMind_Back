-- =============================================================================
-- REVISED MANUAL CLOUDFLARE MODEL LEADERBOARD SEEDING
-- =============================================================================
-- Fixes: ERROR 42883 (round function type mismatch)
-- Provides: Manual ELO scores as requested (no random() function used)
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

-- 2. Fixed View (using numeric for round function compatibility)
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
        -- Fix: ROUND requires numeric type in Postgres
        ELSE ROUND(((ml.total_wins::numeric / ml.total_comparisons::numeric) * 100), 2)
    END as win_rate,
    RANK() OVER (ORDER BY ml.elo_score DESC) as elo_rank
FROM public.ai_models m
JOIN public.model_leaderboard ml ON m.model_id = ml.model_id
WHERE m.status = 'active';

-- 3. Manual seeding with hardcoded scores
-- We use a CTE to map the names to IDs and apply different manual scores.
WITH NewModelStats (m_name, m_elo) AS (
    VALUES 
        ('@cf/meta/llama-3.1-8b-instruct-fast',            1050),
        ('@cf/nvidia/nemotron-3-120b-a12b',                1080),
        ('@cf/zai-org/glm-4.7-flash',                      1020),
        ('@cf/ibm-granite/granite-4.0-h-micro',            1010),
        ('@cf/aisingapore/gemma-sea-lion-v4-27b-it',       1040),
        ('@cf/qwen/qwen3-30b-a3b-fp8',                     1090),
        ('@cf/mistralai/mistral-small-3.1-24b-instruct',   1070),
        ('@cf/qwen/qwq-32b',                               1110),
        ('@cf/qwen/qwen2.5-coder-32b-instruct',            1095),
        ('@cf/meta/llama-guard-3-8b',                      1000),
        ('@cf/deepseek-ai/deepseek-r1-distill-qwen-32b',   1120),
        ('@cf/meta/llama-3.3-70b-instruct-fp8-fast',       1150),
        ('@cf/meta/llama-3.2-1b-instruct',                 980),
        ('@cf/meta/llama-3.2-3b-instruct',                 1010),
        ('@cf/meta/llama-3.2-11b-vision-instruct',         1030),
        ('@cf/meta/llama-3.1-8b-instruct-awq',             1025),
        ('@cf/meta/llama-3.1-8b-instruct-fp8',             1025),
        ('@cf/meta/llama-3.1-8b-instruct',                 1020),
        ('@hf/meta-llama/meta-llama-3-8b-instruct',        1045),
        ('@cf/meta/llama-3-8b-instruct-awq',               1015),
        ('@cf/meta/llama-3-8b-instruct',                   1015),
        ('@hf/mistral/mistral-7b-instruct-v0.2',           1035),
        ('@cf/google/gemma-7b-it-lora',                    1005),
        ('@cf/google/gemma-2b-it-lora',                    975),
        ('@cf/meta-llama/llama-2-7b-chat-hf-lora',         990),
        ('@hf/google/gemma-7b-it',                         1010),
        ('@hf/nousresearch/hermes-2-pro-mistral-7b',       1030),
        ('@cf/mistral/mistral-7b-instruct-v0.2-lora',      1000),
        ('@cf/defog/sqlcoder-7b-2',                        1010),
        ('@cf/microsoft/phi-2',                            960),
        ('@cf/meta/llama-2-7b-chat-fp16',                  985),
        ('@cf/mistral/mistral-7b-instruct-v0.1',           1020),
        ('@cf/meta/llama-2-7b-chat-int8',                  980),
        ('@cf/meta/llama-3.1-70b-instruct',                1140)
)
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
    nms.m_elo,
    0, 0, 0, 0, now()
FROM public.ai_models m
JOIN NewModelStats nms ON m.model_name = nms.m_name
ON CONFLICT (model_id) DO UPDATE SET elo_score = EXCLUDED.elo_score;

COMMIT;
