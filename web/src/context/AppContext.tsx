// ── FloodLink AI — App Context ────────────────────────────────────────────────
//
// Global React context for shared application state.
// Currently a minimal stub — expand as features require shared state.
//
// USAGE:
//   import { useAppContext } from '../context/AppContext';
//   const { apiBaseUrl } = useAppContext();
//
// Adding new shared state:
//   1. Add the field to AppContextValue
//   2. Add it to the useState/useCallback set in AppProvider
//   3. Expose it in the context value object
// ─────────────────────────────────────────────────────────────────────────────

import { createContext, useContext, useState, type ReactNode } from 'react';
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

// ── Context value shape ────────────────────────────────────────────────────────

interface AppContextValue {
  /** FloodLink backend base URL. Override via VITE_API_BASE_URL in .env */
  apiBaseUrl: string;
  // Add shared fields here as features are built:
  // currentUser: User | null;
  // activeWorkflowId: string | null;
}

const AppContext = createContext<AppContextValue | undefined>(undefined);

export function AppProvider({ children }: { children: ReactNode }) {
  const [apiBaseUrl] = useState(
    import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000',
  );

  return (
    <AppContext.Provider value={{ apiBaseUrl }}>
      {children}
    </AppContext.Provider>
  );
}

export function useAppContext(): AppContextValue {
  const ctx = useContext(AppContext);
  if (ctx === undefined) {
    throw new Error('useAppContext must be used inside <AppProvider>');
  }
  return ctx;
}
