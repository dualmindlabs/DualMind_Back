# DualMind Backend

.NET Web API for DualMind Arena – AI model comparison and chat platform.

## Overview

- **User APIs**: Arena chat, model listings, thread management, speech synthesis.
- **Admin APIs**: Dashboard stats, user/model/thread CRUD.
- **Auth**: Supabase JWT tokens.
- **External**: Supabase (data), Groq (AI/speech).

## Setup

### Prerequisites
- .NET Framework 4.8
- Visual Studio 2019+
- Supabase project
- Groq API key (optional)

### Environment Variables
Set in `.env` (local) or Azure App Settings (prod):

- `SUPABASE_URL`: https://your-project.supabase.co
- `SUPABASE_SERVICE_KEY`: Service role key
- `GROQ_API_KEY`: For AI/speech (optional)

### Build & Run
1. Clone repo.
2. Set env vars in `.env`.
3. Open `DualMind_Back.sln` in VS.
4. Build → Rebuild.
5. Run (IIS Express).

## Endpoints

### User APIs
- `GET /api/models`: List AI models.
- `GET /api/threads?userId=X&limit=Y`: Get user threads.
- `POST /api/threads`: Create thread.
- `POST /api/arena/dualchat`: Chat with models.
- `POST /api/speech/generate`: Generate speech.

### Admin APIs
- `GET /api/admin/dashboard/stats`: Dashboard stats.
- `GET /api/admin/users`: List users.
- `GET /api/admin/models`: List models.
- `POST /api/admin/users`: Create user.
- etc. (full CRUD for users, models, threads, votes, comparisons).

All admin endpoints require auth.

## Deployment
- Publish to Azure App Service.
- Set env vars in Azure.
- Redeploy after changes.

## Improvements Suggested
- Add rate limiting, caching, detailed logging.
- Enhance error responses.
- Add user profiles, favorites, bulk ops.

## Additional Features
- User profiles, notifications, webhooks.
- Advanced admin: bulk actions, analytics, moderation.
