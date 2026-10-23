import { HttpErrorResponse } from '@angular/common/http';

/** Copy from docs/specs/features/F1-registration-and-login.md, States & errors. */
export const GENERIC_ERROR_MESSAGE = 'Something went wrong. Please try again.';
export const REGISTRATION_FAILED_MESSAGE = "We couldn't create your account with these details.";
export const INVALID_CREDENTIALS_MESSAGE = 'Email or password is incorrect.';
export const LOGIN_REQUIRED_MESSAGE = 'Please log in to see your trips.';
/** NEW COPY, not in docs/specs/features/F1-registration-and-login.md. Chosen by the product owner; review against the spec. */
export const LOGIN_PASSWORD_REQUIRED_MESSAGE = 'Enter your password.';
export const EMAIL_REQUIRED_MESSAGE = 'Enter your email address.';
/** Copy from docs/specs/features/F2-trips.md, States & errors. */
export const TRIP_NAME_REQUIRED_MESSAGE = 'Enter a trip name.';
export const TRIP_NAME_TOO_LONG_MESSAGE = 'A trip name can be at most 100 characters.';
export const TRIP_LIMIT_MESSAGE = 'You have reached the limit of 50 trips. Delete a trip to create a new one.';
/** Copy from docs/specs/features/F2-trips.md, States & errors (trip not found, F2.R9). */
export const TRIP_NOT_FOUND_MESSAGE = 'This trip no longer exists.';
/** Copy from docs/specs/features/F3-people.md, States & errors (add a person). */
export const PERSON_NAME_REQUIRED_MESSAGE = 'Enter a name.';
export const PERSON_NAME_TOO_LONG_MESSAGE = 'A name can be at most 100 characters.';

const PERSON_NAME_MESSAGES: Record<string, string> = {
  name_required: PERSON_NAME_REQUIRED_MESSAGE,
  name_too_long: PERSON_NAME_TOO_LONG_MESSAGE,
};

/**
 * Copy for a 400 on the add-person name, or null when the response has no name error.
 * The server codes name_required and name_too_long map to the person copy, not the trip copy.
 */
export function personNameErrorMessage(error: unknown): string | null {
  const errors = body(error)?.['errors'];
  if (!isRecord(errors)) {
    return null;
  }
  const codes = errors['name'];
  if (!Array.isArray(codes)) {
    return null;
  }
  const code = codes[0];
  return typeof code === 'string' && Object.hasOwn(PERSON_NAME_MESSAGES, code) ? PERSON_NAME_MESSAGES[code] : GENERIC_ERROR_MESSAGE;
}

/** F3.R3: the duplicate copy, using the trimmed name the user typed. */
export function duplicatePersonMessage(name: string): string {
  return `${name} is already on this trip.`;
}

/** Reason passed in the /login query string when the guard redirects. */
export const LOGIN_REQUIRED_REASON = 'login-required';

/** Server field codes (ValidationProblemDetails) to field copy. */
const FIELD_MESSAGES: Record<string, string> = {
  email_required: EMAIL_REQUIRED_MESSAGE,
  email_invalid: 'Enter an email address like name@example.com.',
  password_too_short: 'Your password must be at least 8 characters.',
  password_too_long: 'Your password can be at most 128 characters.',
  name_required: TRIP_NAME_REQUIRED_MESSAGE,
  name_too_long: TRIP_NAME_TOO_LONG_MESSAGE,
};

/** ProblemDetails `type` values to form-level (above the form) copy. */
const PROBLEM_TYPE_MESSAGES: Record<string, string> = {
  'registration-failed': REGISTRATION_FAILED_MESSAGE,
  'invalid-credentials': INVALID_CREDENTIALS_MESSAGE,
  'trip-limit-reached': TRIP_LIMIT_MESSAGE,
};

/** Reason codes from the router to copy. */
const REASON_MESSAGES: Record<string, string> = {
  [LOGIN_REQUIRED_REASON]: LOGIN_REQUIRED_MESSAGE,
};

/** Copy for one field error code. Unknown codes get the generic message. */
export function fieldErrorMessage(code: string): string {
  return FIELD_MESSAGES[code] ?? GENERIC_ERROR_MESSAGE;
}

/**
 * Maps a validation response (`errors: { field: [code, ...] }`) to copy per
 * field. Only the first code of each field is used. Returns an empty object
 * when the response has no field errors.
 */
export function fieldErrorMessages(error: unknown): Record<string, string> {
  const result: Record<string, string> = {};
  const errors = body(error)?.['errors'];
  if (!isRecord(errors)) {
    return result;
  }
  for (const [field, codes] of Object.entries(errors)) {
    const first = Array.isArray(codes) ? codes[0] : undefined;
    result[field] = typeof first === 'string' ? fieldErrorMessage(first) : GENERIC_ERROR_MESSAGE;
  }
  return result;
}

/**
 * Copy shown above the form for a failed request. Generic failures
 * (registration-failed, invalid-credentials) use their own copy. Anything
 * else, including network errors, gets the generic message.
 */
export function formErrorMessage(error: unknown): string {
  const type = body(error)?.['type'];
  if (typeof type === 'string' && Object.hasOwn(PROBLEM_TYPE_MESSAGES, type)) {
    return PROBLEM_TYPE_MESSAGES[type];
  }
  return GENERIC_ERROR_MESSAGE;
}

/** The ProblemDetails `type` of a failed request, or null if there is none. */
export function problemType(error: unknown): string | null {
  const type = body(error)?.['type'];
  return typeof type === 'string' ? type : null;
}

/** Copy for a reason passed on the /login query string, or null if unknown. */
export function loginReasonMessage(reason: string | null | undefined): string | null {
  if (!reason || !Object.hasOwn(REASON_MESSAGES, reason)) {
    return null;
  }
  return REASON_MESSAGES[reason];
}

function body(error: unknown): Record<string, unknown> | null {
  if (error instanceof HttpErrorResponse) {
    return isRecord(error.error) ? error.error : null;
  }
  return isRecord(error) ? error : null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}
