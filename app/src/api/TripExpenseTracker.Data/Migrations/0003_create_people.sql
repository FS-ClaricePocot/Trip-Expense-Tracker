-- F3: people on a trip. A person has no account, so there are no credential or contact columns (F3.R4).
-- The app sets added_at from TimeProvider (UTC). The list is ordered by seq, the strict order added (F3.R5),
-- because two adds can share a timestamp and the leftover-cent rule needs one exact order.
-- char_length counts characters, the same unit F1 and F2 use (F3.R2).
-- Names are unique per trip ignoring case (F3.R3). The same name may exist on another trip.
-- ON DELETE CASCADE from trips(id): deleting a trip deletes its people (F3.R11, F2.R12).
-- Later features (F4, F6) reference people(id) with the default NO ACTION, so removing a person in use fails with 23503 (F3.R7).

CREATE TABLE people (
  id       uuid PRIMARY KEY,
  trip_id  uuid NOT NULL REFERENCES trips(id) ON DELETE CASCADE,
  name     text NOT NULL CHECK (char_length(name) BETWEEN 1 AND 100),
  added_at timestamptz NOT NULL,
  seq      bigint GENERATED ALWAYS AS IDENTITY,
  CONSTRAINT ux_people_id_trip UNIQUE (id, trip_id)
);

CREATE UNIQUE INDEX ux_people_trip_name ON people (trip_id, lower(name));
CREATE INDEX ix_people_trip_seq ON people (trip_id, seq);
