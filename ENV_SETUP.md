# Environment Variables Setup Guide

## Local Development (.env file)

Create a `.env` file in the project root directory with the following variables:

```env
# ============ REQUIRED ============

# Supabase Configuration
SUPABASE_URL=your_supabase_url_here
SUPABASE_SERVICE_KEY=your_supabase_service_key_here

# Groq API Key (REQUIRED)
GROQ_API_KEY=your_groq_api_key_here

# ============ OPTIONAL ============

# Supabase Anon Key (optional, service key is preferred)
# SUPABASE_ANON_KEY=your_supabase_anon_key_here

# Bytez API Key (only if using Bytez provider)
# BYTEZ_API_KEY=your_bytez_api_key_here

# JWT Secret (only if using custom JWT auth)
# JWT_SECRET=your_jwt_secret_here

# App Secret (NOT NEEDED - was for database key encryption)
# APP_SECRET=not_needed
```

### Priority Order:
1. **Environment Variable (GROQ_API_KEY)** - Used first if set (from .env or Azure)
2. **Database Keys** - Used as fallback if no environment variable is set

## Azure Deployment (Azure Secrets)

When deploying to Azure, set these as **Application Settings** or **Key Vault Secrets**:

1. Go to Azure Portal → Your App Service → Configuration → Application settings
2. Add these REQUIRED settings:
   - `GROQ_API_KEY` = your_groq_api_key
   - `SUPABASE_URL` = your_supabase_url
   - `SUPABASE_SERVICE_KEY` = your_supabase_service_key

The backend will automatically use Azure environment variables when deployed.

## Important Notes:

- **Local Development**: Use `.env` file (already in .gitignore)
- **Azure Production**: Use Azure App Service Configuration/Secrets
- The `.env` file is automatically loaded on application start
- If `GROQ_API_KEY` is set, it takes priority over database keys
- If no `GROQ_API_KEY` is set, the system will use database keys (if available)

