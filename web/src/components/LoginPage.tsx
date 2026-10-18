import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { Button, Form, InlineNotification, PasswordInput, Stack, TextInput, Tile } from '@carbon/react'
import { LoginError, useAuth } from '../context/AuthContext'
import { homePathFor } from '../routes'

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/** Coordinator / depot manager / admin sign-in. Volunteers are told to use the mobile app. */
export function LoginPage() {
  const { user, role, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<{ email?: string; password?: string }>({})
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  if (user) {
    return <Navigate to={homePathFor(role)} replace />
  }

  const validate = () => {
    const next: typeof fieldErrors = {}
    if (!email.trim()) next.email = 'Enter your email address.'
    else if (!EMAIL_PATTERN.test(email.trim())) next.email = 'Enter a valid email address.'
    if (!password) next.password = 'Enter your password.'
    setFieldErrors(next)
    return Object.keys(next).length === 0
  }

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    if (!validate()) return
    setSubmitting(true)
    try {
      const signedIn = await login(email, password)
      navigate(from && from !== '/login' ? from : homePathFor(signedIn.role), { replace: true })
    } catch (err) {
      setError(err instanceof LoginError ? err.message : 'Sign-in failed. Try again.')
      setSubmitting(false)
    }
  }

  return (
    <div className="fl-login">
      <Tile className="fl-login__tile">
        <Form onSubmit={(e) => void onSubmit(e)} noValidate aria-labelledby="login-heading">
          <Stack gap={6}>
            <div>
              <h1 id="login-heading" className="cds--heading-05">Sign in</h1>
              <p className="cds--body-compact-01 fl-text-muted">Coordinators, depot managers and administrators.</p>
            </div>

            {error && (
              <InlineNotification kind="error" role="alert" title="Sign-in failed" subtitle={error} lowContrast hideCloseButton />
            )}

            <TextInput
              id="login-email"
              type="email"
              labelText="Email"
              autoComplete="username"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              invalid={Boolean(fieldErrors.email)}
              invalidText={fieldErrors.email}
            />

            <PasswordInput
              id="login-password"
              labelText="Password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              invalid={Boolean(fieldErrors.password)}
              invalidText={fieldErrors.password}
              showPasswordLabel="Show password"
              hidePasswordLabel="Hide password"
            />

            <Button type="submit" disabled={submitting} className="fl-login__submit">
              {submitting ? 'Signing in…' : 'Sign in'}
            </Button>

            <p className="cds--helper-text-01">
              No account? Staff accounts are created by an administrator. Field volunteers sign up in the
              FloodLink mobile app.
            </p>
          </Stack>
        </Form>
      </Tile>
    </div>
  )
}
