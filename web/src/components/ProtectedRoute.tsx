import type { ReactNode } from 'react'
import { Tile } from '@carbon/react'
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
      <Tile role="alert" className="fl-denied">
        <h1 className="cds--heading-03">You can’t open this page</h1>
        <p className="cds--body-compact-01">
          Your role ({role}) does not have access to {location.pathname}.
        </p>
        <Link to={homePathFor(role)} className="cds--link">
          Go to your home page
        </Link>
      </Tile>
    )
  }

  return <>{children}</>
}
