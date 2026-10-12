import type { ReactNode } from 'react'
import { Link, Navigate, useLocation } from 'react-router-dom'
import type { Role } from '../auth/session'
import { useAuth } from '../context/AuthContext'
import { homePathFor } from '../routes'

interface ProtectedRouteProps {
  /** Roles allowed in (Admin always is). Empty = Admin only. */
  roles: Role[]
  children: ReactNode
}

/** Sends signed-out users to /login (remembering where they were going) and shows a notice for the wrong role. */
export function ProtectedRoute({ roles, children }: ProtectedRouteProps) {
  const { user, role, hasRole } = useAuth()
  const location = useLocation()

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  if (!hasRole(...roles)) {
    return (
      <div role="alert" className="max-w-xl mx-auto mt-12 bg-white border border-slate-200 rounded-lg p-6 text-center shadow-sm">
        <h1 className="text-lg font-semibold text-slate-900">You can’t open this page</h1>
        <p className="mt-2 text-sm text-slate-600">
          Your role ({role}) does not have access to {location.pathname}.
        </p>
        <Link to={homePathFor(role)} className="mt-4 inline-block text-sm font-medium text-blue-700 hover:underline">
          Go to your home page
        </Link>
      </div>
    )
  }

  return <>{children}</>
}
