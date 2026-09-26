import { useState } from 'react'
import ApprovalQueue from './components/ApprovalQueue'
import DispatchHistory from './components/DispatchHistory'
import Analytics from './components/Analytics'

/**
 * App — root component. Coordinator dashboard shell with a simple tab nav.
 * Member D's three screens live behind tabs (no routing library yet — do not
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
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="max-w-5xl mx-auto px-4 py-3 flex items-center justify-between gap-4">
          <h1 className="text-lg font-bold text-gray-900 shrink-0">FloodLink Coordinator</h1>
          <nav className="flex gap-1 overflow-x-auto">
            {TABS.map((t) => (
              <button
                key={t.id}
                type="button"
                onClick={() => setTab(t.id)}
                className={`px-3 py-1.5 rounded-lg text-sm font-medium whitespace-nowrap transition-colors ${
                  tab === t.id
                    ? 'bg-gray-900 text-white'
                    : 'text-gray-600 hover:bg-gray-100'
                }`}
              >
                {t.label}
              </button>
            ))}
          </nav>
        </div>
      </header>

      <main>
        {tab === 'queue' && <ApprovalQueue />}
        {tab === 'history' && <DispatchHistory />}
        {tab === 'analytics' && <Analytics />}
      </main>
    </div>
  )
}

export default App