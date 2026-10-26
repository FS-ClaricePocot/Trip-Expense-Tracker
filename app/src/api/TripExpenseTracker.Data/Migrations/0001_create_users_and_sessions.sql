-- F1: users and server-side sessions.
-- Email is trimmed and stored in lower case by the app (F1.R13). The unique index on lower(email)
-- makes uniqueness case-insensitive and enforced by the database (F1.R2).
-- The password is stored only as a one-way hash (F1.AC9).

CREATE TABLE users (
  id            uuid PRIMARY KEY,
  email         text NOT NULL,
  password_hash text NOT NULL,
  created_at    timestamptz NOT NULL
);

CREATE UNIQUE INDEX ux_users_email_lower ON users (lower(email));

CREATE TABLE sessions (
  id         text PRIMARY KEY,
  user_id    uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  created_at timestamptz NOT NULL
);

CREATE INDEX ix_sessions_user_id ON sessions (user_id);
CREATE INDEX ix_sessions_created_at ON sessions (created_at);
