export interface User {
  id: number
  name: string
  role: string
  phone: string
  createdAt: string
}

export interface Report {
  id: number
  shelterId: number
  shelter?: Shelter
  reportedBy: number
  reporter?: User
  needType: 'Water' | 'Food' | 'Medical' | 'Shelter-Repair' | 'Other' | string
  quantityNeeded: number
  urgencyLevel: number
  /** Volunteer's free-text notes (max 500). Shown as plain text, never as HTML. */
  notes?: string | null
  photoUrl?: string | null
  gpsLat?: number | null
  gpsLng?: number | null
  status: 'New' | 'Triaged' | 'InPlan' | 'Resolved' | string
  createdAt: string
  /** The workflow run currently (or last) handling this report. */
  workflowRunId?: string | null
}

export interface Shelter {
  id: number
  name: string
  latitude: number
  longitude: number
  capacity: number
  currentOccupancy: number
  contactVolunteerId?: number | null
  contactVolunteer?: User | null
  status: 'Active' | 'Closed' | string
  createdAt: string
  updatedAt: string
  reports?: Report[]
}

export interface PaginatedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface Depot {
  id: number
  name: string
  latitude: number
  longitude: number
  /** From GET /api/depots: the assigned DepotManager and how many stock lines the depot holds. */
  managerId?: number | null
  managerName?: string | null
  itemCount?: number
  createdAt: string
  updatedAt?: string
}

export interface InventoryItem {
  id: number
  depotId: number
  depot?: Depot | null
  itemName: string
  unit: string
  /** On hand at the depot, including reserved stock. */
  quantityAvailable: number
  /** Held for plans awaiting approval; free stock is available − reserved. */
  quantityReserved?: number
  /** Reserve floor: free stock at or below this is low. */
  reorderThreshold?: number
  updatedAt: string
}
