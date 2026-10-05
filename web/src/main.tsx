import React from 'react'
import ReactDOM from 'react-dom/client'
import { AppProvider } from './context/AppContext'
import App from './App'
import './index.css'

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    {/*
      AppProvider wraps the entire app so that any component in the tree can
      consume shared state via useAppContext() without prop-drilling.
      See src/context/AppContext.tsx for the current context shape.
    */}
    <AppProvider>
      <App />
    </AppProvider>
  </React.StrictMode>,
)
