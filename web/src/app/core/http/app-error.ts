import { HttpErrorResponse } from '@angular/common/http';

export interface AppError {
  status: number;
  message: string;
  code?: string;
  fieldErrors: Record<string, string[]>;
}

const NETWORK_MESSAGE = 'Unable to reach the server. Please try again.';
const GENERIC_MESSAGE = 'Something went wrong. Please try again.';

interface ProblemDetailsBody {
  title?: unknown;
  detail?: unknown;
  code?: unknown;
  errors?: unknown;
}

export function toAppError(e: HttpErrorResponse): AppError {
  if (e.status === 0) {
    return { status: 0, message: NETWORK_MESSAGE, fieldErrors: {} };
  }

  const body: ProblemDetailsBody = e.error !== null && typeof e.error === 'object' ? e.error : {};
  const message =
    typeof body.detail === 'string' && body.detail
      ? body.detail
      : typeof body.title === 'string' && body.title
        ? body.title
        : GENERIC_MESSAGE;

  const appError: AppError = {
    status: e.status,
    message,
    fieldErrors: toFieldErrors(body.errors),
  };
  if (typeof body.code === 'string') {
    appError.code = body.code;
  }
  return appError;
}

function toFieldErrors(errors: unknown): Record<string, string[]> {
  const result: Record<string, string[]> = {};
  if (errors !== null && typeof errors === 'object') {
    for (const [field, messages] of Object.entries(errors)) {
      if (Array.isArray(messages)) {
        result[field] = messages.filter((m): m is string => typeof m === 'string');
      }
    }
  }
  return result;
}
