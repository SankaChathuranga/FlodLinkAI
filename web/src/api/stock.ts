import { request } from './client'
import type { Depot, PaginatedResult } from '../types'

/** Depot and allocation endpoints (Member B). Errors throw ApiError with the API's { error, message }. */

export interface DepotManagerOption {
  id: number
  name: string
  email: string | null
}

export interface DepotInput {
  name: string
  latitude: number
  longitude: number
  managerId: number | null
}

export interface Allocation {
  id: number
  workflowRunId: string
  depotId: number
  depotName: string | null
  shelterId: number
  shelterName: string | null
  itemName: string
  quantity: number
  status: 'Proposed' | 'Reserved' | 'Committed' | 'Released' | string
  createdAt: string
}

export function listDepots(baseUrl: string, params: { search?: string; page?: number; pageSize?: number } = {}) {
  const qs = new URLSearchParams({ page: String(params.page ?? 1), pageSize: String(params.pageSize ?? 20) })
  if (params.search) qs.set('search', params.search)
  return request<PaginatedResult<Depot>>(baseUrl, `/api/depots?${qs.toString()}`)
}

export function listDepotManagers(baseUrl: string) {
  return request<DepotManagerOption[]>(baseUrl, '/api/depots/managers')
}

export function createDepot(baseUrl: string, depot: DepotInput) {
  return request<Depot>(baseUrl, '/api/depots', { method: 'POST', body: JSON.stringify(depot) })
}

export function updateDepot(baseUrl: string, id: number, depot: DepotInput) {
  return request<Depot>(baseUrl, `/api/depots/${id}`, { method: 'PUT', body: JSON.stringify(depot) })
}

export function listAllocations(
  baseUrl: string,
  params: { status?: string; shelterId?: number; workflowRunId?: string; page?: number; pageSize?: number } = {},
) {
  const qs = new URLSearchParams({ page: String(params.page ?? 1), pageSize: String(params.pageSize ?? 20) })
  if (params.status) qs.set('status', params.status)
  if (params.shelterId) qs.set('shelterId', String(params.shelterId))
  if (params.workflowRunId) qs.set('workflowRunId', params.workflowRunId)
  return request<PaginatedResult<Allocation>>(baseUrl, `/api/allocations?${qs.toString()}`)
}

/** One stock change (GET /api/inventory/movements). */
export interface StockMovement {
  id: number
  inventoryItemId: number
  itemName: string
  unit: string
  depotId: number
  depotName: string | null
  kind: 'Opening' | 'CheckIn' | 'ManualHold' | 'ManualRelease' | 'PlanReserve' | 'PlanCommit' | 'PlanRelease' | 'PhysicalCount' | string
  quantity: number
  availableAfter: number
  reservedAfter: number
  workflowRunId: string | null
  userId: number | null
  userName: string | null
  note: string | null
  createdAt: string
}

export function listMovements(
  baseUrl: string,
  params: { inventoryItemId?: number; depotId?: number; kind?: string; page?: number; pageSize?: number } = {},
) {
  const qs = new URLSearchParams({ page: String(params.page ?? 1), pageSize: String(params.pageSize ?? 20) })
  if (params.inventoryItemId) qs.set('inventoryItemId', String(params.inventoryItemId))
  if (params.depotId) qs.set('depotId', String(params.depotId))
  if (params.kind) qs.set('kind', params.kind)
  return request<PaginatedResult<StockMovement>>(baseUrl, `/api/inventory/movements?${qs.toString()}`)
}
