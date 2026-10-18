import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import {
  Button,
  InlineLoading,
  InlineNotification,
  Pagination,
  Select,
  SelectItem,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableHeader,
  TableRow,
  Tag,
  Tile,
} from '@carbon/react'
import { Renew } from '@carbon/icons-react'
import { useAppContext } from '../context/AppContext'
import { useAuth } from '../context/AuthContext'
import { describeApiError } from '../lib/apiError'
import { listDepots, listMovements, type StockMovement } from '../api/stock'
import type { Depot } from '../types'

/** How each kind of movement reads in the table, and which way it moves stock. */
export const KINDS: Record<string, { label: string; tag: 'green' | 'red' | 'blue' | 'gray' | 'purple' | 'teal'; sign: '+' | '−' | '' }> = {
  Opening: { label: 'Opening stock', tag: 'teal', sign: '+' },
  CheckIn: { label: 'Checked in', tag: 'green', sign: '+' },
  ManualHold: { label: 'Held by staff', tag: 'blue', sign: '' },
  ManualRelease: { label: 'Released by staff', tag: 'gray', sign: '' },
  PlanReserve: { label: 'Reserved for plan', tag: 'blue', sign: '' },
  PlanCommit: { label: 'Dispatched', tag: 'red', sign: '−' },
  PlanRelease: { label: 'Released by plan', tag: 'gray', sign: '' },
  PhysicalCount: { label: 'Shelf count', tag: 'purple', sign: '' },
}

/**
 * Stock history (8.03, Member B): every change to depot stock, newest first — check-ins, staff holds and
 * releases, plan reservations, dispatches and releases, and shelf counts — with the stock left afterwards
 * and who or which workflow caused it. Opened for one line from Inventory (?itemId=).
 */
export function StockHistoryPage() {
  const { apiBaseUrl } = useAppContext()
  const canOpenTrace = useAuth().hasRole('Coordinator')
  const [params, setParams] = useSearchParams()
  const itemId = Number(params.get('itemId')) || undefined

  const [movements, setMovements] = useState<StockMovement[]>([])
  const [totalItems, setTotalItems] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [depotId, setDepotId] = useState('')
  const [kind, setKind] = useState('')
  const [depots, setDepots] = useState<Depot[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => setPage(1), [depotId, kind, itemId])

  useEffect(() => {
    listDepots(apiBaseUrl, { pageSize: 100 }).then((d) => setDepots(d.items)).catch(() => setDepots([]))
  }, [apiBaseUrl])

  const latestRequest = useRef(0)
  const load = useCallback(async () => {
    const requestId = ++latestRequest.current
    setLoading(true)
    setError(null)
    try {
      const data = await listMovements(apiBaseUrl, {
        inventoryItemId: itemId, depotId: depotId ? Number(depotId) : undefined, kind: kind || undefined, page, pageSize,
      })
      if (requestId !== latestRequest.current) return
      setMovements(data.items)
      setTotalItems(data.totalItems)
    } catch (err) {
      if (requestId === latestRequest.current) setError(describeApiError(err))
    } finally {
      if (requestId === latestRequest.current) setLoading(false)
    }
  }, [apiBaseUrl, itemId, depotId, kind, page, pageSize])

  useEffect(() => {
    void load()
  }, [load])

  const line = itemId && movements[0] ? `${movements[0].itemName} at ${movements[0].depotName ?? `depot ${movements[0].depotId}`}` : null

  return (
    <section aria-label="Stock history">
      <Tile>
        <div className="inventory-dashboard__header">
          <div>
            <h2 className="cds--heading-04">Stock history</h2>
            <p className="cds--body-compact-01">Every change to depot stock, with the stock left afterwards.</p>
          </div>
          <Button kind="secondary" renderIcon={Renew} onClick={() => void load()} disabled={loading}>Refresh</Button>
        </div>

        {error && <InlineNotification kind="error" title="Stock history could not be loaded" subtitle={error} lowContrast hideCloseButton />}

        <div className="inventory-dashboard__toolbar">
          {itemId ? (
            <p className="cds--body-compact-01">
              Showing one stock line{line ? `: ${line}` : ''}.{' '}
              <button type="button" className="cds--link" onClick={() => setParams({})}>Show all stock</button>
            </p>
          ) : (
            <Select id="history-depot" labelText="Depot" inline value={depotId} onChange={(e) => setDepotId(e.target.value)}>
              <SelectItem value="" text="All depots" />
              {depots.map((d) => <SelectItem key={d.id} value={String(d.id)} text={d.name} />)}
            </Select>
          )}
          <Select id="history-kind" labelText="Change" inline value={kind} onChange={(e) => setKind(e.target.value)}>
            <SelectItem value="" text="All changes" />
            {Object.entries(KINDS).map(([k, v]) => <SelectItem key={k} value={k} text={v.label} />)}
          </Select>
        </div>

        {loading ? <InlineLoading description="Loading stock history…" /> : movements.length === 0 && !error ? (
          <p className="cds--body-compact-01 inventory-dashboard__empty">No stock changes match these filters.</p>
        ) : (
          <TableContainer title="Stock changes">
            <Table aria-label="Stock changes">
              <TableHead>
                <TableRow>
                  <TableHeader>When</TableHeader>
                  <TableHeader>Stock line</TableHeader>
                  <TableHeader>Change</TableHeader>
                  <TableHeader>Quantity</TableHeader>
                  <TableHeader>On hand after</TableHeader>
                  <TableHeader>Held after</TableHeader>
                  <TableHeader>By</TableHeader>
                  <TableHeader>Note</TableHeader>
                </TableRow>
              </TableHead>
              <TableBody>
                {movements.map((m) => {
                  const k = KINDS[m.kind] ?? { label: m.kind, tag: 'gray' as const, sign: '' as const }
                  return (
                    <TableRow key={m.id}>
                      <TableCell>{new Date(m.createdAt).toLocaleString()}</TableCell>
                      <TableCell>{m.itemName} · {m.depotName ?? `Depot #${m.depotId}`}</TableCell>
                      <TableCell><Tag type={k.tag} size="sm">{k.label}</Tag></TableCell>
                      <TableCell>{k.sign}{m.quantity} {m.unit}</TableCell>
                      <TableCell>{m.availableAfter}</TableCell>
                      <TableCell>{m.reservedAfter}</TableCell>
                      <TableCell>
                        {m.userName ?? (m.workflowRunId ? 'System (validated plan)' : '—')}
                        {m.workflowRunId && canOpenTrace && (
                          <> · <Link to={`/workflows/${m.workflowRunId}`} className="cds--link">workflow</Link></>
                        )}
                      </TableCell>
                      <TableCell>{m.note ?? ''}</TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          </TableContainer>
        )}

        {totalItems > 0 && (
          <Pagination page={page} pageSize={pageSize} pageSizes={[20, 50, 100]} totalItems={totalItems}
            onChange={({ page: nextPage, pageSize: nextSize }) => { setPage(nextPage); setPageSize(nextSize) }} />
        )}
      </Tile>
    </section>
  )
}
