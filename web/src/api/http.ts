import { getToken, notifyUnauthorized } from '../auth/session'

/**
 * fetch() for every FloodLink API call: adds `Authorization: Bearer <token>` when signed in,
 * and on a 401 clears the session so the app returns to /login. Use this instead of fetch.
 */
export async function apiFetch(input: string, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers)
  const token = getToken()
  if (token && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(input, { ...init, headers })
  // A 401 on a call that carried a token means it expired or was revoked.
  if (response.status === 401 && token) {
    notifyUnauthorized()
  }
  return response
}
