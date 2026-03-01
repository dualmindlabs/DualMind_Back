-- =============================================================================
-- DualMind Arena — ORIGINAL SCHEMA v1 (backed up before v2 migration)
-- Saved: 2026-02-26
-- This is the OLD schema — DO NOT RUN. For reference only.
-- =============================================================================

CREATE TABLE public.admins (
  user_id uuid NOT NULL,
  created_at timestamp without time zone DEFAULT now(),
  CONSTRAINT admins_pkey PRIMARY KEY (user_id),
  CONSTRAINT admins_user_id_fkey FOREIGN KEY (user_id) REFERENCES auth.users(id)
);

CREATE TABLE public.ai_messages (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  session_id uuid NOT NULL,
  prompt text NOT NULL,
  system_prompt text,
  model_name character varying NOT NULL,
  agent_role character varying NOT NULL,
  message text NOT NULL,
  prompt_tokens integer DEFAULT 0,
  completion_tokens integer DEFAULT 0,
  total_tokens integer DEFAULT 0,
  selection_mode character varying DEFAULT 'automatic'::character varying,
  created_by uuid,
  created_at timestamp with time zone DEFAULT timezone('utc'::text, now()),
  updated_at timestamp with time zone DEFAULT timezone('utc'::text, now()),
  CONSTRAINT ai_messages_pkey PRIMARY KEY (id),
  CONSTRAINT ai_messages_created_by_fkey FOREIGN KEY (created_by) REFERENCES public.users(user_id)
);

CREATE TABLE public.ai_models (
  model_id uuid NOT NULL DEFAULT gen_random_uuid(),
  model_name character varying NOT NULL,
  provider_name character varying,
  api_url text NOT NULL,
  description text,
  status character varying DEFAULT 'active'::character varying,
  created_by uuid,
  created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
  updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT ai_models_pkey PRIMARY KEY (model_id),
  CONSTRAINT ai_models_created_by_fkey FOREIGN KEY (created_by) REFERENCES public.users(user_id)
);

CREATE TABLE public.chat_sessions (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid,
  model_type character varying NOT NULL,
  _3d_chat_config jsonb DEFAULT '{"bubble_style": "glass", "animation_type": "float", "particle_density": 50}'::jsonb,
  started_at timestamp with time zone DEFAULT now(),
  last_activity timestamp with time zone DEFAULT now(),
  CONSTRAINT chat_sessions_pkey PRIMARY KEY (id),
  CONSTRAINT chat_sessions_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(user_id)
);

CREATE TABLE public.comparisons (
  comparison_id uuid NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid,
  prompt_text text NOT NULL,
  model1_id uuid,
  model2_id uuid,
  model1_response text,
  model2_response text,
  model1_time_ms integer,
  model2_time_ms integer,
  created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT comparisons_pkey PRIMARY KEY (comparison_id),
  CONSTRAINT comparisons_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(user_id),
  CONSTRAINT comparisons_model1_id_fkey FOREIGN KEY (model1_id) REFERENCES public.ai_models(model_id),
  CONSTRAINT comparisons_model2_id_fkey FOREIGN KEY (model2_id) REFERENCES public.ai_models(model_id)
);

CREATE TABLE public.messages (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  session_id uuid,
  content text NOT NULL,
  is_ai boolean DEFAULT false,
  _3d_animation_data jsonb DEFAULT '{"exit": "fade_out", "entrance": "fade_in", "float_intensity": 0.5}'::jsonb,
  created_at timestamp with time zone DEFAULT now(),
  CONSTRAINT messages_pkey PRIMARY KEY (id),
  CONSTRAINT messages_session_id_fkey FOREIGN KEY (session_id) REFERENCES public.chat_sessions(id)
);

CREATE TABLE public.model_votes (
  vote_id uuid NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid,
  comparison_id uuid,
  winner_model_id uuid,
  created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
  vote_choice character varying CHECK (vote_choice::text = ANY (ARRAY['left'::character varying, 'right'::character varying, 'tie'::character varying, 'both-bad'::character varying]::text[])),
  CONSTRAINT model_votes_pkey PRIMARY KEY (vote_id),
  CONSTRAINT model_votes_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(user_id),
  CONSTRAINT model_votes_comparison_id_fkey FOREIGN KEY (comparison_id) REFERENCES public.comparisons(comparison_id),
  CONSTRAINT model_votes_winner_model_id_fkey FOREIGN KEY (winner_model_id) REFERENCES public.ai_models(model_id)
);

CREATE TABLE public.provider_api_keys (
  key_id uuid NOT NULL DEFAULT uuid_generate_v4(),
  provider_name text,
  encrypted_api_key text NOT NULL,
  display_mask text NOT NULL,
  is_active boolean DEFAULT true,
  failure_count integer DEFAULT 0,
  total_calls integer DEFAULT 0,
  last_used_at timestamp with time zone,
  last_error_type text,
  last_error_category text,
  cooldown_until timestamp with time zone,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  created_by uuid,
  CONSTRAINT provider_api_keys_pkey PRIMARY KEY (key_id),
  CONSTRAINT provider_api_keys_provider_name_fkey FOREIGN KEY (provider_name) REFERENCES public.providers(provider_name)
);

CREATE TABLE public.providers (
  provider_name text NOT NULL,
  display_name text NOT NULL,
  is_enabled boolean DEFAULT true,
  priority integer DEFAULT 0,
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT providers_pkey PRIMARY KEY (provider_name)
);

CREATE TABLE public.system_settings (
  key text NOT NULL,
  is_enabled boolean DEFAULT false,
  description text,
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT system_settings_pkey PRIMARY KEY (key)
);

CREATE TABLE public.thread_messages (
  message_id uuid NOT NULL DEFAULT gen_random_uuid(),
  thread_id uuid,
  prompt_text text NOT NULL,
  model1_id uuid,
  model2_id uuid,
  model1_response text,
  model2_response text,
  model1_time_ms integer,
  model2_time_ms integer,
  created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
  comparison_id uuid,
  requesting_user_id uuid,
  CONSTRAINT thread_messages_pkey PRIMARY KEY (message_id),
  CONSTRAINT thread_messages_thread_id_fkey FOREIGN KEY (thread_id) REFERENCES public.threads(thread_id),
  CONSTRAINT thread_messages_model1_id_fkey FOREIGN KEY (model1_id) REFERENCES public.ai_models(model_id),
  CONSTRAINT thread_messages_model2_id_fkey FOREIGN KEY (model2_id) REFERENCES public.ai_models(model_id),
  CONSTRAINT thread_messages_comparison_id_fkey FOREIGN KEY (comparison_id) REFERENCES public.comparisons(comparison_id),
  CONSTRAINT thread_messages_requesting_user_id_fkey FOREIGN KEY (requesting_user_id) REFERENCES public.users(user_id)
);

CREATE TABLE public.threads (
  thread_id uuid NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid,
  title character varying,
  created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
  visibility character varying DEFAULT 'private'::character varying CHECK (visibility::text = ANY (ARRAY['private'::character varying, 'public'::character varying, 'unlisted'::character varying]::text[])),
  CONSTRAINT threads_pkey PRIMARY KEY (thread_id),
  CONSTRAINT threads_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(user_id)
);

CREATE TABLE public.user_preferences (
  id uuid NOT NULL DEFAULT gen_random_uuid(),
  user_id uuid,
  _3d_settings jsonb DEFAULT '{"model_colors": ["#7C3AED", "#06B6D4"], "particle_density": 100, "animation_quality": "high"}'::jsonb,
  reduce_motion boolean DEFAULT false,
  theme_preference character varying DEFAULT 'dark'::character varying CHECK (theme_preference::text = ANY (ARRAY['dark'::character varying, 'light'::character varying, 'auto'::character varying]::text[])),
  animation_speed double precision DEFAULT 1.0 CHECK (animation_speed >= 0.1::double precision AND animation_speed <= 2.0::double precision),
  created_at timestamp with time zone DEFAULT now(),
  updated_at timestamp with time zone DEFAULT now(),
  CONSTRAINT user_preferences_pkey PRIMARY KEY (id),
  CONSTRAINT user_preferences_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(user_id)
);

CREATE TABLE public.users (
  user_id uuid NOT NULL DEFAULT gen_random_uuid(),
  full_name character varying,
  email character varying,
  role character varying DEFAULT 'user'::character varying,
  created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
  last_login_at timestamp without time zone,
  CONSTRAINT users_pkey PRIMARY KEY (user_id),
  CONSTRAINT users_auth_fk FOREIGN KEY (user_id) REFERENCES auth.users(id)
);
