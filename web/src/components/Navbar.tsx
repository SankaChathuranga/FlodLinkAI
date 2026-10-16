import { NavLink, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { PAGES, canOpen } from '../routes'

/** Top bar: only the links the signed-in role may open, plus the user and a sign-out button. */
export function Navbar() {
  const { user, role, logout } = useAuth()
  const navigate = useNavigate()
  const links = PAGES.filter((p) => canOpen(p, role))

  const signOut = () => {
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <header className="bg-slate-900 text-white shadow-md border-b border-slate-800">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex flex-wrap items-center justify-between gap-3 py-3">
          {/* Logo & Branding */}
          <div className="flex items-center space-x-3">
            <div className="bg-blue-600 p-2 rounded-lg text-white">
              <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
              </svg>
            </div>
            <div>
              <span className="font-bold text-lg tracking-wide text-white">FloodLink AI</span>
              <span className="text-xs text-blue-400 block font-medium">Relief Coordination Portal</span>
            </div>
          </div>

          {user && (
            <div className="flex items-center gap-3 text-sm">
              <span className="text-slate-300">
                {user.name} <span className="text-slate-500">·</span>{' '}
                <span className="text-blue-300">{role}</span>
              </span>
              <button
                onClick={signOut}
                className="px-3 py-1.5 rounded-md border border-slate-700 text-slate-200 hover:bg-slate-800 hover:text-white"
              >
                Sign out
              </button>
            </div>
          )}
        </div>

        {user && (
          <nav aria-label="Main" className="flex flex-wrap gap-2 pb-3">
            {links.map((page) => (
              <NavLink
                key={page.path}
                to={page.path}
                className={({ isActive }) =>
                  `px-3 py-2 rounded-md text-xs sm:text-sm font-medium transition-colors ${
                    isActive ? 'bg-blue-600 text-white shadow' : 'text-slate-300 hover:bg-slate-800 hover:text-white'
                  }`
                }
              >
                {page.label}
              </NavLink>
            ))}
          </nav>
        )}
      </div>
    </header>
  )
}
