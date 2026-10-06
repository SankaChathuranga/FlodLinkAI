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
  photoUrl?: string | null
  gpsLat?: number | null
  gpsLng?: number | null
  status: 'New' | 'Triaged' | 'InPlan' | 'Resolved' | string
  createdAt: string
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
  createdAt: string
}

export interface InventoryItem {
  id: number
  depotId: number
  depot?: Depot | null
  itemName: string
  unit: string
  quantityAvailable: number
  updatedAt: string
}
