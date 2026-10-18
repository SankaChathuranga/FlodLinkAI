import { useEffect, useState } from 'react'
import { InlineNotification, Modal, NumberInput, Select, SelectItem, TextInput } from '@carbon/react'
import { useAppContext } from '../context/AppContext'
import { describeApiError } from '../lib/apiError'
import { createShelter, updateShelter, type ShelterInput } from '../api/shelters'
import type { Shelter } from '../types'

const EMPTY: ShelterInput = {
  name: '', latitude: 6.9271, longitude: 79.8612, capacity: 100, currentOccupancy: 0, contactVolunteerId: null, status: 'Active',
}

interface ShelterFormModalProps {
  /** Closed when undefined; `null` adds a new shelter, a shelter edits it. */
  shelter: Shelter | null | undefined
  onClose: () => void
  onSaved: (name: string, created: boolean) => void
}

/** Add or edit a shelter (Coordinators). The contact volunteer is kept as-is on edit. */
export function ShelterFormModal({ shelter, onClose, onSaved }: ShelterFormModalProps) {
  const { apiBaseUrl } = useAppContext()
  const open = shelter !== undefined
  const [form, setForm] = useState<ShelterInput>(EMPTY)
  const [formError, setFormError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (!open) return
    setFormError(null)
    setForm(shelter
      ? {
          name: shelter.name, latitude: shelter.latitude, longitude: shelter.longitude, capacity: shelter.capacity,
          currentOccupancy: shelter.currentOccupancy, contactVolunteerId: shelter.contactVolunteerId ?? null,
          status: shelter.status === 'Closed' ? 'Closed' : 'Active',
        }
      : { ...EMPTY })
  }, [open, shelter])

  const setField = <K extends keyof ShelterInput>(key: K, value: ShelterInput[K]) =>
    setForm((current) => ({ ...current, [key]: value }))

  const validate = (f: ShelterInput): string | null => {
    if (!f.name.trim()) return 'Enter the shelter name.'
    if (!Number.isFinite(f.latitude) || f.latitude < -90 || f.latitude > 90) return 'Latitude must be between -90 and 90.'
    if (!Number.isFinite(f.longitude) || f.longitude < -180 || f.longitude > 180) return 'Longitude must be between -180 and 180.'
    if (f.latitude === 0 && f.longitude === 0) return 'Set the shelter position; (0, 0) is not a real location.'
    if (!Number.isInteger(f.capacity) || f.capacity < 0) return 'Capacity must be a whole number, 0 or more.'
    if (!Number.isInteger(f.currentOccupancy) || f.currentOccupancy < 0) return 'Occupancy must be a whole number, 0 or more.'
    return null
  }

  const save = async () => {
    const problem = validate(form)
    if (problem) {
      setFormError(problem)
      return
    }
    setSaving(true)
    setFormError(null)
    try {
      const body = { ...form, name: form.name.trim() }
      if (shelter) await updateShelter(apiBaseUrl, shelter.id, body)
      else await createShelter(apiBaseUrl, body)
      onSaved(body.name, !shelter)
    } catch (err) {
      setFormError(describeApiError(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal
      open={open}
      modalHeading={shelter ? 'Edit shelter' : 'Add shelter'}
      primaryButtonText={saving ? 'Saving…' : 'Save shelter'}
      secondaryButtonText="Cancel"
      primaryButtonDisabled={saving}
      onRequestSubmit={() => void save()}
      onRequestClose={() => !saving && onClose()}
    >
      {formError && <InlineNotification kind="error" title="Shelter not saved" subtitle={formError} lowContrast hideCloseButton />}
      {open && (
        <div className="depot-form">
          <TextInput id="shelter-name" labelText="Shelter name" maxLength={150} value={form.name}
            onChange={(event) => setField('name', event.target.value)} />
          <NumberInput id="shelter-latitude" label="Latitude" min={-90} max={90} step={0.0001} allowEmpty
            value={form.latitude} onChange={(_, state) => setField('latitude', Number(state.value))} />
          <NumberInput id="shelter-longitude" label="Longitude" min={-180} max={180} step={0.0001} allowEmpty
            value={form.longitude} onChange={(_, state) => setField('longitude', Number(state.value))} />
          <NumberInput id="shelter-capacity" label="Capacity" min={0} step={1} allowEmpty
            value={form.capacity} onChange={(_, state) => setField('capacity', Number(state.value))} />
          <NumberInput id="shelter-occupancy" label="Current occupancy" min={0} step={1} allowEmpty
            value={form.currentOccupancy} onChange={(_, state) => setField('currentOccupancy', Number(state.value))} />
          <Select id="shelter-status" labelText="Status" value={form.status}
            onChange={(event) => setField('status', event.target.value as ShelterInput['status'])}>
            <SelectItem value="Active" text="Active" />
            <SelectItem value="Closed" text="Closed" />
          </Select>
        </div>
      )}
    </Modal>
  )
}
