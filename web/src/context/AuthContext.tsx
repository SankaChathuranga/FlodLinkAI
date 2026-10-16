/**
 * AuthContext — who is signed in to the coordinator portal.
 *
 * Holds the token, user and role; `login` exchanges email + password for a token through
 * POST /api/auth/login and `logout` forgets it. The session is stored in sessionStorage
 * (src/auth/session.ts), so it survives a refresh. Any 401 from the API logs out.
 * Volunteers are refused here: their app is the mobile app.
 */

import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import {
  clearSession,
  loadSession,
  onUnauthorized,
  saveSession,
  type Role,
  type Session,
  type SessionUser,
} from '../auth/session'
import { useAppContext } from './AppContext'

/** Roles that may use the web portal. */
export const WEB_ROLES: readonly Role[] = ['Coordinator', 'DepotManager', 'Admin']

export class LoginError extends Error {}

interface AuthContextValue {
  token: string | null
  user: SessionUser | null
  role: Role | null
  /** Resolves on success; throws LoginError with a message suitable for the form. */
  login: (email: string, password: string) => Promise<SessionUser>
  logout: () => void
  /** True when the user is an Admin or holds one of the roles. */
  hasRole: (...roles: Role[]) => boolean
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const { apiBaseUrl } = useAppContext()
  const [session, setSession] = useState<Session | null>(() => loadSession())

  const logout = useCallback(() => {
    clearSession()
    setSession(null)
  }, [])

  useEffect(() => onUnauthorized(() => setSession(null)), [])

  // Log out when the token expires while the page stays open.
  useEffect(() => {
    if (!session) return
    const ms = new Date(session.expiresAt).getTime() - Date.now()
    const timer = setTimeout(logout, Math.max(ms, 0))
    return () => clearTimeout(timer)
  }, [session, logout])

  const login = useCallback(
    async (email: string, password: string) => {
      let response: Response
      try {
        response = await fetch(`${apiBaseUrl}/api/auth/login`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ email: email.trim(), password }),
        })
      } catch {
        throw new LoginError('Cannot reach the FloodLink API. Check that it is running.')
      }

      if (response.status === 401) throw new LoginError('Incorrect email or password.')
      if (response.status === 429) throw new LoginError('Too many attempts. Wait a minute and try again.')
      if (!response.ok) throw new LoginError(`Sign-in failed (${response.status}). Try again.`)

      const next = (await response.json()) as Session
      if (!WEB_ROLES.includes(next.user.role)) {
        throw new LoginError('Volunteer accounts use the FloodLink mobile app, not the web portal.')
      }
      saveSession(next)
      setSession(next)
      return next.user
    },
    [apiBaseUrl],
  )

  const value = useMemo<AuthContextValue>(() => {
    const role = session?.user.role ?? null
    return {
      token: session?.token ?? null,
      user: session?.user ?? null,
      role,
      login,
      logout,
      hasRole: (...roles: Role[]) => role !== null && (role === 'Admin' || roles.includes(role)),
    }
  }, [session, login, logout])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (ctx === undefined) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return ctx
}
