import React from 'react'
import ReactDOM from 'react-dom/client'
import { AppProvider } from './context/AppContext'
import App from './App'

// ── Global styles ─────────────────────────────────────────────────────────────
// index.scss loads in this order:
//   1. Carbon g10 theme (Sass @use)
//   2. Carbon component styles
//   3. IBM Plex fonts
//   4. FloodLink design token layer (CSS custom properties)
// styles.css has been superseded by index.scss — do not re-import it.
// ─────────────────────────────────────────────────────────────────────────────
import './index.scss'

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
