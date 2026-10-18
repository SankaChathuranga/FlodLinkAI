/**
 * Decodes an encoded polyline (Google/Mapbox "polyline" format, precision 5 — what the Route agent
 * stores from Mapbox `geometries=polyline`) into [longitude, latitude] pairs. Returns [] for text
 * that is not a valid polyline.
 */
export function decodePolyline(encoded: string, precision = 5): [number, number][] {
  const factor = 10 ** precision
  const points: [number, number][] = []
  let index = 0
  let lat = 0
  let lng = 0

  const next = (): number | null => {
    let result = 0
    let shift = 0
    let byte: number
    do {
      if (index >= encoded.length) return null
      byte = encoded.charCodeAt(index++) - 63
      if (byte < 0 || byte > 63 + 31) return null
      result |= (byte & 0x1f) << shift
      shift += 5
    } while (byte >= 0x20)
    return result & 1 ? ~(result >> 1) : result >> 1
  }

  while (index < encoded.length) {
    const dLat = next()
    const dLng = next()
    if (dLat === null || dLng === null) return []
    lat += dLat
    lng += dLng
    points.push([lng / factor, lat / factor])
  }
  return points
}
