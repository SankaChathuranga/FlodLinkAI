import { ApiError } from '../api/client'

/** Human-readable error text for the approval/history/analytics screens. */
export function describeApiError(err: unknown): string {
  if (err instanceof ApiError) {
    if (err.status === 401) {
      return 'Your session has expired. Sign in again.'
    }
    if (err.status === 403) {
      return 'Your role is not allowed to do this.'
    }
    return `Request failed (${err.status} ${err.code}): ${err.message}`
  }
  return err instanceof Error ? err.message : 'Unexpected error. Check that the API is running.'
}