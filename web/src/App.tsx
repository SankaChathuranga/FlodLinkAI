import { useAppContext } from './context/AppContext'

/**
 * App — root component. Currently renders the placeholder dashboard page.
 *
 * As the team adds React Router (when agreed), the router and top-level routes
 * will live here. Until then, render the single placeholder screen.
 *
 * NOTE: No routing library is added yet — do not add one without team agreement.
 * See CONTRIBUTING.md for the React state-management rules.
 */
function App() {
  // Example: reading a value from the shared AppContext.
  // Replace this with real state once the context shape is finalized.
  const { apiBaseUrl } = useAppContext()

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col items-center justify-center">
      {/* ── Tailwind smoke-test ── if the box below is blue, Tailwind is working ── */}
      <div className="bg-blue-600 text-white text-xs font-mono px-2 py-1 rounded mb-6 opacity-50">
        Tailwind CSS ✓
      </div>

      <main className="text-center px-4">
        <h1 className="text-4xl font-bold text-gray-900 mb-4">
          FloodLink Dashboard
        </h1>
        <p className="text-xl text-gray-500 mb-8">— under construction —</p>

        <div className="bg-white rounded-xl shadow p-6 max-w-md mx-auto text-left text-sm text-gray-600 space-y-2">
          <p>
            <span className="font-semibold text-gray-800">API base URL:</span>{' '}
            <code className="bg-gray-100 px-1 rounded">{apiBaseUrl}</code>
          </p>
          <p className="text-xs text-gray-400">
            Set <code>VITE_API_BASE_URL</code> in <code>.env</code> to change this.
            Copy <code>.env.example</code> to get started.
          </p>
        </div>
      </main>
    </div>
  )
}

export default App
