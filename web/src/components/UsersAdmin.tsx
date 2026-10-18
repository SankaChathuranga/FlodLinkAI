import { useCallback, useEffect, useState, type FormEvent } from 'react'
import {
  Button,
  Form,
  InlineLoading,
  InlineNotification,
  PasswordInput,
  Select,
  SelectItem,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableHeader,
  TableRow,
  TextInput,
  Tile,
} from '@carbon/react'
import { apiFetch } from '../api/http'
import type { SessionUser } from '../auth/session'
import { useAppContext } from '../context/AppContext'

type StaffRole = 'Coordinator' | 'DepotManager'

const EMPTY_FORM = { name: '', email: '', phone: '', password: '', role: 'Coordinator' as StaffRole }

/** Admin page: every account, plus a form to create Coordinator and DepotManager accounts. */
export function UsersAdmin() {
  const { apiBaseUrl } = useAppContext()
  const [users, setUsers] = useState<SessionUser[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)

  const [form, setForm] = useState(EMPTY_FORM)
  const [formError, setFormError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const loadUsers = useCallback(async () => {
    setLoading(true)
    setLoadError(null)
    try {
      const res = await apiFetch(`${apiBaseUrl}/api/users`)
      if (!res.ok) throw new Error(`Could not load users (${res.status}).`)
      setUsers((await res.json()) as SessionUser[])
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : 'Could not load users.')
    } finally {
      setLoading(false)
    }
  }, [apiBaseUrl])

  useEffect(() => {
    void loadUsers()
  }, [loadUsers])

  const validate = (): string | null => {
    if (!form.name.trim()) return 'Enter a name.'
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) return 'Enter a valid email address.'
    if (!/^\+?[0-9 ]{7,20}$/.test(form.phone.trim())) return 'Phone must be 7–20 digits, optionally starting with +.'
    if (form.password.length < 8) return 'Password must be at least 8 characters.'
    return null
  }

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setSuccess(null)
    const problem = validate()
    setFormError(problem)
    if (problem) return

    setSaving(true)
    try {
      const res = await apiFetch(`${apiBaseUrl}/api/users`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ...form, name: form.name.trim(), email: form.email.trim(), phone: form.phone.trim() }),
      })
      if (res.status === 409) throw new Error('An account with this email already exists.')
      if (!res.ok) {
        const body = (await res.json().catch(() => null)) as { message?: string; title?: string } | null
        throw new Error(body?.message ?? body?.title ?? `Could not create the account (${res.status}).`)
      }
      const created = (await res.json()) as SessionUser
      setSuccess(`Created ${created.role} account for ${created.email}.`)
      setForm(EMPTY_FORM)
      void loadUsers()
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Could not create the account.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <section aria-labelledby="users-heading">
      <Stack gap={6}>
        <div>
          <h1 id="users-heading" className="cds--heading-04">User management</h1>
          <p className="cds--body-compact-01 fl-text-muted">Volunteers register in the mobile app. Create staff accounts here.</p>
        </div>

        <Tile>
          <Form onSubmit={(e) => void onSubmit(e)} noValidate aria-labelledby="create-staff-heading">
            <Stack gap={5}>
              <h2 id="create-staff-heading" className="cds--heading-03">Create staff account</h2>
              <div className="fl-form-grid">
                <TextInput id="staff-name" labelText="Name" maxLength={100} value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })} />
                <TextInput id="staff-email" type="email" labelText="Email" autoComplete="off" value={form.email}
                  onChange={(e) => setForm({ ...form, email: e.target.value })} />
                <TextInput id="staff-phone" type="tel" labelText="Phone" placeholder="+94 77 123 4567" value={form.phone}
                  onChange={(e) => setForm({ ...form, phone: e.target.value })} />
                <PasswordInput id="staff-password" labelText="Temporary password" autoComplete="new-password" value={form.password}
                  helperText="At least 8 characters."
                  onChange={(e) => setForm({ ...form, password: e.target.value })} />
                <Select id="staff-role" labelText="Role" value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value as StaffRole })}>
                  <SelectItem value="Coordinator" text="Coordinator" />
                  <SelectItem value="DepotManager" text="Depot manager" />
                </Select>
              </div>
              {formError && <InlineNotification kind="error" role="alert" title="Account not created" subtitle={formError} lowContrast hideCloseButton />}
              {success && <InlineNotification kind="success" role="status" title={success} lowContrast onCloseButtonClick={() => setSuccess(null)} />}
              <div>
                <Button type="submit" disabled={saving}>{saving ? 'Creating…' : 'Create account'}</Button>
              </div>
            </Stack>
          </Form>
        </Tile>

        {loading ? (
          <InlineLoading description="Loading users…" />
        ) : loadError ? (
          <div>
            <InlineNotification kind="error" role="alert" title="Users could not be loaded" subtitle={loadError} lowContrast hideCloseButton />
            <Button kind="tertiary" size="sm" onClick={() => void loadUsers()}>Retry</Button>
          </div>
        ) : users.length === 0 ? (
          <p className="cds--body-compact-01">No accounts yet.</p>
        ) : (
          <TableContainer title="Accounts">
            <Table aria-label="Accounts">
              <TableHead>
                <TableRow>
                  <TableHeader>Name</TableHeader>
                  <TableHeader>Email</TableHeader>
                  <TableHeader>Role</TableHeader>
                  <TableHeader>Phone</TableHeader>
                  <TableHeader>Created</TableHeader>
                </TableRow>
              </TableHead>
              <TableBody>
                {users.map((u) => (
                  <TableRow key={u.id}>
                    <TableCell>{u.name}</TableCell>
                    <TableCell>{u.email ?? '—'}</TableCell>
                    <TableCell>{u.role}</TableCell>
                    <TableCell>{u.phone}</TableCell>
                    <TableCell>{new Date(u.createdAt).toLocaleDateString()}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Stack>
    </section>
  )
}
