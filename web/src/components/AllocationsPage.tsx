import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
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
import { listAllocations, type Allocation } from '../api/stock'

/** Where an allocation line is in the stock lifecycle (Proposed → Reserved → Committed, or Released). */
const STATUS: Record<string, { tag: 'gray' | 'blue' | 'green' | 'warm-gray'; meaning: string }> = {
  Proposed: { tag: 'gray', meaning: 'Matched, not yet validated' },
  Reserved: { tag: 'blue', meaning: 'Held while awaiting approval' },
  Committed: { tag: 'green', meaning: 'Approved — taken from stock' },
  Released: { tag: 'warm-gray', meaning: 'Rejected or revised — stock returned' },
}

/**
 * Allocation proposals review (8.02, Member B): every line the Matching agent proposed — which
 * depot sends how much of what to which shelter — and its stock status. Coordinators can open the
 * workflow trace behind each line.
 */
export function AllocationsPage() {
  const { apiBaseUrl } = useAppContext()
  const canOpenTrace = useAuth().hasRole('Coordinator')

  const [items, setItems] = useState<Allocation[]>([])
  const [totalItems, setTotalItems] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => setPage(1), [status])

  const latestRequest = useRef(0)
  const load = useCallback(async () => {
    const requestId = ++latestRequest.current
    setLoading(true)
    setError(null)
    try {
      const data = await listAllocations(apiBaseUrl, { status: status || undefined, page, pageSize })
      if (requestId !== latestRequest.current) return
      setItems(data.items)
      setTotalItems(data.totalItems)
    } catch (err) {
      if (requestId === latestRequest.current) setError(describeApiError(err))
    } finally {
      if (requestId === latestRequest.current) setLoading(false)
    }
  }, [apiBaseUrl, status, page, pageSize])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <section aria-label="Allocation proposals">
      <Tile>
        <div className="inventory-dashboard__header">
          <div>
            <h2 className="cds--heading-04">Allocation proposals</h2>
            <p className="cds--body-compact-01">What the Matching agent proposed to send, and where each line is in the stock lifecycle.</p>
          </div>
          <Button kind="secondary" renderIcon={Renew} onClick={() => void load()} disabled={loading}>Refresh</Button>
        </div>

        {error && <InlineNotification kind="error" title="Allocations could not be loaded" subtitle={error} lowContrast hideCloseButton />}

        <div className="inventory-dashboard__toolbar">
          <Select id="allocation-status" labelText="Status" inline value={status} onChange={(event) => setStatus(event.target.value)}>
            <SelectItem value="" text="All statuses" />
            {Object.keys(STATUS).map((s) => <SelectItem key={s} value={s} text={s} />)}
          </Select>
        </div>

        {loading ? <InlineLoading description="Loading allocations…" /> : items.length === 0 && !error ? (
          <p className="cds--body-compact-01 inventory-dashboard__empty">
            {status ? `No ${status.toLowerCase()} allocations.` : 'No allocations yet. Start a workflow from the Reports page.'}
          </p>
        ) : (
          <TableContainer title="Allocation lines">
            <Table aria-label="Allocations">
              <TableHead>
                <TableRow>
                  <TableHeader>From depot</TableHeader>
                  <TableHeader>To shelter</TableHeader>
                  <TableHeader>Item</TableHeader>
                  <TableHeader>Quantity</TableHeader>
                  <TableHeader>Status</TableHeader>
                  <TableHeader>Workflow</TableHeader>
                  <TableHeader>Proposed</TableHeader>
                </TableRow>
              </TableHead>
              <TableBody>
                {items.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell>{a.depotName ?? `Depot #${a.depotId}`}</TableCell>
                    <TableCell>{a.shelterName ?? `Shelter #${a.shelterId}`}</TableCell>
                    <TableCell>{a.itemName}</TableCell>
                    <TableCell>{a.quantity}</TableCell>
                    <TableCell>
                      <Tag type={STATUS[a.status]?.tag ?? 'gray'} size="sm" title={STATUS[a.status]?.meaning}>{a.status}</Tag>
                    </TableCell>
                    <TableCell>
                      {canOpenTrace
                        ? <Link to={`/workflows/${a.workflowRunId}`} className="cds--link">Open trace</Link>
                        : <code title={a.workflowRunId}>{a.workflowRunId.slice(0, 8)}</code>}
                    </TableCell>
                    <TableCell>{new Date(a.createdAt).toLocaleString()}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}

        {totalItems > 0 && (
          <Pagination page={page} pageSize={pageSize} pageSizes={[10, 20, 50, 100]} totalItems={totalItems}
            onChange={({ page: nextPage, pageSize: nextSize }) => { setPage(nextPage); setPageSize(nextSize) }} />
        )}
      </Tile>
    </section>
  )
}
