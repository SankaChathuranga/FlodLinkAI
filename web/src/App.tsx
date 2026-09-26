import { useState } from 'react'
import { Navbar } from './components/Navbar'
import { SheltersDashboard } from './components/SheltersDashboard'
import { ReportsQueue } from './components/ReportsQueue'
import { TriagePlanViewer } from './components/TriagePlanViewer'

function App() {
  const [activeTab, setActiveTab] = useState<'shelters' | 'reports' | 'triage'>('shelters')

  return (
    <div className="min-h-screen bg-slate-100 flex flex-col font-sans">
      <Navbar activeTab={activeTab} setActiveTab={setActiveTab} />

      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {activeTab === 'shelters' && <SheltersDashboard />}
        {activeTab === 'reports' && <ReportsQueue />}
        {activeTab === 'triage' && <TriagePlanViewer />}
      </main>
    </div>
  )
}

export default App
