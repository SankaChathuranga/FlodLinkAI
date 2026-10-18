import { request } from './client'
import type { Shelter } from '../types'

/** Shelter write endpoints (Coordinator only). Errors throw ApiError with the API's { error, message }. */

export interface ShelterInput {
  name: string
  latitude: number
  longitude: number
  capacity: number
  currentOccupancy: number
  contactVolunteerId: number | null
  status: 'Active' | 'Closed'
}

export function createShelter(baseUrl: string, shelter: ShelterInput) {
  return request<Shelter>(baseUrl, '/api/shelters', { method: 'POST', body: JSON.stringify(shelter) })
}

export function updateShelter(baseUrl: string, id: number, shelter: ShelterInput) {
  return request<Shelter>(baseUrl, `/api/shelters/${id}`, { method: 'PUT', body: JSON.stringify(shelter) })
}
