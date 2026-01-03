-- =====================================================
-- RECOMMENDED INDEXES FOR OPTIMAL PERFORMANCE
-- =====================================================
-- Run these to improve query performance

-- Provider API Keys - Critical for key rotation
CREATE INDEX IF NOT EXISTS idx_provider_api_keys_provider_active 
ON provider_api_keys(provider_name, is_active) 
WHERE is_active = true;

CREATE INDEX IF NOT EXISTS idx_provider_api_keys_last_used_at 
ON provider_api_keys(last_used_at) 
WHERE is_active = true;

CREATE INDEX IF NOT EXISTS idx_provider_api_keys_cooldown 
ON provider_api_keys(cooldown_until) 
WHERE cooldown_until IS NOT NULL;

-- Providers - For sorting and filtering
CREATE INDEX IF NOT EXISTS idx_providers_is_enabled 
ON providers(is_enabled) 
WHERE is_enabled = true;

CREATE INDEX IF NOT EXISTS idx_providers_priority 
ON providers(priority DESC);

-- Comparisons - Common queries
CREATE INDEX IF NOT EXISTS idx_comparisons_user_id 
ON comparisons(user_id);

CREATE INDEX IF NOT EXISTS idx_comparisons_created_at 
ON comparisons(created_at DESC);

CREATE INDEX IF NOT EXISTS idx_comparisons_model1_model2 
ON comparisons(model1_id, model2_id);

-- Model Votes - Analytics queries
CREATE INDEX IF NOT EXISTS idx_model_votes_winner_model 
ON model_votes(winner_model_id);

CREATE INDEX IF NOT EXISTS idx_model_votes_user_id 
ON model_votes(user_id);

CREATE INDEX IF NOT EXISTS idx_model_votes_comparison_id 
ON model_votes(comparison_id);

-- Threads - User queries
CREATE INDEX IF NOT EXISTS idx_threads_user_id 
ON threads(user_id);

CREATE INDEX IF NOT EXISTS idx_threads_created_at 
ON threads(created_at DESC);

-- Thread Messages - Thread queries
CREATE INDEX IF NOT EXISTS idx_thread_messages_thread_id 
ON thread_messages(thread_id);

CREATE INDEX IF NOT EXISTS idx_thread_messages_created_at 
ON thread_messages(created_at ASC);

-- AI Models - Common lookups
CREATE INDEX IF NOT EXISTS idx_ai_models_provider_name 
ON ai_models(provider_name);

CREATE INDEX IF NOT EXISTS idx_ai_models_status 
ON ai_models(status) 
WHERE status = 'active';

-- Users - Common queries
CREATE INDEX IF NOT EXISTS idx_users_email 
ON users(email);

CREATE INDEX IF NOT EXISTS idx_users_role 
ON users(role);



