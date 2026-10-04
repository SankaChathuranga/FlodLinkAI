import { useEffect, useState } from 'react'
import { apiFetch } from '../utils/api'

export default function InventoryDashboard() {
    const [inventory, setInventory] = useState([])
    const [depots, setDepots] = useState([])
    const [selectedDepotId, setSelectedDepotId] = useState('')
    const [lowStockOnly, setLowStockOnly] = useState(false)
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState('')
    const [depotError, setDepotError] = useState('')

    useEffect(() => {
        let active = true
        apiFetch('/api/depots?page=1')
            .then((response) => response.json())
            .then((payload) => {
                if (active) setDepots(payload.data ?? payload)
            })
            .catch((requestError) => {
                if (active) setDepotError(requestError.message)
            })
        return () => { active = false }
    }, []);

    useEffect(() => {
        let active = true
        const query = new URLSearchParams()
        if (selectedDepotId) query.set('depotId', selectedDepotId)
        if (lowStockOnly) query.set('lowStock', 'true')

        setLoading(true)
        setError('')
        apiFetch(`/api/inventory?${query.toString()}`)
            .then((response) => response.json())
            .then((data) => {
                if (active) setInventory(Array.isArray(data) ? data : data.data ?? [])
            })
            .catch((requestError) => {
                if (active) setError(requestError.message)
            })
            .finally(() => {
                if (active) setLoading(false)
            })

        return () => { active = false }
    }, [selectedDepotId, lowStockOnly])

    return (
        <section className="floodlink-card">
            <div className="page-heading">
                <div>
                    <p className="eyebrow">Operations / Stock control</p>
                    <h2 className="floodlink-header">Inventory Dashboard</h2>
                    <p className="page-subtitle">Monitor availability before the next relief dispatch.</p>
                </div>
                <span className="data-count">{inventory.length} item{inventory.length === 1 ? '' : 's'}</span>
            </div>

            <div className="floodlink-controls inventory-controls">
                <label className="field-label" htmlFor="inventory-depot">Depot</label>
                <select
                    id="inventory-depot"
                    className="floodlink-select"
                    value={selectedDepotId}
                    onChange={(event) => setSelectedDepotId(event.target.value)}
                >
                    <option value="">All depots</option>
                    {depots.map((depot) => <option key={depot.id} value={depot.id}>{depot.name}</option>)}
                </select>

                <label className="floodlink-toggle-label">
                    <input
                        type="checkbox"
                        checked={lowStockOnly}
                        onChange={(event) => setLowStockOnly(event.target.checked)}
                    />
                    <span>Low stock only</span>
                </label>
                {depotError && <span className="inline-warning">Depot filter unavailable: {depotError}</span>}
            </div>

            {loading && <div className="loading-spinner">Syncing inventory data...</div>}
            {!loading && error && <div className="state-message state-error">{error}</div>}
            {!loading && !error && inventory.length === 0 && (
                <div className="state-message">No inventory items match these filters.</div>
            )}
            {!loading && !error && inventory.length > 0 && (
                <div className="floodlink-table-wrapper">
                    <table className="floodlink-table">
                        <thead>
                            <tr><th>Item</th><th>Depot</th><th>Available</th><th>Reserved</th><th>Threshold</th><th>Status</th></tr>
                        </thead>
                        <tbody>
                            {inventory.map((item) => {
                                const isLowStock = item.quantityAvailable <= item.reorderThreshold
                                return (
                                    <tr key={item.id} className={isLowStock ? 'row-low-stock' : ''}>
                                        <td className="cell-strong">{item.itemName}</td>
                                        <td>{depots.find((depot) => depot.id === item.depotId)?.name ?? `Depot ${item.depotId}`}</td>
                                        <td>{item.quantityAvailable} {item.unit}</td>
                                        <td>{item.quantityReserved} {item.unit}</td>
                                        <td>{item.reorderThreshold} {item.unit}</td>
                                        <td><span className={`status-badge ${isLowStock ? 'badge-danger' : 'badge-success'}`}>{isLowStock ? 'Low stock' : 'Healthy'}</span></td>
                                    </tr>
                                )
                            })}
                        </tbody>
                    </table>
                </div>
            )}
        </section>
    )
}
