import { useEffect, useState } from 'react'
import { apiFetch } from '../utils/api'

export default function DepotManagement() {
    const initialFormState = { id: null, name: '', latitude: '', longitude: '', managerId: '' }
    const [depots, setDepots] = useState([])
    const [loading, setLoading] = useState(true)
    const [isSubmitting, setIsSubmitting] = useState(false)
    const [error, setError] = useState('')
    const [formError, setFormError] = useState('')
    const [formData, setFormData] = useState(initialFormState)

    useEffect(() => {
        loadDepots()
    }, [])

    const loadDepots = async () => {
        setLoading(true)
        setError('')
        try {
            const response = await apiFetch('/api/depots?page=1')
            const payload = await response.json()
            setDepots(payload.data ?? payload)
        } catch (requestError) {
            setError(requestError.message)
        } finally {
            setLoading(false)
        }
    }

    const handleInputChange = (event) => {
        const { name, value } = event.target
        setFormData((previous) => ({ ...previous, [name]: value }))
    }

    const handleEdit = (depot) => {
        setFormError('')
        setFormData({
            id: depot.id,
            name: depot.name,
            latitude: depot.latitude,
            longitude: depot.longitude,
            managerId: depot.managerId ?? '',
        })
    }

    const handleSubmit = async (event) => {
        event.preventDefault()
        setIsSubmitting(true)
        setFormError('')
        try {
            const isEditing = Boolean(formData.id)
            const payload = {
                name: formData.name,
                latitude: Number(formData.latitude),
                longitude: Number(formData.longitude),
                managerId: formData.managerId === '' ? null : Number(formData.managerId),
            }
            await apiFetch(isEditing ? `/api/depots/${formData.id}` : '/api/depots', {
                method: isEditing ? 'PUT' : 'POST',
                body: JSON.stringify(payload),
            })
            setFormData(initialFormState)
            await loadDepots()
        } catch (requestError) {
            setFormError(requestError.message)
        } finally {
            setIsSubmitting(false)
        }
    }

    return (
        <section className="floodlink-card">
            <div className="page-heading">
                <div>
                    <p className="eyebrow">Operations / Network</p>
                    <h2 className="floodlink-header">Depot Management</h2>
                    <p className="page-subtitle">Keep supply points accurate for matching and routing.</p>
                </div>
                <span className="data-count">{depots.length} depot{depots.length === 1 ? '' : 's'}</span>
            </div>

            <form className="depot-form" onSubmit={handleSubmit}>
                <div className="floodlink-form-grid">
                    <input
                        required
                        className="floodlink-input"
                        placeholder="Depot name"
                        name="name"
                        value={formData.name}
                        onChange={handleInputChange}
                    />
                    <input
                        required
                        type="number"
                        step="any"
                        className="floodlink-input"
                        placeholder="Latitude"
                        name="latitude"
                        value={formData.latitude}
                        onChange={handleInputChange}
                    />
                    <input
                        required
                        type="number"
                        step="any"
                        className="floodlink-input"
                        placeholder="Longitude"
                        name="longitude"
                        value={formData.longitude}
                        onChange={handleInputChange}
                    />
                    <input
                        type="number"
                        className="floodlink-input"
                        placeholder="Manager ID (optional)"
                        name="managerId"
                        value={formData.managerId}
                        onChange={handleInputChange}
                    />
                </div>
                <div className="form-actions">
                    <button type="submit" className="floodlink-btn" disabled={isSubmitting}>
                        {isSubmitting ? 'Saving...' : formData.id ? 'Update depot' : 'Add depot'}
                    </button>
                    {formData.id && (
                        <button
                            type="button"
                            className="floodlink-btn secondary-btn"
                            onClick={() => setFormData(initialFormState)}
                        >
                            Cancel
                        </button>
                    )}
                </div>
                {formError && <div className="state-error form-error">{formError}</div>}
            </form>

            {loading && <div className="loading-spinner">Loading depots...</div>}
            {!loading && error && <div className="state-message state-error">{error}</div>}
            {!loading && !error && depots.length === 0 && <div className="state-message">No depots found.</div>}
            {!loading && !error && depots.length > 0 && (
                <div className="floodlink-table-wrapper">
                    <table className="floodlink-table">
                        <thead>
                            <tr>
                                <th>ID</th>
                                <th>Name</th>
                                <th>Coordinates</th>
                                <th>Manager</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {depots.map((depot) => (
                                <tr key={depot.id}>
                                    <td>{depot.id}</td>
                                    <td className="cell-strong">{depot.name}</td>
                                    <td className="muted-cell">{depot.latitude}, {depot.longitude}</td>
                                    <td>{depot.managerId ?? 'Unassigned'}</td>
                                    <td>
                                        <button
                                            className="table-action"
                                            onClick={() => handleEdit(depot)}
                                        >
                                            Edit
                                        </button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}
        </section>
    )
}
