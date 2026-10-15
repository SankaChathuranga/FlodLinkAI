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
import { Add, Renew } from '@carbon/icons-react'
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
  const [selectedItem, setSelectedItem] = useState<InventoryItem | null>(null)
  const [quantityReceived, setQuantityReceived] = useState(1)
  const [submitting, setSubmitting] = useState(false)

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

  const openCheckIn = (item: InventoryItem) => {
    setSelectedItem(item)
    setQuantityReceived(1)
  }

  const submitCheckIn = async () => {
    if (selectedItem === null || quantityReceived <= 0) return
    setSubmitting(true)
    setError(null)
    try {
      const response = await apiFetch(`${apiBaseUrl}/api/inventory/${selectedItem.id}/check-in`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ quantityReceived }),
      })
      if (!response.ok) throw new Error(`Could not update stock (HTTP ${response.status}).`)
      setSelectedItem(null)
      await loadInventory()
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Could not update stock.')
    } finally {
      setSubmitting(false)
    }
  }

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
                              ) : cell.info.header === 'action' && item && canCheckIn ? (
                                <Button kind="ghost" size="sm" renderIcon={Add} onClick={() => openCheckIn(item)}>
                                  Check in
                                </Button>
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
        open={selectedItem !== null}
        modalHeading="Check in stock"
        primaryButtonText={submitting ? 'Saving…' : 'Add stock'}
        secondaryButtonText="Cancel"
        primaryButtonDisabled={submitting || quantityReceived <= 0}
        onRequestSubmit={() => void submitCheckIn()}
        onRequestClose={() => !submitting && setSelectedItem(null)}
      >
        <p className="cds--body-compact-01">
          Add received stock to <strong>{selectedItem?.itemName}</strong> at {selectedItem?.depot?.name ?? `Depot #${selectedItem?.depotId}`}. 
        </p>
        <div className="inventory-dashboard__field">
          <NumberInput
            id="quantity-received"
            label={`Quantity received (${selectedItem?.unit ?? 'units'})`}
            min={0.01}
            step={1}
            value={quantityReceived}
            onChange={(_, state) => setQuantityReceived(Number(state.value) || 0)}
          />
        </div>
      </Modal>
    </section>
  )
}
