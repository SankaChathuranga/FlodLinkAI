import { ApiError } from '../api/client'

/** Human-readable error text for the approval/history/analytics screens. */
export function describeApiError(err: unknown): string {
  if (err instanceof ApiError) {
    if (err.status === 401) {
      return (
        'API returned 401 Unauthorized — the endpoints require ' +
        '[Authorize(Roles="Coordinator")]. Until JWT auth is wired, remove that ' +
        'attribute in the controllers for a dev demo.'
      )
    }
    return `Request failed (${err.status} ${err.code}): ${err.message}`
  }
  return err instanceof Error ? err.message : 'Unexpected error. Check that the API is running.'
}