import { useCallback, useEffect, useRef, useState } from 'react'
import {
  Button,
  InlineLoading,
  InlineNotification,
  Modal,
  NumberInput,
  Pagination,
  Search,
  Select,
  SelectItem,
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
import { Add, Edit, Renew } from '@carbon/icons-react'
import { useAppContext } from '../context/AppContext'
import { useAuth } from '../context/AuthContext'
import { describeApiError } from '../lib/apiError'
import { useDebouncedValue } from '../lib/useDebouncedValue'
import { createDepot, listDepotManagers, listDepots, updateDepot, type DepotInput, type DepotManagerOption } from '../api/stock'
import type { Depot } from '../types'

const EMPTY: DepotInput = { name: '', latitude: 6.9271, longitude: 79.8612, managerId: null }

/**
 * Depot management (8.01, Member B): list depots with their manager and stock lines; depot
 * managers and admins add depots, edit name and position, and assign the responsible manager.
 * Coordinators see the list read-only.
 */
export function DepotsPage() {
  const { apiBaseUrl } = useAppContext()
  const canEdit = useAuth().hasRole('DepotManager')

  const [depots, setDepots] = useState<Depot[]>([])
  const [totalItems, setTotalItems] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebouncedValue(search.trim())
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const [managers, setManagers] = useState<DepotManagerOption[]>([])
  const [editing, setEditing] = useState<{ id: number | null; form: DepotInput } | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => setPage(1), [debouncedSearch])

  // Only the latest request may update the table.
  const latestRequest = useRef(0)
  const load = useCallback(async () => {
    const requestId = ++latestRequest.current
    setLoading(true)
    setError(null)
    try {
      const data = await listDepots(apiBaseUrl, { search: debouncedSearch || undefined, page, pageSize })
      if (requestId !== latestRequest.current) return
      setDepots(data.items)
      setTotalItems(data.totalItems)
    } catch (err) {
      if (requestId === latestRequest.current) setError(describeApiError(err))
    } finally {
      if (requestId === latestRequest.current) setLoading(false)
    }
  }, [apiBaseUrl, debouncedSearch, page, pageSize])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    if (!canEdit) return
    listDepotManagers(apiBaseUrl).then(setManagers).catch(() => setManagers([]))
  }, [apiBaseUrl, canEdit])

  const openForm = (depot: Depot | null) => {
    setFormError(null)
    setNotice(null)
    setEditing(depot
      ? { id: depot.id, form: { name: depot.name, latitude: depot.latitude, longitude: depot.longitude, managerId: depot.managerId ?? null } }
      : { id: null, form: { ...EMPTY } })
  }

  const setField = <K extends keyof DepotInput>(key: K, value: DepotInput[K]) =>
    setEditing((current) => (current ? { ...current, form: { ...current.form, [key]: value } } : current))

  const validate = (form: DepotInput): string | null => {
    if (!form.name.trim()) return 'Enter the depot name.'
    if (!Number.isFinite(form.latitude) || form.latitude < -90 || form.latitude > 90) return 'Latitude must be between -90 and 90.'
    if (!Number.isFinite(form.longitude) || form.longitude < -180 || form.longitude > 180) return 'Longitude must be between -180 and 180.'
    if (form.latitude === 0 && form.longitude === 0) return 'Set the depot position; (0, 0) is not a real location.'
    return null
  }

  const save = async () => {
    if (!editing) return
    const problem = validate(editing.form)
    if (problem) {
      setFormError(problem)
      return
    }
    setSaving(true)
    setFormError(null)
    try {
      const body = { ...editing.form, name: editing.form.name.trim() }
      if (editing.id === null) await createDepot(apiBaseUrl, body)
      else await updateDepot(apiBaseUrl, editing.id, body)
      setNotice(editing.id === null ? `Depot “${body.name}” added.` : `Depot “${body.name}” saved.`)
      setEditing(null)
      await load()
    } catch (err) {
      setFormError(describeApiError(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section aria-label="Depot management">
      <Tile>
        <div className="inventory-dashboard__header">
          <div>
            <h2 className="cds--heading-04">Depots</h2>
            <p className="cds--body-compact-01">Supply depots, where they are, and who manages them.</p>
          </div>
          <div className="inventory-dashboard__actions">
            <Button kind="secondary" renderIcon={Renew} onClick={() => void load()} disabled={loading}>Refresh</Button>
            {canEdit && <Button renderIcon={Add} onClick={() => openForm(null)}>Add depot</Button>}
          </div>
        </div>

        {error && <InlineNotification kind="error" title="Depots could not be loaded" subtitle={error} lowContrast hideCloseButton />}
        {notice && <InlineNotification kind="success" title={notice} lowContrast onCloseButtonClick={() => setNotice(null)} />}

        <div className="inventory-dashboard__toolbar">
          <Search size="md" labelText="Search depots" placeholder="Search depot or manager" value={search}
            onChange={(event) => setSearch(event.target.value)} closeButtonLabelText="Clear search" />
        </div>

        {loading ? <InlineLoading description="Loading depots…" /> : depots.length === 0 && !error ? (
          <p className="cds--body-compact-01 inventory-dashboard__empty">
            {debouncedSearch ? 'No depots match this search.' : 'No depots yet.'}
          </p>
        ) : (
          <TableContainer title="All depots">
            <Table aria-label="Depots">
              <TableHead>
                <TableRow>
                  <TableHeader>Depot</TableHeader>
                  <TableHeader>Position</TableHeader>
                  <TableHeader>Manager</TableHeader>
                  <TableHeader>Stock lines</TableHeader>
                  <TableHeader>Last updated</TableHeader>
                  {canEdit && <TableHeader>Action</TableHeader>}
                </TableRow>
              </TableHead>
              <TableBody>
                {depots.map((depot) => (
                  <TableRow key={depot.id}>
                    <TableCell>{depot.name}</TableCell>
                    <TableCell>{depot.latitude.toFixed(4)}, {depot.longitude.toFixed(4)}</TableCell>
                    <TableCell>{depot.managerName ?? 'Not assigned'}</TableCell>
                    <TableCell>{depot.itemCount ?? 0}</TableCell>
                    <TableCell>{new Date(depot.updatedAt ?? depot.createdAt).toLocaleString()}</TableCell>
                    {canEdit && (
                      <TableCell>
                        <Button kind="ghost" size="sm" renderIcon={Edit} onClick={() => openForm(depot)} aria-label={`Edit ${depot.name}`}>
                          Edit
                        </Button>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}

        {totalItems > 0 && (
          <Pagination page={page} pageSize={pageSize} pageSizes={[10, 20, 50]} totalItems={totalItems}
            onChange={({ page: nextPage, pageSize: nextSize }) => { setPage(nextPage); setPageSize(nextSize) }} />
        )}
      </Tile>

      <Modal
        open={editing !== null}
        modalHeading={editing?.id === null ? 'Add depot' : 'Edit depot'}
        primaryButtonText={saving ? 'Saving…' : 'Save depot'}
        secondaryButtonText="Cancel"
        primaryButtonDisabled={saving}
        onRequestSubmit={() => void save()}
        onRequestClose={() => !saving && setEditing(null)}
      >
        {formError && <InlineNotification kind="error" title="Depot not saved" subtitle={formError} lowContrast hideCloseButton />}
        {editing && (
          <div className="depot-form">
            <TextInput id="depot-name" labelText="Depot name" maxLength={150} value={editing.form.name}
              onChange={(event) => setField('name', event.target.value)} />
            <NumberInput id="depot-latitude" label="Latitude" min={-90} max={90} step={0.0001} allowEmpty
              value={editing.form.latitude} onChange={(_, state) => setField('latitude', Number(state.value))} />
            <NumberInput id="depot-longitude" label="Longitude" min={-180} max={180} step={0.0001} allowEmpty
              value={editing.form.longitude} onChange={(_, state) => setField('longitude', Number(state.value))} />
            <Select id="depot-manager" labelText="Manager" value={editing.form.managerId ?? ''}
              onChange={(event) => setField('managerId', event.target.value ? Number(event.target.value) : null)}>
              <SelectItem value="" text="Not assigned" />
              {managers.map((manager) => (
                <SelectItem key={manager.id} value={manager.id} text={`${manager.name}${manager.email ? ` (${manager.email})` : ''}`} />
              ))}
            </Select>
          </div>
        )}
      </Modal>
    </section>
  )
}
