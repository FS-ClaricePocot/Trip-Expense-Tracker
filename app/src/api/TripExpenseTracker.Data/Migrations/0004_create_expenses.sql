-- F4: expenses and their participants.
-- amount_cents is a whole number of cents greater than 0 (F4.R3). Money is never a float.
-- description is 1-100 characters, counted the same way as F3 and F2 (F4.R2). The service trims it before saving.
-- created_at is set from TimeProvider (UTC). seq orders the list newest first (F4.R8).
-- Composite keys (person_id, trip_id) -> people (id, trip_id) prove that the payer and each participant
-- belong to the same trip as the expense (F4.R4, F4.R5). The database refuses a person from another trip.
-- Payer and participant references use the default NO ACTION, so removing a person in use fails (F3.R7).
-- Deleting a trip cascades to expenses (F2.R12, F4.R14), and expenses cascade to their participants.
-- Shares are not stored. They are computed from amount_cents, the participants and people.seq (F4.R7).
-- The rule "at least one participant" is enforced by the service in the same transaction, not here.

CREATE TABLE expenses (
  id               uuid PRIMARY KEY,
  trip_id          uuid NOT NULL REFERENCES trips(id) ON DELETE CASCADE,
  description      text NOT NULL CHECK (char_length(description) BETWEEN 1 AND 100),
  amount_cents     bigint NOT NULL CHECK (amount_cents > 0),
  payer_person_id  uuid NOT NULL,
  created_at       timestamptz NOT NULL,
  seq              bigint GENERATED ALWAYS AS IDENTITY,
  CONSTRAINT ux_expenses_id_trip UNIQUE (id, trip_id),
  CONSTRAINT fk_expenses_payer FOREIGN KEY (payer_person_id, trip_id)
    REFERENCES people (id, trip_id)
);

CREATE INDEX ix_expenses_trip_seq ON expenses (trip_id, seq);
CREATE INDEX ix_expenses_payer ON expenses (payer_person_id);

CREATE TABLE expense_participants (
  expense_id uuid NOT NULL,
  trip_id    uuid NOT NULL,
  person_id  uuid NOT NULL,
  PRIMARY KEY (expense_id, person_id),
  CONSTRAINT fk_participant_expense FOREIGN KEY (expense_id, trip_id)
    REFERENCES expenses (id, trip_id) ON DELETE CASCADE,
  CONSTRAINT fk_participant_person FOREIGN KEY (person_id, trip_id)
    REFERENCES people (id, trip_id)
);

CREATE INDEX ix_expense_participants_person ON expense_participants (person_id);
