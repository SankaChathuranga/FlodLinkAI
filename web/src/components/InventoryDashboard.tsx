import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  Button,
  Checkbox,
  DataTable,
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
  Tag,
  Tile,
} from '@carbon/react'
import { Add, Locked, QrCode, Renew, Unlocked } from '@carbon/icons-react'
import QRCode from 'qrcode'
import { Link } from 'react-router-dom'
import { useAppContext } from '../context/AppContext'
import { apiFetch } from '../api/http'
import { useAuth } from '../context/AuthContext'
import { useDebouncedValue } from '../lib/useDebouncedValue'
import type { InventoryItem, PaginatedResult } from '../types'

const headers = [
  { key: 'depot', header: 'Depot' },
  { key: 'itemName', header: 'Item' },
  { key: 'quantity', header: 'Free stock' },
  { key: 'reserved', header: 'Reserved' },
  { key: 'updated', header: 'Last updated' },
  { key: 'action', header: 'Action' },
]

const sortOptions = [
  { value: 'depot', label: 'Depot, then item' },
  { value: 'item', label: 'Item name' },
  { value: 'quantity_asc', label: 'Least free stock first' },
  { value: 'quantity_desc', label: 'Most free stock first' },
  { value: 'updated', label: 'Recently updated' },
]

type StockActionKind = 'check-in' | 'reserve' | 'release'

/** What each stock action sends and says (PUT /api/inventory/{id}/check-in | reserve | release). */
const stockActions: Record<StockActionKind, { heading: string; verb: string; field: string; submit: string }> = {
  'check-in': { heading: 'Check in stock', verb: 'Add received stock to', field: 'Quantity received', submit: 'Add stock' },
  reserve: { heading: 'Hold stock', verb: 'Hold stock of', field: 'Quantity to hold', submit: 'Hold stock' },
  release: { heading: 'Release held stock', verb: 'Release held stock of', field: 'Quantity to release', submit: 'Release stock' },
}

/** Text of a stock line's QR label; the app's scanner reads it (8.06). */
export const qrPayload = (item: InventoryItem) => `FLI:${item.id}`

/** Free stock: on hand minus what pending plans hold. */
const freeStock = (item: InventoryItem) => item.quantityAvailable - (item.quantityReserved ?? 0)

/** Low when free stock is at or below the reorder threshold (the same rule as the API's lowStock filter). */
export const isLowStock = (item: InventoryItem) => freeStock(item) <= (item.reorderThreshold ?? 0)

export function InventoryDashboard() {
  const { apiBaseUrl } = useAppContext()
  // Coordinators can see stock; only depot managers (and admins) can change it.
  const canCheckIn = useAuth().hasRole('DepotManager')
  const [inventory, setInventory] = useState<InventoryItem[]>([])
  const [totalItems, setTotalItems] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebouncedValue(search.trim())
  const [lowStockOnly, setLowStockOnly] = useState(false)
  const [sort, setSort] = useState('depot')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [action, setAction] = useState<{ kind: StockActionKind; item: InventoryItem } | null>(null)
  const [quantity, setQuantity] = useState(1)
  const [submitting, setSubmitting] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)
  const [label, setLabel] = useState<{ item: InventoryItem; image: string } | null>(null)

  const openLabel = async (item: InventoryItem) => {
    try {
      setLabel({ item, image: await QRCode.toDataURL(qrPayload(item), { margin: 1, width: 240 }) })
    } catch {
      setError('Could not draw the QR label.')
    }
  }

  // A new search, filter or sort starts again from the first page.
  useEffect(() => {
    setPage(1)
  }, [debouncedSearch, lowStockOnly, sort])

  // Only the latest request may update the table (a filter change and its page reset both fetch).
  const latestRequest = useRef(0)

  const loadInventory = useCallback(async () => {
    const requestId = ++latestRequest.current
    setLoading(true)
    setError(null)
    try {
      const params = new URLSearchParams({ sort, page: String(page), pageSize: String(pageSize) })
      if (debouncedSearch) params.set('search', debouncedSearch)
      if (lowStockOnly) params.set('lowStock', 'true')
      const response = await apiFetch(`${apiBaseUrl}/api/inventory?${params.toString()}`)
      if (!response.ok) throw new Error(`Could not load stock (HTTP ${response.status}).`)
      const data = await response.json() as PaginatedResult<InventoryItem>
      if (requestId !== latestRequest.current) return
      setInventory(data.items)
      setTotalItems(data.totalItems)
    } catch (requestError) {
      if (requestId !== latestRequest.current) return
      setError(requestError instanceof Error ? requestError.message : 'Could not load stock.')
    } finally {
      if (requestId === latestRequest.current) setLoading(false)
    }
  }, [apiBaseUrl, sort, page, pageSize, debouncedSearch, lowStockOnly])

  useEffect(() => {
    void loadInventory()
  }, [loadInventory])

  const rows = useMemo(() => inventory.map((item) => ({
    id: String(item.id),
    depot: item.depot?.name ?? `Depot #${item.depotId}`,
    itemName: item.itemName,
    quantity: `${freeStock(item)} ${item.unit}`,
    reserved: `${item.quantityReserved ?? 0}`,
    updated: new Date(item.updatedAt).toLocaleString(),
    action: '',
  })), [inventory])

  const openAction = (kind: StockActionKind, item: InventoryItem) => {
    setAction({ kind, item })
    setQuantity(1)
    setActionError(null)
  }

  const submitAction = async () => {
    if (action === null || quantity <= 0) return
    setSubmitting(true)
    setActionError(null)
    try {
      const body = action.kind === 'check-in' ? { quantityReceived: quantity } : { quantity }
      const response = await apiFetch(`${apiBaseUrl}/api/inventory/${action.item.id}/${action.kind}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      })
      if (!response.ok) {
        // The API explains refusals, e.g. "Only 5 Medical is free to reserve."
        const problem = (await response.json().catch(() => null)) as { message?: string } | null
        throw new Error(problem?.message ?? `Could not update stock (HTTP ${response.status}).`)
      }
      setAction(null)
      await loadInventory()
    } catch (requestError) {
      setActionError(requestError instanceof Error ? requestError.message : 'Could not update stock.')
    } finally {
      setSubmitting(false)
    }
  }

  const actionText = action ? stockActions[action.kind] : stockActions['check-in']

  return (
    <section aria-label="Inventory dashboard">
      <Tile>
        <div className="inventory-dashboard__header">
          <div>
            <h2 className="cds--heading-04">Inventory dashboard</h2>
            <p className="cds--body-compact-01">Current relief supplies across all depots.</p>
          </div>
          <Button kind="secondary" renderIcon={Renew} onClick={() => void loadInventory()} disabled={loading}>
            Refresh
          </Button>
        </div>

        {error && (
          <InlineNotification
            kind="error"
            title="Inventory request failed"
            subtitle={error}
            lowContrast
            hideCloseButton
          />
        )}

        <div className="inventory-dashboard__toolbar">
          <Search
            size="md"
            labelText="Search stock"
            placeholder="Search item or depot"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            closeButtonLabelText="Clear search"
          />
          <Select id="inventory-sort" labelText="Sort by" inline value={sort} onChange={(event) => setSort(event.target.value)}>
            {sortOptions.map((option) => <SelectItem key={option.value} value={option.value} text={option.label} />)}
          </Select>
          <Checkbox
            id="inventory-low-stock"
            labelText="Low stock only"
            checked={lowStockOnly}
            onChange={(_, { checked }) => setLowStockOnly(checked)}
          />
        </div>

        {loading ? <InlineLoading description="Loading inventory…" /> : inventory.length === 0 && !error ? (
          <p className="cds--body-compact-01 inventory-dashboard__empty">
            {lowStockOnly || debouncedSearch ? 'No stock lines match these filters.' : 'No stock recorded yet.'}
          </p>
        ) : (
          <DataTable rows={rows} headers={headers}>
            {({ rows: tableRows, headers: tableHeaders, getHeaderProps, getRowProps, getTableProps }) => (
              <TableContainer title="Depot stock levels">
                <Table {...getTableProps()}>
                  <TableHead>
                    <TableRow>
                      {tableHeaders.map((header) => (
                        <TableHeader {...getHeaderProps({ header })}>{header.header}</TableHeader>
                      ))}
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {tableRows.map((row) => {
                      const item = inventory.find((inventoryItem) => String(inventoryItem.id) === row.id)
                      return (
                        <TableRow {...getRowProps({ row })}>
                          {row.cells.map((cell) => (
                            <TableCell key={cell.id}>
                              {cell.info.header === 'quantity' && item && isLowStock(item) ? (
                                <>
                                  {cell.value}{' '}
                                  <Tag type="red" size="sm" title={`At or below the reorder threshold of ${item.reorderThreshold ?? 0}`}>
                                    Low stock
                                  </Tag>
                                </>
                              ) : cell.info.header === 'action' && item ? (
                                <div className="inventory-dashboard__actions">
                                  <Link to={`/stock-history?itemId=${item.id}`} className="cds--link"
                                    aria-label={`Stock history of ${item.itemName} at ${item.depot?.name ?? `depot ${item.depotId}`}`}>
                                    History
                                  </Link>
                                  <Button kind="ghost" size="sm" renderIcon={QrCode} onClick={() => void openLabel(item)}
                                    aria-label={`QR label for ${item.itemName} at ${item.depot?.name ?? `depot ${item.depotId}`}`}>
                                    QR label
                                  </Button>
                                  {canCheckIn && <>
                                  <Button kind="ghost" size="sm" renderIcon={Add} onClick={() => openAction('check-in', item)}
                                    aria-label={`Check in ${item.itemName} at ${item.depot?.name ?? `depot ${item.depotId}`}`}>
                                    Check in
                                  </Button>
                                  <Button kind="ghost" size="sm" renderIcon={Locked} onClick={() => openAction('reserve', item)}
                                    aria-label={`Hold ${item.itemName} at ${item.depot?.name ?? `depot ${item.depotId}`}`}>
                                    Hold
                                  </Button>
                                  {(item.quantityReserved ?? 0) > 0 && (
                                    <Button kind="ghost" size="sm" renderIcon={Unlocked} onClick={() => openAction('release', item)}
                                      aria-label={`Release ${item.itemName} at ${item.depot?.name ?? `depot ${item.depotId}`}`}>
                                      Release
                                    </Button>
                                  )}
                                  </>}
                                </div>
                              ) : cell.info.header === 'action' ? '—' : cell.value}
                            </TableCell>
                          ))}
                        </TableRow>
                      )
                    })}
                  </TableBody>
                </Table>
              </TableContainer>
            )}
          </DataTable>
        )}

        {totalItems > 0 && (
          <Pagination
            page={page}
            pageSize={pageSize}
            pageSizes={[10, 20, 50, 100]}
            totalItems={totalItems}
            onChange={({ page: nextPage, pageSize: nextSize }) => {
              setPage(nextPage)
              setPageSize(nextSize)
            }}
          />
        )}
      </Tile>

      <Modal
        open={label !== null}
        passiveModal
        modalHeading="Stock label"
        onRequestClose={() => setLabel(null)}
      >
        {label && (
          <div className="stock-label">
            <img src={label.image} alt={`QR code ${qrPayload(label.item)}`} width={240} height={240} />
            <p className="cds--body-compact-01">
              <strong>{label.item.itemName}</strong> · {label.item.depot?.name ?? `Depot #${label.item.depotId}`}<br />
              <code>{qrPayload(label.item)}</code>
            </p>
            <p className="cds--helper-text-01">Print and stick on the shelf. Scanning it in the FloodLink app opens check-in for this line.</p>
            <a className="cds--link" href={label.image} download={`floodlink-${label.item.itemName.toLowerCase().replace(/[^a-z0-9]+/g, '-')}-${label.item.id}.png`}>
              Download label
            </a>
          </div>
        )}
      </Modal>

      <Modal
        open={action !== null}
        modalHeading={actionText.heading}
        primaryButtonText={submitting ? 'Saving…' : actionText.submit}
        secondaryButtonText="Cancel"
        primaryButtonDisabled={submitting || quantity <= 0}
        onRequestSubmit={() => void submitAction()}
        onRequestClose={() => !submitting && setAction(null)}
      >
        <p className="cds--body-compact-01">
          {actionText.verb} <strong>{action?.item.itemName}</strong> at {action?.item.depot?.name ?? `Depot #${action?.item.depotId}`}.
          {action && action.kind !== 'check-in' && (
            <> Free: {freeStock(action.item)} {action.item.unit}, held: {action.item.quantityReserved ?? 0}.</>
          )}
        </p>
        {actionError && (
          <InlineNotification kind="error" title="Stock not changed" subtitle={actionError} lowContrast hideCloseButton />
        )}
        <div className="inventory-dashboard__field">
          <NumberInput
            id="stock-action-quantity"
            label={`${actionText.field} (${action?.item.unit ?? 'units'})`}
            min={0.01}
            step={1}
            value={quantity}
            onChange={(_, state) => setQuantity(Number(state.value) || 0)}
          />
        </div>
      </Modal>
    </section>
  )
}
