# ADR-011 — Carbon components for the whole React portal, with automated accessibility checks

**Status:** Accepted (18 Oct 2026) · **Tasks:** 8.18, 8.19

## Context

The portal grew page by page in three styles: Carbon components (Inventory, Depots, Allocations, Stock
history), hand-written "Carbon-like" CSS classes (Approvals, Dispatches, Analytics) and Tailwind utility
classes (Reports, Shelters, Triage, Workflows, Trace, Users, Login). Buttons, inputs and notifications
looked and behaved differently between pages, and an accessibility review found real problems: clickable
`div`s that keyboards could not reach (shelter cards, triage runs), a decision-notes label not tied to its
text box, a "modal" without dialog semantics, Esc or focus handling, information only in hover tooltips,
and a header that wrapped twelve links onto several lines on small screens.

## Decision

- Use `@carbon/react` components (white theme) for every control and surface in the React portal; keep
  only layout CSS of our own (`fl-*` classes in `index.css`). The custom button/badge/notification/table
  classes were removed.
- Replace the custom header with the Carbon UI Shell: pages grouped into menus (Operations, Stock,
  Insights, Admin), a side navigation on narrow screens, a skip-to-content link.
- Map workflow states to Carbon tag colours (`stateTagType`): in progress blue, awaiting approval purple
  (Carbon has no yellow tag), revision warm gray, approved green, rejected/failed red.
- Guard accessibility with tests: axe-core (WCAG 2.1 A/AA) on every screen in its loaded state, plus a
  WCAG contrast test for our own colour pairs (jsdom cannot compute contrast).

## Consequences

- Pages share one look and behaviour; Carbon's components bring keyboard support and ARIA by default.
- Carbon's `TextInput` always renders an empty `role="alert"` counter, so tests look for the alert that
  contains their message rather than "the only alert".
- Carbon `Tabs` require tab panels; view switches use `ContentSwitcher` instead.
- Carbon drops custom `title` attributes on tags, which pushed details that were hover-only into visible text.
- The bundle includes Carbon's CSS (~1 MB uncompressed, ~110 KB gzipped), as before.
- Narrow-screen layout relies on CSS grids that collapse to one column and tables that scroll inside their
  container; it is not covered by automated tests (jsdom has no layout) and should be checked by hand on a phone.
