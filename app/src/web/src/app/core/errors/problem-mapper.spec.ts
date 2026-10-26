import { HttpErrorResponse } from '@angular/common/http';
import {
  fieldErrorMessage,
  fieldErrorMessages,
  formErrorMessage,
  loginReasonMessage,
} from './problem-mapper';

describe('problem-mapper', () => {
  describe('field codes', () => {
    it('maps each server field code to the spec copy', () => {
      expect(fieldErrorMessage('email_required')).toBe('Enter your email address.');
      expect(fieldErrorMessage('email_invalid')).toBe('Enter an email address like name@example.com.');
      expect(fieldErrorMessage('password_too_short')).toBe('Your password must be at least 8 characters.');
      expect(fieldErrorMessage('password_too_long')).toBe('Your password can be at most 128 characters.');
    });

    it('falls back to the generic message for an unknown code', () => {
      expect(fieldErrorMessage('something_else')).toBe('Something went wrong. Please try again.');
    });

    it('maps a ValidationProblemDetails body to copy per field', () => {
      const error = new HttpErrorResponse({
        status: 400,
        error: { errors: { email: ['email_invalid'], password: ['password_too_short'] } },
      });

      expect(fieldErrorMessages(error)).toEqual({
        email: 'Enter an email address like name@example.com.',
        password: 'Your password must be at least 8 characters.',
      });
    });

    it('returns no field copy when the body has no errors', () => {
      expect(fieldErrorMessages(new HttpErrorResponse({ status: 500 }))).toEqual({});
    });
  });

  describe('form-level copy', () => {
    it('maps registration-failed to the generic registration message', () => {
      const error = new HttpErrorResponse({ status: 400, error: { type: 'registration-failed' } });
      expect(formErrorMessage(error)).toBe("We couldn't create your account with these details.");
    });

    it('maps invalid-credentials to the generic login message', () => {
      const error = new HttpErrorResponse({ status: 401, error: { type: 'invalid-credentials' } });
      expect(formErrorMessage(error)).toBe('Email or password is incorrect.');
    });

    it('maps a network error to the generic error message', () => {
      const error = new HttpErrorResponse({ status: 0, error: new ProgressEvent('error') });
      expect(formErrorMessage(error)).toBe('Something went wrong. Please try again.');
    });

    it('maps an unknown type to the generic error message', () => {
      const error = new HttpErrorResponse({ status: 500, error: { type: 'boom' } });
      expect(formErrorMessage(error)).toBe('Something went wrong. Please try again.');
    });
  });

  describe('login reason', () => {
    it('maps login-required to the trip-page message', () => {
      expect(loginReasonMessage('login-required')).toBe('Please log in to see your trips.');
    });

    it('returns null for an unknown or missing reason', () => {
      expect(loginReasonMessage('nope')).toBeNull();
      expect(loginReasonMessage(null)).toBeNull();
    });
  });
});
