// ── FloodLink AI — App Root ───────────────────────────────────────────────────
//
// Thin root component — delegates everything to AppShell.
// Keep this file minimal: routing, auth guards, and global error boundaries
// should wrap AppShell here as the app grows.
// ─────────────────────────────────────────────────────────────────────────────

import AppShell from './components/AppShell';

export default function App() {
  return <AppShell />;
}
