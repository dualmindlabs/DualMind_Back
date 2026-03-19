# Environment Variables Setup Guide

## Local Development (.env file)

Create a `.env` file in the project root directory with the following variables:

```env
# ============ REQUIRED ============

# Supabase Configuration
SUPABASE_URL=your_supabase_url_here
SUPABASE_SERVICE_KEY=your_supabase_service_key_here

# Groq API Key
# Keep this if you want /api/speech/generate to stay direct to Groq.
# Chat/streaming no longer need it when Cloudflare AI Gateway + BYOK are configured.
# GROQ_API_KEY=your_groq_api_key_here

# Google API Key (optional, if using Google directly outside Cloudflare)
# GOOGLE_API_KEY=your_google_api_key_here

# ============ OPTIONAL ============

# Supabase Anon Key (optional, service key is preferred)
# SUPABASE_ANON_KEY=your_supabase_anon_key_here

# Bytez API Key (only if using Bytez provider)
# BYTEZ_API_KEY=your_bytez_api_key_here

# JWT Secret (only if using custom JWT auth)
# JWT_SECRET=your_jwt_secret_here

# App Secret (NOT NEEDED - was for database key encryption)
# APP_SECRET=not_needed

# Cloudflare AI Gateway (required for chat/streaming)
# Groq + Google chat/streaming and Cloudflare Workers AI chat all route through the gateway.
# Internal DB model names stay unchanged for Groq/Google; Cloudflare Workers AI models should be stored
# exactly as their Workers AI model IDs (for example @cf/meta/llama-3.1-8b-instruct).
# CLOUDFLARE_AI_GATEWAY_ACCOUNT_ID=your_cloudflare_account_id
# CLOUDFLARE_AI_GATEWAY_ID=your_gateway_id

# Set to true only if Cloudflare is storing your provider keys (BYOK mode).
# In BYOK mode, the app sends CLOUDFLARE_AI_GATEWAY_TOKEN for chat requests instead of provider keys.
# Groq speech still uses the direct Groq API, so keep GROQ_API_KEY or DB provider keys available for speech.
# Chat and streaming are routed through Cloudflare AI Gateway and will fail fast if the gateway is not configured.
# CLOUDFLARE_AI_GATEWAY_USE_BYOK=false
# CLOUDFLARE_AI_GATEWAY_TOKEN=your_cloudflare_gateway_token

# Cloudflare Workers AI (required if you add provider_name = cloudflare models in ai_models)
# Use a Cloudflare API token with Workers AI Read access.
# CLOUDFLARE_WORKERS_AI_API_TOKEN=your_cloudflare_workers_ai_api_token

# Optional default used only if a Cloudflare Workers AI request reaches the provider without a model name.
# DEFAULT_CLOUDFLARE_WORKERS_AI_MODEL=@cf/meta/llama-3.1-8b-instruct
```

### Priority Order:
1. **Cloudflare AI Gateway** - Required for chat/streaming traffic
2. **Provider Env Vars / Database Keys** - Used for Groq or Google only when the gateway path still needs direct provider auth
3. **Direct Groq Speech** - `/api/speech/generate` still uses `GROQ_API_KEY` or Groq DB keys

## Azure Deployment (Azure Secrets)

When deploying to Azure, set these as **Application Settings** or **Key Vault Secrets**:

1. Go to Azure Portal → Your App Service → Configuration → Application settings
2. Add these REQUIRED settings:
   - `GROQ_API_KEY` = your_groq_api_key
   - `SUPABASE_URL` = your_supabase_url
   - `SUPABASE_SERVICE_KEY` = your_supabase_service_key

Optional AI Gateway settings:
   - `CLOUDFLARE_AI_GATEWAY_ACCOUNT_ID`
   - `CLOUDFLARE_AI_GATEWAY_ID`
   - `CLOUDFLARE_AI_GATEWAY_USE_BYOK`
   - `CLOUDFLARE_AI_GATEWAY_TOKEN`
   - `CLOUDFLARE_WORKERS_AI_API_TOKEN`
   - `DEFAULT_CLOUDFLARE_WORKERS_AI_MODEL`

The backend will automatically use Azure environment variables when deployed.

## Important Notes:

- **Local Development**: Use `.env` file (already in .gitignore)
- **Azure Production**: Use Azure App Service Configuration/Secrets
- The `.env` file is automatically loaded on application start
- Chat and streaming require Cloudflare AI Gateway to be configured
- Groq and Google model names stay unchanged in the database; the backend maps them to Cloudflare-compatible names at request time
- Cloudflare Workers AI model names should be stored exactly as the official Workers AI model IDs
- If Cloudflare Workers AI models are active, set `CLOUDFLARE_WORKERS_AI_API_TOKEN`
- If Groq speech is enabled, keep `GROQ_API_KEY` or active Groq DB keys available

