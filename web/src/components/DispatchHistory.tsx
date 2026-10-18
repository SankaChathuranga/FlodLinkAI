import { useEffect, useRef, useState } from 'react'
import {
  Button,
  InlineLoading,
  InlineNotification,
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
  Tag,
  TextInput,
  Tile,
} from '@carbon/react'
import { useAppContext } from '../context/AppContext'
import { stateTagType } from '../api/workflows'
import { getAuditTrail, listDispatches } from '../api/client'
import { describeApiError } from '../lib/apiError'
import type { AuditEvent, DispatchListResponse } from '../api/types'

/**
 * Dispatch History – Member D (Ijini).
 * Paginated, filterable table of all coordinator dispatch decisions,
 * with expandable per-dispatch audit timeline.
 */

const DECISIONS = ['Approved', 'Rejected', 'RevisionRequested']

interface Filters { status: string; dateFrom: string; dateTo: string }

function DispatchHistory() {
  const { apiBaseUrl } = useAppContext()

  const [page, setPage] = useState(1)
  const [filters, setFilters] = useState<Filters>({ status: '', dateFrom: '', dateTo: '' })
  const [draft, setDraft] = useState<Filters>({ status: '', dateFrom: '', dateTo: '' })
  const [data, setData] = useState<DispatchListResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [expandedId, setExpandedId] = useState<string | null>(null)
  const [audits, setAudits] = useState<Record<string, AuditEvent[]>>({})
  const mountedRef = useRef(true)

  useEffect(() => {
    mountedRef.current = true
    setLoading(true)
    listDispatches(apiBaseUrl, { ...filters, page })
      .then((res: DispatchListResponse) => {
        if (!mountedRef.current) return
        setData(res)
        setError(null)
      })
      .catch((err: unknown) => {
        if (!mountedRef.current) return
        setError(describeApiError(err))
      })
      .finally(() => {
        if (mountedRef.current) setLoading(false)
      })
    return () => { mountedRef.current = false }
  }, [apiBaseUrl, filters, page])

  const applyFilters = () => { setFilters(draft); setPage(1) }
  const resetFilters = () => {
    const empty: Filters = { status: '', dateFrom: '', dateTo: '' }
    setDraft(empty); setFilters(empty); setPage(1)
  }

  const toggleAudit = async (dispatchId: string): Promise<void> => {
    if (expandedId === dispatchId) { setExpandedId(null); return }
    setExpandedId(dispatchId)
    if (!audits[dispatchId]) {
      try {
        const result = await getAuditTrail(apiBaseUrl, dispatchId)
        if (!mountedRef.current) return
        setAudits((prev) => ({ ...prev, [dispatchId]: result.events }))
      } catch { /* non-fatal */ }
    }
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1

  return (
    <section aria-labelledby="dispatches-heading">
      <Stack gap={5}>
        <div>
          <h1 id="dispatches-heading" className="cds--heading-04">Dispatch History</h1>
          <p className="cds--body-compact-01 fl-text-muted">All coordinator decisions with full audit trail.</p>
        </div>

        {error && <InlineNotification kind="error" role="alert" title={error} lowContrast hideCloseButton />}

        <Tile>
          <form className="fl-filter-bar" aria-label="Filter decisions" onSubmit={(e) => { e.preventDefault(); applyFilters() }}>
            <Select id="dispatch-decision" labelText="Decision" value={draft.status} onChange={(e) => setDraft({ ...draft, status: e.target.value })}>
              <SelectItem value="" text="All" />
              {DECISIONS.map((d) => <SelectItem key={d} value={d} text={d} />)}
            </Select>
            <TextInput id="dispatch-from" type="date" labelText="From" value={draft.dateFrom}
              onChange={(e) => setDraft({ ...draft, dateFrom: e.target.value })} />
            <TextInput id="dispatch-to" type="date" labelText="To" value={draft.dateTo}
              onChange={(e) => setDraft({ ...draft, dateTo: e.target.value })} />
            <div className="fl-toolbar-inline">
              <Button type="submit" size="md">Apply</Button>
              <Button type="button" kind="secondary" size="md" onClick={resetFilters}>Reset</Button>
            </div>
          </form>
        </Tile>

        {loading ? (
          <InlineLoading description="Loading history…" />
        ) : !data || data.items.length === 0 ? (
          <Tile className="fl-empty"><p className="cds--body-compact-01">No dispatch decisions match the current filters.</p></Tile>
        ) : (
          <>
            <TableContainer title="Decisions">
              <Table aria-label="Dispatch decisions">
                <TableHead>
                  <TableRow>
                    <TableHeader>Decision</TableHeader>
                    <TableHeader>Workflow run</TableHeader>
                    <TableHeader>Notes / reason</TableHeader>
                    <TableHeader>Dispatched at</TableHeader>
                    <TableHeader>Created at</TableHeader>
                    <TableHeader><span className="sr-only">Audit</span></TableHeader>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {data.items.map((item) => (
                    <AuditRow
                      key={item.id}
                      dispatchId={item.id}
                      decision={item.decision}
                      workflowRunId={item.workflowRunId}
                      notes={item.approvalNotes}
                      dispatchedAt={item.dispatchedAt}
                      createdAt={item.createdAt}
                      expanded={expandedId === item.id}
                      events={audits[item.id]}
                      onToggle={() => void toggleAudit(item.id)}
                    />
                  ))}
                </TableBody>
              </Table>
            </TableContainer>

            <nav aria-label="Dispatch pages" className="fl-pager">
              <span className="cds--body-compact-01">Page {data.page} of {totalPages} · {data.total} total</span>
              <div>
                <Button kind="secondary" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
                <Button kind="secondary" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Next</Button>
              </div>
            </nav>
          </>
        )}
      </Stack>
    </section>
  )
}

function AuditRow({
  dispatchId, decision, workflowRunId, notes, dispatchedAt, createdAt, expanded, events, onToggle,
}: {
  dispatchId: string; decision: string; workflowRunId: string; notes: string | null
  dispatchedAt: string | null; createdAt: string; expanded: boolean
  events: AuditEvent[] | undefined; onToggle: () => void
}) {
  const panelId = `audit-${dispatchId}`
  return (
    <>
      <TableRow>
        <TableCell><DecisionBadge decision={decision} /></TableCell>
        {/* Monospace for IDs per spec: reserved for IDs and coordinates */}
        <TableCell className="fl-mono fl-id-cell">{workflowRunId}</TableCell>
        <TableCell className="fl-notes-cell">{notes ?? '–'}</TableCell>
        <TableCell>{dispatchedAt ? new Date(dispatchedAt).toLocaleString() : '–'}</TableCell>
        <TableCell>{new Date(createdAt).toLocaleString()}</TableCell>
        <TableCell>
          <Button kind="ghost" size="sm" onClick={onToggle} aria-expanded={expanded} aria-controls={panelId}>
            {expanded ? 'Hide audit' : 'Audit'}
          </Button>
        </TableCell>
      </TableRow>
      {expanded && (
        <TableRow id={panelId}>
          <TableCell colSpan={6} className="fl-audit-cell">
            <AuditTimeline events={events} dispatchId={dispatchId} />
          </TableCell>
        </TableRow>
      )}
    </>
  )
}

function AuditTimeline({ events, dispatchId }: { events: AuditEvent[] | undefined; dispatchId: string }) {
  if (!events) {
    return <InlineLoading description="Loading audit trail…" />
  }
  if (events.length === 0) {
    return <p className="cds--helper-text-01">No audit events recorded for dispatch {dispatchId}.</p>
  }
  return (
    <ol className="fl-timeline" aria-label={`Audit trail of dispatch ${dispatchId}`}>
      {events.map((event) => (
        <li key={event.id} className="fl-stack-sm">
          <strong>{event.eventType}</strong>
          {event.eventDetailJson && (
            /* Monospace for raw JSON values per spec */
            <pre className="fl-mono fl-json">{formatDetail(event.eventDetailJson)}</pre>
          )}
          <span className="cds--helper-text-01">
            {new Date(event.createdAt).toLocaleString()}
            {event.actorId ? ` · actor ${event.actorId}` : ''}
          </span>
        </li>
      ))}
    </ol>
  )
}

/** Decision tag – WorkflowState → colour mapping shared with the rest of the portal. */
function DecisionBadge({ decision }: { decision: string }) {
  return <Tag type={stateTagType(decision)} size="sm">{decision}</Tag>
}

function formatDetail(json: string): string {
  try { return JSON.stringify(JSON.parse(json), null, 2) }
  catch { return json }
}

export default DispatchHistory
