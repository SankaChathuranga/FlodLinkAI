import { useCallback, useEffect, useState, type FormEvent } from 'react'
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

  const input = 'mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500'

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-slate-900">User management</h1>
        <p className="text-sm text-slate-500">Volunteers register in the mobile app. Create staff accounts here.</p>
      </div>

      <form
        onSubmit={(e) => void onSubmit(e)}
        noValidate
        aria-labelledby="create-staff-heading"
        className="bg-white rounded-lg border border-slate-200 shadow-sm p-5 grid gap-4 sm:grid-cols-2 lg:grid-cols-5 items-end"
      >
        <h2 id="create-staff-heading" className="sm:col-span-2 lg:col-span-5 font-semibold text-slate-800">Create staff account</h2>
        <label className="text-sm font-medium text-slate-700">
          Name
          <input className={input} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
        </label>
        <label className="text-sm font-medium text-slate-700">
          Email
          <input type="email" className={input} value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
        </label>
        <label className="text-sm font-medium text-slate-700">
          Phone
          <input className={input} value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
        </label>
        <label className="text-sm font-medium text-slate-700">
          Temporary password
          <input
            type="password"
            autoComplete="new-password"
            className={input}
            value={form.password}
            onChange={(e) => setForm({ ...form, password: e.target.value })}
          />
        </label>
        <label className="text-sm font-medium text-slate-700">
          Role
          <select className={input} value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value as StaffRole })}>
            <option value="Coordinator">Coordinator</option>
            <option value="DepotManager">Depot manager</option>
          </select>
        </label>
        <div className="sm:col-span-2 lg:col-span-5 flex flex-wrap items-center gap-3">
          <button
            type="submit"
            disabled={saving}
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-60"
          >
            {saving ? 'Creating…' : 'Create account'}
          </button>
          {formError && <p role="alert" className="text-sm text-red-700">{formError}</p>}
          {success && <p role="status" className="text-sm text-green-700">{success}</p>}
        </div>
      </form>

      <section className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-x-auto">
        {loading ? (
          <p className="p-5 text-sm text-slate-500">Loading users…</p>
        ) : loadError ? (
          <div role="alert" className="p-5 text-sm text-red-700">
            {loadError}{' '}
            <button className="underline" onClick={() => void loadUsers()}>Retry</button>
          </div>
        ) : users.length === 0 ? (
          <p className="p-5 text-sm text-slate-500">No accounts yet.</p>
        ) : (
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-left text-slate-600">
              <tr>
                <th className="px-4 py-2 font-medium">Name</th>
                <th className="px-4 py-2 font-medium">Email</th>
                <th className="px-4 py-2 font-medium">Role</th>
                <th className="px-4 py-2 font-medium">Phone</th>
                <th className="px-4 py-2 font-medium">Created</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {users.map((u) => (
                <tr key={u.id}>
                  <td className="px-4 py-2 text-slate-900">{u.name}</td>
                  <td className="px-4 py-2 text-slate-700">{u.email ?? '—'}</td>
                  <td className="px-4 py-2 text-slate-700">{u.role}</td>
                  <td className="px-4 py-2 text-slate-700">{u.phone}</td>
                  <td className="px-4 py-2 text-slate-500">{new Date(u.createdAt).toLocaleDateString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  )
}
