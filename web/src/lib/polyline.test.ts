import { describe, expect, it } from 'vitest'
import { decodePolyline } from './polyline'

describe('decodePolyline', () => {
  it('decodes the reference example into [lng, lat] pairs', () => {
    // Example from the encoded polyline algorithm documentation.
    expect(decodePolyline('_p~iF~ps|U_ulLnnqC_mqNvxq`@')).toEqual([
      [-120.2, 38.5],
      [-120.95, 40.7],
      [-126.453, 43.252],
    ])
  })

  it('returns no points for empty or malformed text', () => {
    expect(decodePolyline('')).toEqual([])
    expect(decodePolyline('_p~iF')).toEqual([]) // latitude without longitude
  })
})
