CREATE TABLE IF NOT EXISTS telegram_sessions (
  telegram_chat_id BIGINT PRIMARY KEY,
  jwt_token TEXT NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE telegram_sessions
  ADD COLUMN IF NOT EXISTS refresh_token TEXT;

ALTER TABLE telegram_sessions
  ADD COLUMN IF NOT EXISTS jwt_expires_at TIMESTAMPTZ;
