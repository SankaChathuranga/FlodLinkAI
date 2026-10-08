import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  Button,
  DataTable,
  InlineLoading,
  InlineNotification,
  Modal,
  NumberInput,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableHeader,
  TableRow,
  Tile,
} from '@carbon/react'
import { Add, Renew } from '@carbon/icons-react'
import { useAppContext } from '../context/AppContext'
import type { InventoryItem } from '../types'

const headers = [
  { key: 'depot', header: 'Depot' },
  { key: 'itemName', header: 'Item' },
  { key: 'quantity', header: 'Available stock' },
  { key: 'updated', header: 'Last updated' },
  { key: 'action', header: 'Action' },
]

export function InventoryDashboard() {
  const { apiBaseUrl } = useAppContext()
  const [inventory, setInventory] = useState<InventoryItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selectedItem, setSelectedItem] = useState<InventoryItem | null>(null)
  const [quantityReceived, setQuantityReceived] = useState(1)
  const [submitting, setSubmitting] = useState(false)

  const loadInventory = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const response = await fetch(`${apiBaseUrl}/api/inventory`)
      if (!response.ok) throw new Error(`Could not load stock (HTTP ${response.status}).`)
      setInventory(await response.json() as InventoryItem[])
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Could not load stock.')
    } finally {
      setLoading(false)
    }
  }, [apiBaseUrl])

  useEffect(() => {
    void loadInventory()
  }, [loadInventory])

  const rows = useMemo(() => inventory.map((item) => ({
    id: String(item.id),
    depot: item.depot?.name ?? `Depot #${item.depotId}`,
    itemName: item.itemName,
    quantity: `${item.quantityAvailable} ${item.unit}`,
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
      const response = await fetch(`${apiBaseUrl}/api/inventory/${selectedItem.id}/check-in`, {
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

        {loading ? <InlineLoading description="Loading inventory…" /> : (
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
                              {cell.info.header === 'action' && item ? (
                                <Button kind="ghost" size="sm" renderIcon={Add} onClick={() => openCheckIn(item)}>
                                  Check in
                                </Button>
                              ) : cell.value}
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
