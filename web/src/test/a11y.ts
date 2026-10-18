import axe from 'axe-core'

/**
 * Runs axe-core (WCAG 2.1 A/AA rules) on a rendered component and returns readable violations.
 * Two rules are off here: colour contrast (jsdom can't compute styles; see contrast.test.ts) and
 * "region" (components are rendered without the app's header/main landmarks).
 */
export async function a11yViolations(container: Element): Promise<string[]> {
  const result = await axe.run(container, {
    runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'best-practice'] },
    rules: { 'color-contrast': { enabled: false }, region: { enabled: false } },
  })
  return result.violations.map(
    (v) => `${v.id}: ${v.help} — ${v.nodes.map((n) => n.target.join(' ')).slice(0, 3).join(' | ')}`,
  )
}
