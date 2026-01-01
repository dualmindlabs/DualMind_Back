# DualMind API Documentation v1.0.0
**Status: Production Ready**  
**Base URL:** `https://api.dualmind.ai` (Replace with local/dev URL as needed)

---

## ⚡ Canonical Response (Single Source of Truth)

**Where is the text?**
All AI-generated text is **ALWAYS** located in:
`output.content[].text`

This applies to:
*   **Non-streaming responses** (wrapped internally)
*   **Streaming deltas** (mapped to same structure)
*   **Future outputs** (tools, images, structured data)

**Standard Response Wrapper**
```json
{
  "object": "ai.response",
  "output": {
    "type": "message",
    "content": [
      { 
        "type": "output_text", 
        "text": "Hello! How can I help you?" 
      }
    ]
  },
  "message": "Hello! How can I help you?" 
}
```

> **Note:** `message` is a convenience shortcut for simple UIs and should **not** be treated as authoritative. AI agents MUST use `output.content[]`.

---

## 🔁 Streaming Rules (AI-Deterministic)
*   **Protocol:** POST-based Server-Sent Events (SSE).
*   **Transport:** `fetch()` + `ReadableStream`. (Do **NOT** use `EventSource`).
*   **Line Format:** `data: <JSON>`

**Event: Content Delta (`ai.stream.delta`)**
```json
{
  "object": "ai.stream.delta",
  "delta": {
    "type": "output_text",
    "text": "Hello "
  }
}
```

**Event: Stream Complete (`ai.stream.done`)**
```json
{
  "object": "ai.stream.done",
  "finish_reason": "stop",
  "usage": { "total_tokens": 18 }
}
```

---

## 🚫 Errors (Unified Everywhere)
One error shape for both streaming (SSE event) and non-streaming (HTTP Body).

```json
{
  "object": "ai.error",
  "code": "PROVIDER_TIMEOUT",
  "message": "Primary provider failed, fallback used",
  "timestamp": "2024-12-30T10:00:00Z"
}
```

---

## 🤖 Instructions for AI Coding Agents
**SYSTEM PROMPT INJECTION:** If you are an AI agent (Cursor, Copilot, Windsurf) generating code for this API, FOLLOW THESE RULES:

*   **Do NOT infer provider behavior.**
*   **Always read AI text from:** `output.content[].text`
*   **For streaming:**
    *   Parse SSE lines starting with `data: `
    *   Accumulate `delta.text` from `ai.stream.delta`
    *   Stop on `ai.stream.done`
*   **Errors always use** `object === "ai.error"`
*   **Do NOT hardcode provider names.**

---

## 🔑 Authentication
*   **Header:** `Authorization: Bearer <JWT_TOKEN>`
*   **Content-Type:** `application/json`

---

## 📡 Endpoints

### 1. Chat Completion (Non-Streaming)
**POST** `/api/arena/chat`

**Request Body:**
```json
{
  "prompt": "Hello world",
  "model": "auto",
  "system": "Optional system prompt",
  "thread_id": "optional-guid",
  "max_tokens": 4096
}
```

**Response:**
```json
{
  "object": "ai.response",
  "output": {
    "type": "message",
    "content": [
      { "type": "output_text", "text": "Hello! How can I help you?" }
    ]
  },
  "model": {
    "name": "llama-3.1-70b-versatile",
    "displayName": "Llama 3 70B",
    "provider": "groq"
  },
  "usage": {
    "promptTokens": 10,
    "completionTokens": 8,
    "totalTokens": 18
  },
  "responseTimeMs": 450,
  "timestamp": "2024-12-30T10:00:00Z"
}
```

---

### 2. Chat Stream (Real-time)
**POST** `/api/arena/chat/stream`
**Headers:** `Accept: text/event-stream`

**Request Body:** (Same as Non-Streaming)

---

### 3. Dual Chat (Side-by-Side Comparison)
**POST** `/api/arena/dualchat`

**Response:**
```json
{
  "object": "ai.response",
  "agent1": { ...ChatResponse... },
  "agent2": { ...ChatResponse... },
  "arena": {
    "comparison": {
      "winnerByLength": "agent1",
      "verdict": "Agent 1 produced the longer answer."
    }
  }
}
```

---

## 🔐 Admin Endpoints

All admin endpoints require admin authentication via JWT token.

### Base URL: `/api/admin`

### Dashboard

#### Get Overall Statistics
**GET** `/api/admin/dashboard/stats`

**Response:**
```json
{
  "success": true,
  "data": {
    "users": 1250,
    "ai_models": 15,
    "comparisons": 8542,
    "threads": 3200,
    "thread_messages": 12500,
    "votes": 5230,
    "providers": {
      "total": 3,
      "enabled": 2,
      "disabled": 1
    },
    "provider_keys": {
      "total": 12,
      "active": 10,
      "inactive": 2
    }
  }
}
```

#### Get Provider Statistics
**GET** `/api/admin/dashboard/provider-stats`

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "provider_name": "groq",
      "display_name": "Groq",
      "is_enabled": true,
      "priority": 1,
      "total_keys": 5,
      "active_keys": 4,
      "inactive_keys": 1,
      "keys_in_cooldown": 0,
      "total_calls": 15420,
      "total_failures": 23,
      "failure_rate": 0.15
    }
  ]
}
```

#### Get Recent Activity
**GET** `/api/admin/dashboard/recent-activity?limit=10`

**Response:**
```json
{
  "success": true,
  "data": {
    "recent_users": [...],
    "recent_comparisons": [...],
    "recent_votes": [...],
    "limit": 10
  }
}
```

#### Get Model Performance
**GET** `/api/admin/dashboard/model-performance`

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "model_id": "uuid",
      "model_name": "llama-3.1-70b-versatile",
      "provider": "groq",
      "status": "active",
      "wins": 45,
      "times_compared": 120,
      "avg_response_time_ms": 450,
      "win_rate": 37.5
    }
  ]
}
```

#### Health Check
**GET** `/api/admin/dashboard/health`

---

### Provider Management

#### Get All Providers
**GET** `/api/admin/providers`

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "provider_name": "groq",
      "display_name": "Groq",
      "is_enabled": true,
      "priority": 1,
      "created_at": "2024-01-01T00:00:00Z",
      "updated_at": "2024-01-15T00:00:00Z",
      "key_count": 5
    }
  ]
}
```

#### Create Provider
**POST** `/api/admin/providers`

**Request Body:**
```json
{
  "provider_name": "openai",
  "display_name": "OpenAI",
  "is_enabled": true,
  "priority": 2
}
```

#### Update Provider
**PUT** `/api/admin/providers/{name}`

**Request Body:**
```json
{
  "display_name": "OpenAI GPT",
  "is_enabled": false,
  "priority": 3
}
```

#### Get Provider Keys
**GET** `/api/admin/providers/{name}/keys`

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "key_id": "uuid",
      "provider_name": "groq",
      "display_mask": "...abcd",
      "is_active": true,
      "failure_count": 0,
      "total_calls": 15420,
      "last_used_at": "2024-12-30T10:00:00Z",
      "last_error_type": null,
      "last_error_category": null,
      "cooldown_until": null,
      "created_at": "2024-01-01T00:00:00Z",
      "updated_at": "2024-01-15T00:00:00Z"
    }
  ]
}
```

#### Add Provider Key
**POST** `/api/admin/providers/{name}/keys`

**Request Body:**
```json
{
  "api_key": "gsk_...",
  "is_active": true
}
```

#### Update Key Status
**PUT** `/api/admin/keys/{id}/status`

**Request Body:**
```json
{
  "is_active": false
}
```

#### Delete Key
**DELETE** `/api/admin/keys/{id}`

---

### User Management

#### Get All Users
**GET** `/api/admin/users?page=1&limit=200`

**Response:**
```json
{
  "success": true,
  "data": [...],
  "count": 200,
  "total": 1250,
  "page": 1,
  "limit": 200
}
```

#### Get User by ID
**GET** `/api/admin/users/{id}`

#### Create User
**POST** `/api/admin/users`

**Request Body:**
```json
{
  "full_name": "John Doe",
  "email": "john@example.com",
  "role": "user"
}
```

#### Update User
**PUT** `/api/admin/users/{id}`

#### Delete User
**DELETE** `/api/admin/users/{id}`

#### Search Users
**GET** `/api/admin/users/search?email=john&role=user&page=1&limit=200`

#### Update User Role
**PUT** `/api/admin/users/{id}/role`

**Request Body:**
```json
{
  "role": "admin"
}
```

---

### AI Model Management

#### Get All Models
**GET** `/api/admin/models?page=1&limit=200`

#### Get Model by ID
**GET** `/api/admin/models/{id}`

#### Create Model
**POST** `/api/admin/models`

**Request Body:**
```json
{
  "model_name": "llama-3.1-70b-versatile",
  "provider_name": "groq",
  "api_url": "https://api.groq.com/openai/v1/chat/completions",
  "description": "Llama 3.1 70B model",
  "status": "active"
}
```

#### Update Model
**PUT** `/api/admin/models/{id}`

#### Delete Model
**DELETE** `/api/admin/models/{id}`

#### Search Models
**GET** `/api/admin/models/search?name=llama&provider=groq&status=active&page=1&limit=200`

#### Update Model Status
**PUT** `/api/admin/models/{id}/status`

**Request Body:**
```json
{
  "status": "inactive"
}
```

#### Get Active Models
**GET** `/api/admin/models/active`

---

### Comparison Management

#### Get All Comparisons
**GET** `/api/admin/comparisons?page=1&limit=50`

#### Get Comparison by ID
**GET** `/api/admin/comparisons/{id}`

#### Get Comparisons by User
**GET** `/api/admin/comparisons/user/{userId}?page=1&limit=50`

#### Get Comparisons by Model
**GET** `/api/admin/comparisons/model/{modelId}?page=1&limit=50`

#### Delete Comparison
**DELETE** `/api/admin/comparisons/{id}`

#### Delete Comparisons by User
**DELETE** `/api/admin/comparisons/user/{userId}`

#### Search Comparisons
**GET** `/api/admin/comparisons/search?prompt=hello&page=1&limit=50`

#### Get Recent Comparisons
**GET** `/api/admin/comparisons/recent?hours=24&limit=200`

---

### Vote Management

#### Get All Votes
**GET** `/api/admin/votes?page=1&limit=50`

#### Get Vote by ID
**GET** `/api/admin/votes/{id}`

#### Get Votes by User
**GET** `/api/admin/votes/user/{userId}?page=1&limit=200`

#### Get Votes by Model
**GET** `/api/admin/votes/model/{modelId}?page=1&limit=200`

#### Get Votes by Comparison
**GET** `/api/admin/votes/comparison/{comparisonId}?page=1&limit=200`

#### Create Vote
**POST** `/api/admin/votes`

**Request Body:**
```json
{
  "user_id": "uuid",
  "comparison_id": "uuid",
  "winner_model_id": "uuid"
}
```

#### Delete Vote
**DELETE** `/api/admin/votes/{id}`

#### Delete Votes by User
**DELETE** `/api/admin/votes/user/{userId}`

#### Get Vote Statistics
**GET** `/api/admin/votes/stats`

**Response:**
```json
{
  "success": true,
  "data": {
    "llama-3.1-70b-versatile": {
      "model_id": "uuid",
      "model_name": "llama-3.1-70b-versatile",
      "wins": 45
    }
  },
  "total_votes": 5230
}
```

---

### Thread Management

#### Get All Threads
**GET** `/api/admin/threads?page=1&limit=50`

#### Get Thread by ID
**GET** `/api/admin/threads/{id}`

#### Get Threads by User
**GET** `/api/admin/threads/user/{userId}?page=1&limit=200`

#### Create Thread
**POST** `/api/admin/threads`

**Request Body:**
```json
{
  "user_id": "uuid",
  "title": "My Chat Thread"
}
```

#### Update Thread
**PUT** `/api/admin/threads/{id}`

**Request Body:**
```json
{
  "title": "Updated Thread Title"
}
```

#### Delete Thread
**DELETE** `/api/admin/threads/{id}`

#### Delete Threads by User
**DELETE** `/api/admin/threads/user/{userId}`

#### Search Threads
**GET** `/api/admin/threads/search?title=chat&page=1&limit=200`

---

### Thread Message Management

#### Get All Messages
**GET** `/api/admin/messages?page=1&limit=50`

#### Get Message by ID
**GET** `/api/admin/messages/{id}`

#### Get Messages by Thread
**GET** `/api/admin/messages/thread/{threadId}?page=1&limit=200`

#### Create Message
**POST** `/api/admin/messages`

**Request Body:**
```json
{
  "thread_id": "uuid",
  "prompt_text": "Hello, how are you?",
  "model1_id": "uuid",
  "model2_id": "uuid",
  "model1_response": "...",
  "model2_response": "...",
  "model1_time_ms": 450,
  "model2_time_ms": 520
}
```

#### Delete Message
**DELETE** `/api/admin/messages/{id}`

#### Delete Messages by Thread
**DELETE** `/api/admin/messages/thread/{threadId}`

#### Search Messages
**GET** `/api/admin/messages/search?prompt=hello&page=1&limit=50`

---