-- F2: trips.
-- The app trims the name and sets created_at from TimeProvider (UTC). The check guards the length only;
-- char_length counts characters, the same unit F1 uses for passwords (F2.R4).
-- owner_user_id is part of every query in TripDao, so "not yours" and "missing" look the same (F2.R9).
-- Later trip children (F3–F6) use ON DELETE CASCADE from trips(id), so one DELETE removes the whole trip (F2.R12).

CREATE TABLE trips (
  id            uuid PRIMARY KEY,
  owner_user_id uuid NOT NULL REFERENCES users(id),
  name          text NOT NULL CHECK (char_length(name) BETWEEN 1 AND 100),
  created_at    timestamptz NOT NULL
);

CREATE INDEX ix_trips_owner_created ON trips (owner_user_id, created_at DESC, id DESC);
