import type { ReactElement } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { Navbar } from './components/Navbar'
import { ProtectedRoute } from './components/ProtectedRoute'
import { LoginPage } from './components/LoginPage'
import { SheltersDashboard } from './components/SheltersDashboard'
import { ReportsQueue } from './components/ReportsQueue'
import { TriagePlanViewer } from './components/TriagePlanViewer'
import { InventoryDashboard } from './components/InventoryDashboard'
import { WorkflowMonitor } from './components/WorkflowMonitor'
import { WorkflowTrace } from './components/WorkflowTrace'
import { UsersAdmin } from './components/UsersAdmin'
import { DepotsPage } from './components/DepotsPage'
import { AllocationsPage } from './components/AllocationsPage'
import { StockHistoryPage } from './components/StockHistoryPage'
import ApprovalQueue from './components/ApprovalQueue'
import DispatchHistory from './components/DispatchHistory'
import Analytics from './components/Analytics'
import { useAuth } from './context/AuthContext'
import { PAGES, homePathFor } from './routes'

const ELEMENTS: Record<string, ReactElement> = {
  '/shelters': <SheltersDashboard />,
  '/reports': <ReportsQueue />,
  '/triage': <TriagePlanViewer />,
  '/workflows': <WorkflowMonitor />,
  '/inventory': <InventoryDashboard />,
  '/depots': <DepotsPage />,
  '/allocations': <AllocationsPage />,
  '/stock-history': <StockHistoryPage />,
  '/approvals': <ApprovalQueue />,
  '/dispatches': <DispatchHistory />,
  '/analytics': <Analytics />,
  '/users': <UsersAdmin />,
}

function App() {
  const { role } = useAuth()

  return (
    <div className="min-h-screen bg-slate-100 flex flex-col font-sans">
      <Navbar />

      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          {PAGES.map((page) => (
            <Route
              key={page.path}
              path={page.path}
              element={<ProtectedRoute roles={page.roles}>{ELEMENTS[page.path]}</ProtectedRoute>}
            />
          ))}
          <Route
            path="/workflows/:id"
            element={<ProtectedRoute roles={['Coordinator']}><WorkflowTrace /></ProtectedRoute>}
          />
          <Route path="*" element={<Navigate to={homePathFor(role)} replace />} />
        </Routes>
      </main>
    </div>
  )
}

export default App
