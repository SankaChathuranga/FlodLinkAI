import { describe, expect, it } from 'vitest'

// 8.19: WCAG 2.1 AA contrast (4.5:1 for text) for the colour pairs the portal's own CSS uses.
// Carbon's components meet AA in its white theme; these are FloodLink's additions (index.css).

function luminance(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4))
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

export function contrast(foreground: string, background: string): number {
  const [a, b] = [luminance(foreground), luminance(background)].sort((x, y) => y - x)
  return (a + 0.05) / (b + 0.05)
}

const TEXT_PAIRS: [string, string, string][] = [
  ['body text on page background', '#161616', '#f4f4f4'],
  ['muted text on page background', '#6f6f6f', '#f4f4f4'],
  ['muted text on tiles', '#6f6f6f', '#ffffff'],
  ['links / accent on tiles', '#0f62fe', '#ffffff'],
  ['error text on tiles', '#da1e28', '#ffffff'],
  ['error text in failed-check rows', '#da1e28', '#fff1f1'],
  ['success text on tiles', '#198038', '#ffffff'],
  ['signed-in user in header', '#c6c6c6', '#161616'],
  ['role in header', '#78a9ff', '#161616'],
  ['helper text in LLM-accepted box', '#525252', '#f6f2ff'],
  ['helper text in fallback box', '#525252', '#fcf4d6'],
  ['body text in warning callout', '#161616', '#fcf4d6'],
  ['notes text', '#161616', '#f4f4f4'],
]

describe('colour contrast (WCAG 2.1 AA)', () => {
  it('matches the reference value for black on white', () => {
    expect(contrast('#000000', '#ffffff')).toBeCloseTo(21, 5)
  })

  it.each(TEXT_PAIRS)('%s is at least 4.5:1', (_name, fg, bg) => {
    expect(contrast(fg, bg)).toBeGreaterThanOrEqual(4.5)
  })

  it('the selected-card outline and focus ring stand out (3:1 for UI parts)', () => {
    expect(contrast('#0f62fe', '#ffffff')).toBeGreaterThanOrEqual(3)
    expect(contrast('#0f62fe', '#f4f4f4')).toBeGreaterThanOrEqual(3)
  })
})
