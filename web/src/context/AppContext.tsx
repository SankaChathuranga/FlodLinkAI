/**
 * AppContext — shared application state for the FloodLink coordinator dashboard.
 *
 * STATE MANAGEMENT RULE (see CONTRIBUTING.md):
 * All shared state MUST go through this Context API. Do NOT add Redux, Zustand,
 * Jotai, or any other state management library. If state is needed in a single
 * component only, use local useState/useReducer there instead.
 *
 * HOW TO USE:
 *   1. Add your state field + setter to AppContextValue below.
 *   2. Initialise it in AppProvider.
 *   3. Consume it in any component with: const { yourField } = useAppContext()
 *
 * EXAMPLE PATTERN (no real state yet — placeholder only):
 *
 *   // In AppContextValue, add:
 *   currentWorkflowId: string | null
 *   setCurrentWorkflowId: (id: string | null) => void
 *
 *   // In AppProvider, add:
 *   const [currentWorkflowId, setCurrentWorkflowId] = useState<string | null>(null)
 *
 *   // Spread into the value object and use in components.
 */

import { createContext, useContext, type ReactNode } from 'react'

// ── Context value shape ────────────────────────────────────────────────────────

interface AppContextValue {
  /**
   * The API base URL, read from the VITE_API_BASE_URL environment variable.
   * Default: http://localhost:5000 (matches the Api project's default port).
   * Change by setting VITE_API_BASE_URL in your .env file (copy from .env.example).
   */
  apiBaseUrl: string

  // TODO: Add real shared state fields here as the team builds out the dashboard.
  // Example:
  //   currentUser: User | null
  //   setCurrentUser: (user: User | null) => void
}

// ── Context creation ───────────────────────────────────────────────────────────

const AppContext = createContext<AppContextValue | undefined>(undefined)

// ── Provider ───────────────────────────────────────────────────────────────────

/**
 * Wraps the application and provides shared state to all child components.
 * Mount this once at the React root (see src/main.tsx).
 */
export function AppProvider({ children }: { children: ReactNode }) {
  // Read API base URL from Vite environment variable.
  // VITE_ prefix is required for Vite to expose env vars to the client.
  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'

  // TODO: useState hooks for real shared state go here.
  // Example:
  //   const [currentUser, setCurrentUser] = useState<User | null>(null)

  const value: AppContextValue = {
    apiBaseUrl,
    // currentUser,
    // setCurrentUser,
  }

  return <AppContext.Provider value={value}>{children}</AppContext.Provider>
}

// ── Custom hook ────────────────────────────────────────────────────────────────

/**
 * Hook for consuming the AppContext. Must be called inside a component that is
 * a descendant of AppProvider.
 *
 * @throws Error if called outside AppProvider (guards against missing the provider).
 */
export function useAppContext(): AppContextValue {
  const ctx = useContext(AppContext)
  if (ctx === undefined) {
    throw new Error('useAppContext must be used within an AppProvider')
  }
  return ctx
}
