import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { AppProvider } from '../context/AppContext'
import { AuthProvider } from '../context/AuthContext'
import { saveSession, type Role } from '../auth/session'
import { apiFetch } from '../api/http'
import { LoginPage } from './LoginPage'
import { ProtectedRoute } from './ProtectedRoute'

// WEB-AUTH: route guard, login form validation and error states, token header and 401 handling.

function signIn(role: Role) {
  saveSession({
    token: `token-${role}`,
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: { id: 1, name: 'Test User', email: 'test@floodlink.lk', role, phone: '+94770000000', createdAt: '' },
  })
}

function renderAt(path: string) {
  return render(
    <AppProvider>
      <AuthProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/inventory" element={<ProtectedRoute roles={['DepotManager']}><p>Inventory page</p></ProtectedRoute>} />
            <Route path="/approvals" element={<ProtectedRoute roles={['Coordinator']}><p>Approvals page</p></ProtectedRoute>} />
            <Route path="/shelters" element={<ProtectedRoute roles={['Coordinator']}><p>Shelters page</p></ProtectedRoute>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </AppProvider>,
  )
}

afterEach(() => sessionStorage.clear())

describe('ProtectedRoute', () => {
  it('sends a signed-out user to the login page', () => {
    renderAt('/approvals')
    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.queryByText('Approvals page')).not.toBeInTheDocument()
  })

  it('blocks a DepotManager from /approvals', () => {
    signIn('DepotManager')
    renderAt('/approvals')
    expect(screen.getByRole('alert')).toHaveTextContent(/can’t open this page/)
    expect(screen.queryByText('Approvals page')).not.toBeInTheDocument()
  })

  it('lets an Admin into every page', () => {
    signIn('Admin')
    renderAt('/inventory')
    expect(screen.getByText('Inventory page')).toBeInTheDocument()
  })
})

describe('LoginPage', () => {
  it('validates the fields before calling the API', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    renderAt('/login')

    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(screen.getByText('Enter your email address.')).toBeInTheDocument()
    expect(screen.getByText('Enter your password.')).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Email'), 'not-an-email')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('shows an error for a wrong password', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ title: 'Invalid email or password.' }), { status: 401 })))
    renderAt('/login')

    await userEvent.type(screen.getByLabelText('Email'), 'coordinator@floodlink.lk')
    await userEvent.type(screen.getByLabelText('Password'), 'wrong-password')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect((await screen.findByText('Incorrect email or password.')).closest('[role="alert"]')).not.toBeNull()
  })

  it('refuses a Volunteer account with a pointer to the mobile app', async () => {
    const session = {
      token: 't', expiresAt: new Date(Date.now() + 3600_000).toISOString(),
      user: { id: 1, name: 'V', email: 'volunteer@floodlink.lk', role: 'Volunteer', phone: '', createdAt: '' },
    }
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify(session), { status: 200 })))
    renderAt('/login')

    await userEvent.type(screen.getByLabelText('Email'), 'volunteer@floodlink.lk')
    await userEvent.type(screen.getByLabelText('Password'), 'any-password')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect((await screen.findByText(/mobile app/, { selector: '.cds--inline-notification__subtitle' })).closest('[role="alert"]')).not.toBeNull()
    expect(sessionStorage.length).toBe(0)
  })

  it('signs a Coordinator in and opens the page they asked for', async () => {
    const session = {
      token: 't', expiresAt: new Date(Date.now() + 3600_000).toISOString(),
      user: { id: 2, name: 'C', email: 'coordinator@floodlink.lk', role: 'Coordinator', phone: '', createdAt: '' },
    }
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify(session), { status: 200 })))
    renderAt('/approvals')

    await userEvent.type(screen.getByLabelText('Email'), 'coordinator@floodlink.lk')
    await userEvent.type(screen.getByLabelText('Password'), 'right-password')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Approvals page')).toBeInTheDocument()
  })
})

describe('apiFetch', () => {
  it('sends the bearer token and clears the session on 401', async () => {
    signIn('Coordinator')
    const fetchMock = vi.fn(async () => new Response(null, { status: 401 }))
    vi.stubGlobal('fetch', fetchMock)

    await apiFetch('http://api/x')

    const headers = (fetchMock.mock.calls[0] as unknown as [string, RequestInit])[1].headers as Headers
    expect(headers.get('Authorization')).toBe('Bearer token-Coordinator')
    expect(sessionStorage.length).toBe(0)
  })
})
