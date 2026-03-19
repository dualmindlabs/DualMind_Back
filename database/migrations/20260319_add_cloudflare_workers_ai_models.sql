-- Adds Cloudflare Workers AI as a first-class provider and seeds the
-- current text-generation model catalog from Cloudflare Workers AI docs.
--
-- Sources used for model IDs:
-- https://developers.cloudflare.com/workers-ai/models/
-- https://developers.cloudflare.com/ai-gateway/usage/providers/workersai/
--
-- Notes:
-- - This seeds only text-generation/chat-capable Workers AI models that are
--   currently listed in the public catalog.
-- - Model names are stored exactly as the official Workers AI model IDs.
-- - `llama-3.2-11b-vision-instruct` is included because it supports chat
--   completions, but Cloudflare documents an extra one-time license acceptance
--   step before normal use.

begin;

update providers
set
    display_name = 'Cloudflare Workers AI',
    is_enabled = true,
    priority = 30,
    updated_at = now()
where lower(provider_name) = 'cloudflare';

insert into providers (
    provider_name,
    display_name,
    is_enabled,
    priority,
    created_at,
    updated_at
)
select
    'cloudflare',
    'Cloudflare Workers AI',
    true,
    30,
    now(),
    now()
where not exists (
    select 1
    from providers
    where lower(provider_name) = 'cloudflare'
);

with desired_models(model_name, display_name) as (
    values
        ('@cf/openai/gpt-oss-120b', 'gpt-oss-120b'),
        ('@cf/openai/gpt-oss-20b', 'gpt-oss-20b'),
        ('@cf/meta/llama-4-scout-17b-16e-instruct', 'llama-4-scout-17b-16e-instruct'),
        ('@cf/meta/llama-3.1-8b-instruct-fast', 'llama-3.1-8b-instruct-fast'),
        ('@cf/nvidia/nemotron-3-120b-a12b', 'nemotron-3-120b-a12b'),
        ('@cf/zai-org/glm-4.7-flash', 'glm-4.7-flash'),
        ('@cf/ibm-granite/granite-4.0-h-micro', 'granite-4.0-h-micro'),
        ('@cf/aisingapore/gemma-sea-lion-v4-27b-it', 'gemma-sea-lion-v4-27b-it'),
        ('@cf/qwen/qwen3-30b-a3b-fp8', 'qwen3-30b-a3b-fp8'),
        ('@cf/google/gemma-3-12b-it', 'gemma-3-12b-it'),
        ('@cf/mistralai/mistral-small-3.1-24b-instruct', 'mistral-small-3.1-24b-instruct'),
        ('@cf/qwen/qwq-32b', 'qwq-32b'),
        ('@cf/qwen/qwen2.5-coder-32b-instruct', 'qwen2.5-coder-32b-instruct'),
        ('@cf/meta/llama-guard-3-8b', 'llama-guard-3-8b'),
        ('@cf/deepseek-ai/deepseek-r1-distill-qwen-32b', 'deepseek-r1-distill-qwen-32b'),
        ('@cf/meta/llama-3.3-70b-instruct-fp8-fast', 'llama-3.3-70b-instruct-fp8-fast'),
        ('@cf/meta/llama-3.2-1b-instruct', 'llama-3.2-1b-instruct'),
        ('@cf/meta/llama-3.2-3b-instruct', 'llama-3.2-3b-instruct'),
        ('@cf/meta/llama-3.2-11b-vision-instruct', 'llama-3.2-11b-vision-instruct'),
        ('@cf/meta/llama-3.1-8b-instruct-awq', 'llama-3.1-8b-instruct-awq'),
        ('@cf/meta/llama-3.1-8b-instruct-fp8', 'llama-3.1-8b-instruct-fp8'),
        ('@cf/meta/llama-3.1-8b-instruct', 'llama-3.1-8b-instruct'),
        ('@hf/meta-llama/meta-llama-3-8b-instruct', 'meta-llama-3-8b-instruct'),
        ('@cf/meta/llama-3-8b-instruct-awq', 'llama-3-8b-instruct-awq'),
        ('@cf/meta/llama-3-8b-instruct', 'llama-3-8b-instruct'),
        ('@hf/mistral/mistral-7b-instruct-v0.2', 'mistral-7b-instruct-v0.2'),
        ('@cf/google/gemma-7b-it-lora', 'gemma-7b-it-lora'),
        ('@cf/google/gemma-2b-it-lora', 'gemma-2b-it-lora'),
        ('@cf/meta-llama/llama-2-7b-chat-hf-lora', 'llama-2-7b-chat-hf-lora'),
        ('@hf/google/gemma-7b-it', 'gemma-7b-it'),
        ('@hf/nousresearch/hermes-2-pro-mistral-7b', 'hermes-2-pro-mistral-7b'),
        ('@cf/mistral/mistral-7b-instruct-v0.2-lora', 'mistral-7b-instruct-v0.2-lora'),
        ('@cf/defog/sqlcoder-7b-2', 'sqlcoder-7b-2'),
        ('@cf/microsoft/phi-2', 'phi-2'),
        ('@cf/meta/llama-2-7b-chat-fp16', 'llama-2-7b-chat-fp16'),
        ('@cf/mistral/mistral-7b-instruct-v0.1', 'mistral-7b-instruct-v0.1'),
        ('@cf/meta/llama-2-7b-chat-int8', 'llama-2-7b-chat-int8'),
        ('@cf/meta/llama-3.1-70b-instruct', 'llama-3.1-70b-instruct')
),
updated as (
    update ai_models as target
    set
        display_name = desired.display_name,
        provider_name = 'cloudflare',
        is_free = false,
        status = 'active'
    from desired_models as desired
    where lower(target.model_name) = lower(desired.model_name)
    returning lower(target.model_name) as model_name
)
insert into ai_models (
    model_id,
    model_name,
    display_name,
    provider_name,
    is_free,
    status,
    created_at
)
select
    gen_random_uuid(),
    desired.model_name,
    desired.display_name,
    'cloudflare',
    false,
    'active',
    now()
from desired_models as desired
where not exists (
    select 1
    from updated
    where updated.model_name = lower(desired.model_name)
)
and not exists (
    select 1
    from ai_models as existing
    where lower(existing.model_name) = lower(desired.model_name)
);

commit;
