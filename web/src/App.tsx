import ApprovalQueue from './components/ApprovalQueue'

/**
 * App — root component. Renders the coordinator dashboard.
 *
 * Currently shows Member D's Approval Queue as the single working screen.
 * As the team adds React Router (when agreed), routing will live here.
 *
 * NOTE: No routing library is added yet — do not add one without team agreement.
 * See CONTRIBUTING.md for the React state-management rules.
 */
function App() {
  return (
    <div className="min-h-screen bg-gray-50">
      <main>
        <ApprovalQueue />
      </main>
    </div>
  )
}

export default App
