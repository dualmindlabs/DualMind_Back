# DualMind Admin Panel AI Handoff

Last updated: 2026-03-28
Source of truth: backend code under `src/DualMind.API`

## 1. Purpose

Use this document to generate an admin panel that matches the current backend.

Rule:
- Trust this file and the live backend routes.
- Do not trust older API docs blindly.
- Do not invent endpoints.

## 2. Auth and Access

- Frontend signs in with Supabase and sends `Authorization: Bearer <token>`.
- Admin routes are under `/api/admin/*`.
- Admin controllers use `[Authorize(Policy = "AdminOnly")]`.
- `AdminOnly` is satisfied from `users.role` via role claims transformation.
- `GET /api/arena/test` is a protected token-validation ping.
- `GET /api/arena/model-stats` is public.

Important backend risk:
- If `JWT_SECRET` is missing, current backend falls back to unsafe signature bypass logic. This must be treated as a backend security issue, not a frontend behavior.

## 3. Common Response Contract

Most admin endpoints return:

```json
{
  "success": true,
  "data": [],
  "page": 1,
  "pageSize": 10,
  "page_size": 10,
  "total": 0,
  "message": "optional",
  "error": "optional"
}
```

Notes:
- List endpoints return `data` as an array.
- Pagination currently exposes both `pageSize` and `page_size`.
- Error shape is generally `{ success: false, error, message? }`.

## 4. Implemented Routes

## 4.1 Health and Auth

- `GET /api/arena/test`
  - Requires bearer token.
  - Use as auth/session validation ping.

## 4.2 Dashboard

- `GET /api/admin/dashboard`
  - Returns:
    - `data.metrics.total_users`
    - `data.metrics.active_models`
    - `data.metrics.total_comparisons`
    - `data.metrics.total_votes`
    - `data.api_keys.total`
    - `data.api_keys.active`
    - `data.api_keys.in_cooldown`
    - `data.health.supabase_realtime`
    - `data.health.redis`
  - Also returns extra fields for backward compatibility:
    - `total_users`
    - `total_models`
    - `total_comparisons`
    - `total_votes`
    - `active_models_count`
    - `recent_activity`
    - `provider_health`
    - `api_key_metrics`
    - `system_health_status`

- `GET /api/admin/votes/stats`
  - Returns:
    - `data.votes_over_time[]`
      - `{ date, vote_count }`
    - `data.top_10_models[]`
      - `{ model_name, win_rate, wins }`
  - Also returns:
    - `total_votes`
    - `votes_by_model`
    - `votes_by_day`
    - `tie_count`

## 4.3 Users

- `GET /api/admin/users?page=1&pageSize=10&search=&role=`
  - Row fields:
    - `user_id`
    - `email`
    - `role`
    - `created_at`
  - Also includes `full_name`, `last_login_at` when present.

- `PATCH /api/admin/users/{id}/role`
  - Body:
```json
{ "role": "admin" }
```

- `DELETE /api/admin/users/{id}`

- Also implemented for forward compatibility:
  - `GET /api/admin/users/{id}`
  - `POST /api/admin/users`
  - `PUT /api/admin/users/{id}`

Validation:
- Allowed roles: `admin`, `user`

## 4.4 Models

- `GET /api/admin/models?page=1&pageSize=10&search=&provider=&status=`
  - Row fields:
    - `model_id`
    - `model_name`
    - `display_name`
    - `provider_name`
    - `is_free`
    - `status`

- `POST /api/admin/models`
- `PUT /api/admin/models/{id}`
- `PATCH /api/admin/models/{id}/status`
- `DELETE /api/admin/models/{id}`

Validation:
- Allowed status values: `active`, `inactive`, `maintenance`

## 4.5 Providers

- `GET /api/admin/providers`
  - Row fields:
    - `provider_name`
    - `status`
  - Also includes:
    - `display_name`
    - `is_enabled`
    - `priority`
    - `key_count`

- Also implemented:
  - `GET /api/admin/providers/{name}`
  - `POST /api/admin/providers`
  - `PUT /api/admin/providers/{name}`
  - `DELETE /api/admin/providers/{name}`

## 4.6 API Keys

- `GET /api/admin/keys?provider=openai&pageSize=50`
  - Row fields:
    - `key_id`
    - `display_mask`
    - `is_active`
    - `cooldown_until`
    - `total_calls`
    - `failure_count`
    - `last_used_at`
    - `last_error_type`
  - Also includes:
    - `provider_name`
    - `last_error_category`
    - `created_at`
    - `updated_at`
  - List route intentionally excludes raw `api_key`.

- `POST /api/admin/keys`
  - Body:
```json
{
  "provider_name": "openai",
  "api_key": "sk-..."
}
```

- `PATCH /api/admin/keys/{id}/toggle`
  - Preferred body:
```json
{ "isActive": false }
```
  - Also accepts no body and toggles current state.

- `POST /api/admin/keys/{id}/reset-cooldown`

- Also implemented:
  - `GET /api/admin/keys/{id}`
  - `PUT /api/admin/keys/{id}`
  - `DELETE /api/admin/keys/{id}`

Important backend risk:
- `GET /api/admin/keys/{id}` still returns the raw `api_key`. Avoid exposing this in admin UI. Prefer masked-only display and rotate workflows.

## 4.7 Threads and Messages

- `GET /api/admin/threads?page=1&pageSize=10&search=&visibility=`
  - Row fields:
    - `thread_id`
    - `title`
    - `user_id`
    - `mode`
    - `visibility`
    - `message_count`
    - `created_at`

- `PATCH /api/admin/threads/{id}/visibility`
  - Body:
```json
{ "visibility": "public" }
```

- `DELETE /api/admin/threads/{id}`

- `GET /api/admin/messages?threadId={threadId}&pageSize=50`
  - Row fields:
    - `message_id`
    - `thread_id`
    - `prompt_text`
    - `model1_name`
    - `model2_name`
    - `created_at`
  - Also includes optional responses and timing fields.

- Also implemented:
  - `GET /api/admin/threads/{id}`
  - `POST /api/admin/threads`
  - `PUT /api/admin/threads/{id}`
  - `POST /api/admin/messages`
  - `GET /api/admin/messages/{id}`
  - `DELETE /api/admin/messages/{id}`

Validation:
- Allowed visibility values: `private`, `unlisted`, `public`

## 4.8 Comparisons

- `GET /api/admin/comparisons?page=1&pageSize=10&search=&isRevealed=true|false`
  - Row fields currently come from the raw `comparisons` table:
    - `comparison_id`
    - `prompt_text`
    - `model1_id`
    - `model2_id`
    - `is_revealed`
    - `created_at`
    - `model1_response`
    - `model2_response`
    - `model1_time_ms`
    - `model2_time_ms`

- `DELETE /api/admin/comparisons/{id}`

- Also implemented:
  - `GET /api/admin/comparisons/{id}`

Important backend note:
- `model1_id` and `model2_id` are currently raw stored IDs from the comparisons table. If UI wants model names, frontend must map them or backend must add joined display fields later.

## 4.9 Votes

- `GET /api/admin/votes?page=1&pageSize=15&search=`
  - Row fields:
    - `vote_id`
    - `comparison_id`
    - `user_id`
    - `winner_model_id`
    - `picked_model_id`
    - `vote_choice`
    - `choice`
    - `created_at`
    - `voted_at`
  - `picked_model_id` is an alias of `winner_model_id`.
  - `choice` is an alias of `vote_choice`.

- `DELETE /api/admin/votes/{id}`

- Also implemented:
  - `POST /api/admin/votes`
  - `GET /api/admin/votes/{id}`

## 4.10 Leaderboard

- `GET /api/arena/model-stats`
  - Returns:
```json
{
  "success": true,
  "items": []
}
```
  - Each row exposes the UI-needed aliases:
    - `model_name`
    - `elo_rating`
    - `total_matches`
    - `wins`
  - Also exposes compatibility aliases:
    - `model_id`
    - `display_name`
    - `provider_name`
    - `elo`
    - `matches`
    - `win_rate`

## 5. Validation Rules

- Role:
  - `admin`
  - `user`

- Model status:
  - `active`
  - `inactive`
  - `maintenance`

- Thread visibility:
  - `private`
  - `unlisted`
  - `public`

## 6. What the Admin UI Should Generate

Generate these pages:
- Dashboard
- Users
- Models
- Providers
- API Keys
- Threads
- Messages
- Comparisons
- Votes
- Leaderboard

Behavior requirements:
- Use server-side pagination.
- Use the exact query parameter names listed above.
- Show destructive action confirms.
- Show toast/error handling using backend `error` or `message`.
- Hide or disable actions that depend on backend gaps.
- Do not display raw secrets even if an endpoint exposes them.

## 7. Backend Gaps Still Remaining

These are not complete if the goal is "admin manages everything":
- No admin CRUD for `system_settings`
- No admin CRUD for `user_preferences`
- No admin CRUD for `chat_sessions`
- No admin CRUD for `ai_messages`
- No audit log endpoints for admin actions
- No bulk actions
- No export endpoints
- No impersonation or session-control endpoints
- No dedicated admin integration test suite for the full route matrix

Contract/security gaps still open:
- JWT validation fallback in `Program.cs` is unsafe when `JWT_SECRET` is missing
- `GET /api/admin/keys/{id}` returns raw secrets
- Comparisons endpoint does not resolve model display names

## 8. Copy-Paste Prompt for Another AI

```text
Create a production-ready React + TypeScript admin panel for DualMind.

Use only the backend routes and payloads in this file. Do not invent endpoints.

Auth:
- Supabase login on the frontend
- Send Authorization: Bearer <token> to backend
- Use GET /api/arena/test as authenticated session ping

Pages:
- Dashboard
- Users
- Models
- Providers
- API Keys
- Threads
- Messages
- Comparisons
- Votes
- Leaderboard

Rules:
1. Use server-side pagination and exact query parameter names from the spec.
2. Expect admin API responses in the form { success, data, page, pageSize, page_size, total, error, message }.
3. For leaderboard, expect { success, items }.
4. Build strict TypeScript types from the route contracts.
5. Add route guards so non-admin users cannot access admin pages.
6. If a backend feature is missing, render a disabled UI state with a clear "Backend endpoint required" message.
7. Never show raw API keys in the UI even if a detail endpoint returns them.
8. Include reusable table, filters, modal forms, confirm dialogs, and error handling.
9. Include an API client layer, page components, route config, and .env.example.
10. Add one "Backend Gaps" page that lists missing admin capabilities from this handoff file.
```
