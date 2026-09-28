import { useState } from 'react'
import DepotManagement from './pages/DepotManagement'
import InventoryDashboard from './pages/InventoryDashboard'

function App() {
  const [activePage, setActivePage] = useState<'inventory' | 'depots'>('inventory')

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="brand-lockup">
          <span className="brand-mark">FL</span>
          <div>
            <p className="brand-name">FloodLink</p>
            <p className="brand-caption">Relief coordination console</p>
          </div>
        </div>
        <nav className="app-nav" aria-label="Primary navigation">
          <button className={activePage === 'inventory' ? 'nav-item active' : 'nav-item'} onClick={() => setActivePage('inventory')}>
            Inventory
          </button>
          <button className={activePage === 'depots' ? 'nav-item active' : 'nav-item'} onClick={() => setActivePage('depots')}>
            Depots
          </button>
        </nav>
        <span className="system-status"><span className="status-dot" /> System online</span>
      </header>

      <main className="app-content">
        {activePage === 'inventory' ? <InventoryDashboard /> : <DepotManagement />}
      </main>
    </div>
  )
}

export default App
