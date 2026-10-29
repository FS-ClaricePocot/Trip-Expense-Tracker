-- F6: recorded payments between people on a trip.
-- amount_cents is a whole number of cents greater than 0, and a payment is always recorded in full (F6.R6).
-- Money is never a float. marked_paid_at is set from TimeProvider (UTC) (F6.R7).
-- seq orders the payments made list newest first (F6.R7).
-- Composite keys (payer_person_id, trip_id) and (payee_person_id, trip_id) -> people (id, trip_id) prove that
-- the payer and payee belong to the same trip as the payment (F6.R6). The database refuses a person from another trip.
-- Payer and payee references use the default NO ACTION, so removing a person in a payment fails (F3.R7, F6.R16).
-- Deleting a trip cascades to payments (F6.R17, F2.R12).
-- There is no UNIQUE constraint, so identical payments are both kept (spec, Edge cases).
-- Payments are never edited (F6.R9). The "payer and payee differ" rule is enforced here by payer_not_payee.

CREATE TABLE payments (
  id               uuid PRIMARY KEY,
  trip_id          uuid NOT NULL REFERENCES trips(id) ON DELETE CASCADE,
  payer_person_id  uuid NOT NULL,
  payee_person_id  uuid NOT NULL,
  amount_cents     bigint NOT NULL CHECK (amount_cents > 0),
  marked_paid_at   timestamptz NOT NULL,
  seq              bigint GENERATED ALWAYS AS IDENTITY,
  CONSTRAINT payer_not_payee CHECK (payer_person_id <> payee_person_id),
  CONSTRAINT fk_payments_payer FOREIGN KEY (payer_person_id, trip_id)
    REFERENCES people (id, trip_id),
  CONSTRAINT fk_payments_payee FOREIGN KEY (payee_person_id, trip_id)
    REFERENCES people (id, trip_id)
);

CREATE INDEX ix_payments_trip_seq ON payments (trip_id, seq);
CREATE INDEX ix_payments_payer ON payments (payer_person_id);
CREATE INDEX ix_payments_payee ON payments (payee_person_id);
