-- Deactivate Cloudflare Workers AI models that failed runtime smoke tests
-- on March 19, 2026 against the local /api/arena/chat integration.
--
-- These models currently fall back to the basic Groq model instead of
-- returning a native Cloudflare Workers AI response, so they should not
-- remain active in production selection until re-verified.

begin;

update ai_models
set
    status = 'inactive'
where lower(provider_name) = 'cloudflare'
  and lower(model_name) in (
    lower('@cf/meta/llama-3.2-11b-vision-instruct'),
    lower('@cf/google/gemma-7b-it-lora'),
    lower('@cf/meta-llama/llama-2-7b-chat-hf-lora'),
    lower('@hf/google/gemma-7b-it'),
    lower('@hf/nousresearch/hermes-2-pro-mistral-7b'),
    lower('@cf/microsoft/phi-2'),
    lower('@cf/meta/llama-2-7b-chat-fp16')
  );

commit;
