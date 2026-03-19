-- =============================================================================
-- MANUAL LEADERBOARD INSERT FOR NEW CLOUDFLARE MODELS
--
-- This script explicitly targets the list of models you provided.
-- It matches them by 'model_name' to their 'model_id' in the ai_models table
-- and inserts them into 'model_leaderboard' with randomized initial ELOs.
-- =============================================================================

BEGIN;

DO $$
DECLARE
    row_count integer;
BEGIN
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
        (950 + (random() * 150))::INTEGER as initial_elo, -- Random score between 950 and 1100
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
    -- Only insert if they don't already have a stats record
    AND NOT EXISTS (
        SELECT 1 FROM public.model_leaderboard ml WHERE ml.model_id = m.model_id
    )
    ON CONFLICT (model_id) DO NOTHING;

    GET DIAGNOSTICS row_count = ROW_COUNT;
    RAISE NOTICE 'Initialized leaderboard entries for % new Cloudflare models.', row_count;
END $$;

COMMIT;
