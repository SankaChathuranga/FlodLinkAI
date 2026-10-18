import { useEffect, useRef, useState } from 'react'
import mapboxgl from 'mapbox-gl'
import 'mapbox-gl/dist/mapbox-gl.css'
import type { Shelter } from '../types'

interface ShelterMapProps {
  shelters: Shelter[]
  selectedShelterId?: number | null
  onSelectShelter?: (shelter: Shelter) => void
}

function SimulatedShelterMap({ shelters, selectedShelterId, onSelectShelter }: ShelterMapProps) {
  const [activeHoverId, setActiveHoverId] = useState<number | null>(null)

  // Normalize lat/lng to map coordinates (Colombo area bounding box default)
  const minLat = 6.8
  const maxLat = 7.2
  const minLng = 79.7
  const maxLng = 80.1

  const getPosition = (lat: number, lng: number) => {
    const x = Math.min(Math.max(((lng - minLng) / (maxLng - minLng)) * 100, 5), 95)
    const y = Math.min(Math.max((1 - (lat - minLat) / (maxLat - minLat)) * 100, 5), 95)
    return { left: `${x}%`, top: `${y}%` }
  }

  return (
    <div className="relative w-full h-80 bg-slate-900 rounded-xl overflow-hidden border border-slate-800 shadow-inner">
      {/* Background Map Grid & Topology Simulation */}
      <div className="absolute inset-0 bg-slate-950 opacity-90">
        <svg className="w-full h-full text-slate-800" fill="none">
          <defs>
            <pattern id="grid" width="40" height="40" patternUnits="userSpaceOnUse">
              <path d="M 40 0 L 0 0 0 40" fill="none" stroke="currentColor" strokeWidth="0.5" />
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill="url(#grid)" />
          {/* River / coastline representation */}
          <path d="M 30,0 Q 40,150 100,320" stroke="#0284c7" strokeWidth="6" opacity="0.3" fill="none" />
        </svg>
      </div>

      <div className="absolute top-3 left-3 bg-slate-900/80 backdrop-blur border border-slate-700 px-3 py-1.5 rounded-lg text-xs font-mono text-slate-300 z-10 flex items-center space-x-2">
        <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
        <span>Interactive GIS Shelter Map (Colombo &amp; Western Region)</span>
      </div>

      {/* Map Pins */}
      {shelters.map((shelter) => {
        const occupancyPct = shelter.capacity > 0 ? Math.round((shelter.currentOccupancy / shelter.capacity) * 100) : 0
        const isOvercrowded = occupancyPct >= 100
        const isHigh = occupancyPct >= 80
        const isSelected = shelter.id === selectedShelterId || activeHoverId === shelter.id
        const pos = getPosition(shelter.latitude, shelter.longitude)

        return (
          <div
            key={shelter.id}
            style={{ left: pos.left, top: pos.top }}
            className="absolute transform -translate-x-1/2 -translate-y-1/2 z-20 transition-all cursor-pointer group"
            onClick={() => onSelectShelter?.(shelter)}
            onMouseEnter={() => setActiveHoverId(shelter.id)}
            onMouseLeave={() => setActiveHoverId(null)}
          >
            {/* Marker Pin */}
            <div
              className={`w-7 h-7 rounded-full flex items-center justify-center font-bold text-xs shadow-lg transition-transform ${
                isSelected ? 'scale-125 ring-4 ring-blue-400' : ''
              } ${
                shelter.status.toLowerCase() === 'closed'
                  ? 'bg-slate-600 text-slate-300'
                  : isOvercrowded
                  ? 'bg-rose-600 text-white'
                  : isHigh
                  ? 'bg-amber-500 text-white'
                  : 'bg-emerald-600 text-white'
              }`}
            >
              {occupancyPct}%
            </div>

            {/* Popup Tooltip */}
            <div className="hidden group-hover:block absolute bottom-full left-1/2 transform -translate-x-1/2 mb-2 w-48 bg-slate-900 text-white text-xs rounded-lg p-2.5 shadow-xl border border-slate-700 z-30 pointer-events-none">
              <p className="font-bold text-slate-100">{shelter.name}</p>
              <p className="text-slate-400 text-[10px]">
                Lat: {shelter.latitude.toFixed(4)}, Lng: {shelter.longitude.toFixed(4)}
              </p>
              <div className="mt-1 flex items-center justify-between text-[11px]">
                <span>Occupancy:</span>
                <span className="font-semibold text-blue-400">
                  {shelter.currentOccupancy} / {shelter.capacity} ({occupancyPct}%)
                </span>
              </div>
              <span className={`inline-block mt-1 px-1.5 py-0.5 rounded text-[10px] font-semibold ${
                shelter.status.toLowerCase() === 'active' ? 'bg-emerald-950 text-emerald-300' : 'bg-rose-950 text-rose-300'
              }`}>
                {shelter.status}
              </span>
            </div>
          </div>
        )
      })}
    </div>
  )
}

const MAPBOX_TOKEN = import.meta.env.VITE_MAPBOX_TOKEN as string | undefined

function pinColor(shelter: Shelter) {
  const pct = shelter.capacity > 0 ? (shelter.currentOccupancy / shelter.capacity) * 100 : 0
  if (shelter.status.toLowerCase() === 'closed') return '#475569'
  if (pct >= 100) return '#e11d48'
  if (pct >= 80) return '#f59e0b'
  return '#059669'
}

function MapboxShelterMap({ shelters, selectedShelterId, onSelectShelter }: ShelterMapProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const mapRef = useRef<mapboxgl.Map | null>(null)
  const markersRef = useRef<mapboxgl.Marker[]>([])
  const onSelectRef = useRef(onSelectShelter)
  onSelectRef.current = onSelectShelter

  useEffect(() => {
    if (!containerRef.current) return
    mapboxgl.accessToken = MAPBOX_TOKEN as string
    const map = new mapboxgl.Map({
      container: containerRef.current,
      style: 'mapbox://styles/mapbox/outdoors-v12',
      center: [79.8612, 6.9271],
      zoom: 9,
    })
    map.addControl(new mapboxgl.NavigationControl({ showCompass: false }), 'top-right')
    mapRef.current = map
    return () => {
      map.remove()
      mapRef.current = null
    }
  }, [])

  useEffect(() => {
    const map = mapRef.current
    if (!map) return
    markersRef.current.forEach((m) => m.remove())
    markersRef.current = shelters.map((shelter) => {
      const pct = shelter.capacity > 0 ? Math.round((shelter.currentOccupancy / shelter.capacity) * 100) : 0
      const el = document.createElement('div')
      el.textContent = `${pct}%`
      el.style.cssText = `width:28px;height:28px;border-radius:9999px;display:flex;align-items:center;justify-content:center;font:700 11px sans-serif;color:#fff;cursor:pointer;box-shadow:0 2px 6px rgba(0,0,0,.5);background:${pinColor(shelter)};${
        shelter.id === selectedShelterId ? 'outline:4px solid #60a5fa;' : ''
      }`
      el.addEventListener('click', () => onSelectRef.current?.(shelter))
      const popup = new mapboxgl.Popup({ offset: 18, closeButton: false }).setHTML(
        `<strong>${shelter.name.replace(/</g, '&lt;')}</strong><br/>${shelter.currentOccupancy} / ${shelter.capacity} (${pct}%) · ${shelter.status.replace(/</g, '&lt;')}`,
      )
      return new mapboxgl.Marker({ element: el }).setLngLat([shelter.longitude, shelter.latitude]).setPopup(popup).addTo(map)
    })
    if (shelters.length > 0) {
      const bounds = new mapboxgl.LngLatBounds()
      shelters.forEach((s) => bounds.extend([s.longitude, s.latitude]))
      map.fitBounds(bounds, { padding: 60, maxZoom: 13, duration: 0 })
    }
  }, [shelters, selectedShelterId])

  return <div ref={containerRef} className="relative w-full h-80 rounded-xl overflow-hidden border border-slate-800" />
}

export function ShelterMap(props: ShelterMapProps) {
  // Falls back to the simulated grid map when no Mapbox token is configured.
  return MAPBOX_TOKEN ? <MapboxShelterMap {...props} /> : <SimulatedShelterMap {...props} />
}
