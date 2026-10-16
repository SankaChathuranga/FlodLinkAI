/**
 * The signed-in session (token + user), kept in sessionStorage so it survives a page refresh
 * but not a closed tab. AuthContext owns the React state; this module is the storage layer the
 * API wrapper reads the token from, so non-React code never needs the context.
 */

export type Role = 'Volunteer' | 'Coordinator' | 'DepotManager' | 'Admin'

export interface SessionUser {
  id: number
  name: string
  email: string | null
  role: Role
  phone: string
  createdAt: string
}

export interface Session {
  token: string
  expiresAt: string
  user: SessionUser
}

const STORAGE_KEY = 'floodlink.session'

export function loadSession(): Session | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as Session
    if (!session.token || new Date(session.expiresAt).getTime() <= Date.now()) {
      sessionStorage.removeItem(STORAGE_KEY)
      return null
    }
    return session
  } catch {
    return null
  }
}

export function saveSession(session: Session): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session))
  } catch {
    // Storage blocked (private mode): the session still lives in memory until refresh.
  }
}

export function clearSession(): void {
  try {
    sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    // ignore
  }
}

export function getToken(): string | null {
  return loadSession()?.token ?? null
}

// ── 401 broadcast ─────────────────────────────────────────────────────────────
// The API wrapper calls notifyUnauthorized() on any 401; AuthContext subscribes and logs out.

type Listener = () => void
const listeners = new Set<Listener>()

export function onUnauthorized(listener: Listener): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function notifyUnauthorized(): void {
  clearSession()
  listeners.forEach((l) => l())
}
