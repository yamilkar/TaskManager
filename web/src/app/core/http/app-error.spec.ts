import { HttpErrorResponse } from '@angular/common/http';
import { toAppError } from './app-error';

describe('toAppError', () => {
  it('toAppError maps ProblemDetails detail, code and errors', () => {
    const err = new HttpErrorResponse({
      status: 400,
      error: {
        title: 'Validation failed',
        detail: 'Bad input.',
        code: 'validation.failed',
        errors: { email: ['Invalid email.'] },
      },
    });

    expect(toAppError(err)).toEqual({
      status: 400,
      message: 'Bad input.',
      code: 'validation.failed',
      fieldErrors: { email: ['Invalid email.'] },
    });
  });

  it('toAppError maps status 0 to the "Unable to reach the server" message', () => {
    const result = toAppError(new HttpErrorResponse({ status: 0 }));

    expect(result.status).toBe(0);
    expect(result.message).toBe('Unable to reach the server. Please try again.');
    expect(result.fieldErrors).toEqual({});
  });

  it('falls back to title and then a generic message', () => {
    expect(toAppError(new HttpErrorResponse({ status: 500, error: { title: 'Boom' } })).message).toBe('Boom');
    expect(toAppError(new HttpErrorResponse({ status: 500, error: 'oops' })).message).toBe(
      'Something went wrong. Please try again.',
    );
  });
});
