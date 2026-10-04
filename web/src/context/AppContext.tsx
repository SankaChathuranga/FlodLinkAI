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

import { createContext, useContext, useState, ReactNode } from 'react';

interface AppContextValue {
  /** FloodLink backend base URL. Override via VITE_API_BASE_URL in .env */
  apiBaseUrl: string;
  // Add shared fields here as features are built:
  // currentUser: User | null;
  // activeWorkflowId: string | null;
}

const AppContext = createContext<AppContextValue | null>(null);

export function AppProvider({ children }: { children: ReactNode }) {
  const [apiBaseUrl] = useState(
    import.meta.env['VITE_API_BASE_URL'] ?? 'http://localhost:5000'
  );

  return (
    <AppContext.Provider value={{ apiBaseUrl }}>
      {children}
    </AppContext.Provider>
  );
}

export function useAppContext(): AppContextValue {
  const ctx = useContext(AppContext);
  if (!ctx) throw new Error('useAppContext must be used inside <AppProvider>');
  return ctx;
}
