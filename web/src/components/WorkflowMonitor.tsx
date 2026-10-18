import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  Button,
  ContentSwitcher,
  InlineLoading,
  InlineNotification,
  ProgressBar,
  Search,
  Select,
  SelectItem,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableHeader,
  TableRow,
  Tag,
  Tile,
  Toggle,
} from '@carbon/react'
import { Renew } from '@carbon/icons-react'
import { useAppContext } from '../context/AppContext'
import {
  WORKFLOW_STATES,
  listWorkflows,
  retryWorkflow,
  stateTagType,
  stateLabel,
  type Page,
  type WorkflowRunSummary,
} from '../api/workflows'
import { describeApiError } from '../lib/apiError'

const REFRESH_MS = 5000
type View = 'all' | 'failed'

/**
 * Workflow monitor (4.01) and failed-runs view (4.03): every run with its stage and progress,
 * filtered by state or objective, paged, and refreshed every 5 s so stage changes show up live.
 * Failed runs show why they stopped and a Retry button (within the run's retry limit).
 */
export function WorkflowMonitor() {
  const { apiBaseUrl } = useAppContext()
  const [view, setView] = useState<View>('all')
  const [stateFilter, setStateFilter] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [autoRefresh, setAutoRefresh] = useState(true)

  const [data, setData] = useState<Page<WorkflowRunSummary> | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)
  const [retrying, setRetrying] = useState<string | null>(null)
  const [notice, setNotice] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null)
  const inFlight = useRef<AbortController | null>(null)

  const effectiveState = view === 'failed' ? 'Failed' : stateFilter

  const load = useCallback(
    async (quiet = false) => {
      inFlight.current?.abort()
      const controller = new AbortController()
      inFlight.current = controller
      if (!quiet) setLoading(true)
      try {
        const result = await listWorkflows(apiBaseUrl, { state: effectiveState, search, page }, controller.signal)
        setData(result)
        setError(null)
        setLastUpdated(new Date())
      } catch (err) {
        if (controller.signal.aborted) return
        setError(describeApiError(err))
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    },
    [apiBaseUrl, effectiveState, search, page],
  )

  useEffect(() => {
    void load()
    return () => inFlight.current?.abort()
  }, [load])

  // Live updates: poll quietly while auto-refresh is on and the tab is visible.
  useEffect(() => {
    if (!autoRefresh) return
    const timer = setInterval(() => {
      if (document.visibilityState === 'visible') void load(true)
    }, REFRESH_MS)
    return () => clearInterval(timer)
  }, [autoRefresh, load])

  // Debounce the search box.
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearch(searchInput.trim())
      setPage(1)
    }, 300)
    return () => clearTimeout(timer)
  }, [searchInput])

  const retry = async (run: WorkflowRunSummary) => {
    setRetrying(run.id)
    setNotice(null)
    try {
      const result = await retryWorkflow(apiBaseUrl, run.id)
      setNotice({ kind: 'ok', text: `Retrying "${run.objective}" from ${result.state} (attempt ${result.retryCount} of ${run.retryLimit}).` })
      void load(true)
    } catch (err) {
      setNotice({ kind: 'error', text: describeApiError(err) })
    } finally {
      setRetrying(null)
    }
  }

  const switchView = (next: View) => {
    setView(next)
    setPage(1)
  }

  return (
    <section aria-labelledby="workflows-heading">
      <Stack gap={5}>
        <Tile>
          <div className="inventory-dashboard__header">
            <div>
              <h1 id="workflows-heading" className="cds--heading-04">Workflow runs</h1>
              <p className="cds--body-compact-01 fl-text-muted">
                Start a run from the Reports page. Click a run to see its full execution trace.
              </p>
            </div>
            <div className="fl-toolbar-inline">
              <Toggle id="workflows-auto-refresh" size="sm" labelText="Auto-refresh" hideLabel={false}
                labelA="Off" labelB="On" toggled={autoRefresh} onToggle={(on) => setAutoRefresh(on)} />
              <span className="cds--helper-text-01" aria-live="polite">
                {lastUpdated ? `Updated ${lastUpdated.toLocaleTimeString()}` : ''}
              </span>
              <Button kind="secondary" size="md" renderIcon={Renew} onClick={() => void load()} disabled={loading}>Refresh</Button>
            </div>
          </div>

          <ContentSwitcher aria-label="Run views" selectedIndex={view === 'all' ? 0 : 1} size="md"
            onChange={({ index }) => switchView(index === 0 ? 'all' : 'failed')}>
            <Switch name="all" text="All runs" />
            <Switch name="failed" text="Failed runs" />
          </ContentSwitcher>

          <div className="fl-filter-bar fl-filter-bar--spaced">
            <Search size="md" labelText="Search objectives" placeholder="Search objectives…" value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)} closeButtonLabelText="Clear search" />
            {view === 'all' && (
              <Select id="workflows-stage" labelText="Filter by stage" value={stateFilter}
                onChange={(e) => { setStateFilter(e.target.value); setPage(1) }}>
                <SelectItem value="" text="All stages" />
                {WORKFLOW_STATES.map((st) => <SelectItem key={st} value={st} text={stateLabel(st)} />)}
              </Select>
            )}
          </div>
        </Tile>

        {notice && (
          <InlineNotification kind={notice.kind === 'error' ? 'error' : 'success'} role={notice.kind === 'error' ? 'alert' : 'status'}
            title={notice.text} lowContrast onCloseButtonClick={() => setNotice(null)} />
        )}

        {loading && !data ? (
          <InlineLoading description="Loading workflow runs…" />
        ) : error && !data ? (
          <div role="alert" className="fl-error-block">
            <InlineNotification kind="error" title="Workflow runs could not be loaded" subtitle={error} lowContrast hideCloseButton />
            <Button kind="tertiary" size="sm" onClick={() => void load()}>Try again</Button>
          </div>
        ) : !data || data.items.length === 0 ? (
          <Tile className="fl-empty">
            <p className="cds--body-compact-01">
              {view === 'failed' ? 'No failed runs.' : search || stateFilter ? 'No runs match these filters.' : 'No workflow runs yet.'}
            </p>
          </Tile>
        ) : (
          <TableContainer title={view === 'failed' ? 'Failed runs' : 'All runs'}>
            <Table aria-label="Workflow runs">
              <TableHead>
                <TableRow>
                  <TableHeader>Objective</TableHeader>
                  <TableHeader>Stage</TableHeader>
                  <TableHeader>Progress</TableHeader>
                  <TableHeader>Reports</TableHeader>
                  <TableHeader>Retries</TableHeader>
                  <TableHeader>Updated</TableHeader>
                  {view === 'failed' && <TableHeader>Action</TableHeader>}
                </TableRow>
              </TableHead>
              <TableBody>
                {data.items.map((run) => {
                  const canRetry = run.currentState === 'Failed' && run.retryCount < run.retryLimit
                  return (
                    <TableRow key={run.id}>
                      <TableCell>
                        <Link to={`/workflows/${run.id}`} className="cds--link">{run.objective}</Link>
                        <span className="fl-mono fl-text-muted fl-truncate">{run.id.slice(0, 8)}</span>
                      </TableCell>
                      <TableCell>
                        <Tag type={stateTagType(run.currentState)} size="sm">{stateLabel(run.currentState)}</Tag>
                        {run.failureReason && (
                          <span className="fl-failure">
                            {run.failedAtState && <strong>{run.failedAtState}: </strong>}
                            {run.failureReason}
                          </span>
                        )}
                      </TableCell>
                      <TableCell className="fl-progress-cell">
                        <ProgressBar
                          label={`Progress of ${run.objective}`}
                          hideLabel
                          value={run.progressPercent}
                          max={100}
                          size="small"
                          status={run.currentState === 'Failed' || run.currentState === 'Rejected' ? 'error' : 'active'}
                          helperText={`${run.progressPercent}%`}
                        />
                      </TableCell>
                      <TableCell>{run.reportCount}</TableCell>
                      <TableCell>{run.retryCount} / {run.retryLimit}</TableCell>
                      <TableCell>{new Date(run.updatedAt).toLocaleString()}</TableCell>
                      {view === 'failed' && (
                        <TableCell>
                          <Button size="sm" onClick={() => void retry(run)} disabled={!canRetry || retrying === run.id}
                            title={canRetry ? `Re-run the ${run.failedAtState} stage` : 'Retry limit reached'}>
                            {retrying === run.id ? 'Retrying…' : canRetry ? 'Retry' : 'Limit reached'}
                          </Button>
                        </TableCell>
                      )}
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          </TableContainer>
        )}

        {error && data && (
          <InlineNotification kind="warning" role="alert" title="Live refresh failed" subtitle={error} lowContrast hideCloseButton />
        )}

        {data && data.totalPages > 1 && (
          <nav aria-label="Pagination" className="fl-pager">
            <Button kind="secondary" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
            <span className="cds--body-compact-01">Page {data.page} of {data.totalPages} ({data.totalItems} runs)</span>
            <Button kind="secondary" size="sm" disabled={page >= data.totalPages} onClick={() => setPage(page + 1)}>Next</Button>
          </nav>
        )}
      </Stack>
    </section>
  )
}
