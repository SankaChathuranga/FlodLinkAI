import { useEffect, useMemo, useRef, useState } from 'react'
import mapboxgl from 'mapbox-gl'
import 'mapbox-gl/dist/mapbox-gl.css'
import { decodePolyline } from '../lib/polyline'

/** One delivery leg to draw: its encoded polyline and a label for depot → shelter. */
export interface RouteMapLeg {
  key: string
  label: string
  polyline: string
}

const MAPBOX_TOKEN = import.meta.env.VITE_MAPBOX_TOKEN as string | undefined
const COLORS = ['#0f62fe', '#da1e28', '#198038', '#8a3ffc', '#b28600', '#007d79']

/**
 * Route map preview (8.10): draws every leg of a plan. Uses Mapbox when VITE_MAPBOX_TOKEN is set,
 * otherwise a plain SVG drawing of the same lines (no basemap) so the shape of the plan is still visible.
 */
export function RouteMap({ legs }: { legs: RouteMapLeg[] }) {
  // Rebuilt only when the legs change, not on every render (the trace polls while a run is active).
  const signature = legs.map((l) => `${l.key}:${l.label}:${l.polyline}`).join('|')
  const lines = useMemo(
    () => legs
      .map((leg, i) => ({ ...leg, color: COLORS[i % COLORS.length], points: decodePolyline(leg.polyline) }))
      .filter((leg) => leg.points.length >= 2),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [signature],
  )

  if (lines.length === 0) return <p className="cds--body-compact-01 fl-text-muted">No route geometry to draw.</p>

  return (
    <figure className="fl-route-map">
      {MAPBOX_TOKEN ? <MapboxRoutes lines={lines} /> : <SvgRoutes lines={lines} />}
      <figcaption>
        <ul className="fl-route-map__legend" aria-label="Map legend">
          {lines.map((l) => (
            <li key={l.key}>
              <span aria-hidden="true" className="fl-route-map__swatch" style={{ background: l.color }} />
              {l.label}
            </li>
          ))}
        </ul>
      </figcaption>
    </figure>
  )
}

type Line = RouteMapLeg & { color: string; points: [number, number][] }

function SvgRoutes({ lines }: { lines: Line[] }) {
  const all = lines.flatMap((l) => l.points)
  const [minX, maxX] = [Math.min(...all.map((p) => p[0])), Math.max(...all.map((p) => p[0]))]
  const [minY, maxY] = [Math.min(...all.map((p) => p[1])), Math.max(...all.map((p) => p[1]))]
  const span = Math.max(maxX - minX, maxY - minY, 1e-6)
  const pad = 20
  const size = 400
  const project = ([x, y]: [number, number]) =>
    [pad + ((x - minX) / span) * (size - 2 * pad), size - pad - ((y - minY) / span) * (size - 2 * pad)] as const

  return (
    <svg role="img" aria-label={`Route map with ${lines.length} leg${lines.length === 1 ? '' : 's'}`}
      viewBox={`0 0 ${size} ${size}`} className="fl-route-map__svg">
      {lines.map((l) => {
        const pts = l.points.map(project)
        const [sx, sy] = pts[0]
        const [ex, ey] = pts[pts.length - 1]
        return (
          <g key={l.key} data-testid="route-leg">
            <polyline points={pts.map((p) => p.join(',')).join(' ')} fill="none" stroke={l.color} strokeWidth={3} strokeLinejoin="round" />
            <circle cx={sx} cy={sy} r={5} fill="#fff" stroke={l.color} strokeWidth={2}><title>{`Depot · ${l.label}`}</title></circle>
            <circle cx={ex} cy={ey} r={5} fill={l.color}><title>{`Shelter · ${l.label}`}</title></circle>
          </g>
        )
      })}
    </svg>
  )
}

function MapboxRoutes({ lines }: { lines: Line[] }) {
  const containerRef = useRef<HTMLDivElement>(null)
  // Mapbox needs WebGL; without it (old browsers, some VMs, tests) draw the SVG version instead.
  const [unavailable, setUnavailable] = useState(false)

  useEffect(() => {
    if (!containerRef.current) return
    mapboxgl.accessToken = MAPBOX_TOKEN as string
    const bounds = new mapboxgl.LngLatBounds()
    lines.forEach((l) => l.points.forEach((p) => bounds.extend(p)))
    let map: mapboxgl.Map
    try {
      map = new mapboxgl.Map({ container: containerRef.current, style: 'mapbox://styles/mapbox/streets-v12', bounds, fitBoundsOptions: { padding: 40 } })
    } catch {
      setUnavailable(true)
      return
    }
    map.addControl(new mapboxgl.NavigationControl({ showCompass: false }), 'top-right')
    const markers: mapboxgl.Marker[] = []
    map.on('load', () => {
      lines.forEach((l) => {
        map.addSource(l.key, { type: 'geojson', data: { type: 'Feature', properties: {}, geometry: { type: 'LineString', coordinates: l.points } } })
        map.addLayer({ id: l.key, type: 'line', source: l.key, layout: { 'line-join': 'round', 'line-cap': 'round' }, paint: { 'line-color': l.color, 'line-width': 4 } })
        markers.push(
          new mapboxgl.Marker({ color: '#ffffff' }).setLngLat(l.points[0]).setPopup(new mapboxgl.Popup().setText(`Depot · ${l.label}`)).addTo(map),
          new mapboxgl.Marker({ color: l.color }).setLngLat(l.points[l.points.length - 1]).setPopup(new mapboxgl.Popup().setText(`Shelter · ${l.label}`)).addTo(map),
        )
      })
    })
    return () => {
      markers.forEach((m) => m.remove())
      map.remove()
    }
  }, [lines])

  if (unavailable) return <SvgRoutes lines={lines} />
  return <div ref={containerRef} role="region" aria-label="Route map" className="fl-route-map__canvas" />
}
