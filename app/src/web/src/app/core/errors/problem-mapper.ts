import { HttpErrorResponse } from '@angular/common/http';

/** Copy from docs/specs/features/F1-registration-and-login.md, States & errors. */
export const GENERIC_ERROR_MESSAGE = 'Something went wrong. Please try again.';
export const REGISTRATION_FAILED_MESSAGE = "We couldn't create your account with these details.";
export const INVALID_CREDENTIALS_MESSAGE = 'Email or password is incorrect.';
export const LOGIN_REQUIRED_MESSAGE = 'Please log in to see your trips.';
/** NEW COPY, not in docs/specs/features/F1-registration-and-login.md. Chosen by the product owner; review against the spec. */
export const LOGIN_PASSWORD_REQUIRED_MESSAGE = 'Enter your password.';
export const EMAIL_REQUIRED_MESSAGE = 'Enter your email address.';

/** Reason passed in the /login query string when the guard redirects. */
export const LOGIN_REQUIRED_REASON = 'login-required';

/** Server field codes (ValidationProblemDetails) to field copy. */
const FIELD_MESSAGES: Record<string, string> = {
  email_required: EMAIL_REQUIRED_MESSAGE,
  email_invalid: 'Enter an email address like name@example.com.',
  password_too_short: 'Your password must be at least 8 characters.',
  password_too_long: 'Your password can be at most 128 characters.',
};

/** ProblemDetails `type` values to form-level (above the form) copy. */
const PROBLEM_TYPE_MESSAGES: Record<string, string> = {
  'registration-failed': REGISTRATION_FAILED_MESSAGE,
  'invalid-credentials': INVALID_CREDENTIALS_MESSAGE,
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
