import { useState } from 'react'
import ApprovalQueue from './components/ApprovalQueue'
import DispatchHistory from './components/DispatchHistory'
import Analytics from './components/Analytics'

/**
 * App – root component. Coordinator dashboard shell with a Carbon-style tab nav.
 * Member D's three screens live behind tabs (no routing library yet – do not
 * add one without team agreement; see CONTRIBUTING.md).
 */

type Tab = 'queue' | 'history' | 'analytics'

const TABS: { id: Tab; label: string }[] = [
  { id: 'queue', label: 'Approval Queue' },
  { id: 'history', label: 'Dispatch History' },
  { id: 'analytics', label: 'Analytics' },
]

function App() {
  const [tab, setTab] = useState<Tab>('queue')

  return (
    <div style={{ minHeight: '100vh', backgroundColor: 'var(--bg-base)' }}>
      {/* Carbon-style dark top header */}
      <header className="fl-header">
        <div className="fl-header-inner">
          <span className="fl-header-title">FloodLink — Coordinator</span>
          <nav style={{ display: 'flex', gap: 0, overflow: 'hidden' }}>
            {TABS.map((t) => (
              <button
                key={t.id}
                type="button"
                onClick={() => setTab(t.id)}
                className={`fl-tab${tab === t.id ? ' active' : ''}`}
              >
                {t.label}
              </button>
            ))}
          </nav>
        </div>
      </header>

      <main style={{ maxWidth: '80rem', margin: '0 auto', padding: '1.5rem 1rem' }}>
        {tab === 'queue' && <ApprovalQueue />}
        {tab === 'history' && <DispatchHistory />}
        {tab === 'analytics' && <Analytics />}
      </main>
    </div>
  )
}

export default App
