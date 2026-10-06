# UI Context — FloodLink AI

## Theme

Calm, professional humanitarian operations interface inspired by the **IBM
Carbon Design System**. Light-first, information-dense, trustworthy, and
designed for high-pressure disaster-response workflows. Avoid typical AI
aesthetics such as neon gradients, excessive glassmorphism, glowing effects,
robots, and futuristic visuals.

## Colors

IBM Carbon-inspired neutrals with FloodLink operational blue and semantic
emergency colors. All components must use CSS variables — no hardcoded hex
values. Reuse these exact hex values in the Flutter `ColorScheme` so status
colors look identical on web and mobile.

| Role            | CSS Variable        | Value     |
| ---------------- | -------------------- | --------- |
| Page background   | `--bg-base`           | `#F4F4F4` |
| Surface            | `--bg-surface`        | `#FFFFFF` |
| Primary text       | `--text-primary`      | `#161616` |
| Muted text         | `--text-muted`        | `#6F6F6F` |
| Primary accent     | `--accent-primary`    | `#0F62FE` |
| Border             | `--border-default`    | `#C6C6C6` |
| Error              | `--state-error`       | `#DA1E28` |
| Warning            | `--state-warning`     | `#F1C21B` |
| Success            | `--state-success`     | `#198038` |
| Info               | `--state-info`        | `#0043CE` |

**Usage rule:** `--accent-primary` is only for interactive elements —
buttons, links, active nav, focus rings. `--state-info` is only for
informational status badges (e.g. Triage/Matching/Routing/Validating
in-progress). Never substitute one for the other.

**Warning contrast rule:** `--state-warning` (`#F1C21B`) has poor contrast
for text on white or `--bg-base`. Always pair it with `--text-primary` (dark
text) on top of it — never white text on yellow, and never yellow used for
body text.

**Semantic mapping to `WorkflowState`** (apply consistently everywhere a
status badge appears):
`Triage / Matching / Routing / Validating` → `--state-info` ·
`PendingApproval / RevisionRequested` → `--state-warning` ·
`Approved` → `--state-success` · `Rejected / Failed` → `--state-error`

## Typography

| Role      | Font          | Variable       |
| --------- | ------------- | -------------- |
| UI text   | IBM Plex Sans | `--font-sans`  |
| Code/mono | IBM Plex Mono | `--font-mono`  |

Mono reserved for IDs, coordinates, and raw JSON/log values in the
`AgentExecutionLog` viewer.

## Border Radius

| Context            | Class          |
| -------------------- | -------------- |
| Inline / small UI      | `rounded-sm`   |
| Cards / panels          | `rounded-md`   |
| Modals / overlays       | `rounded-md`   |
| Status badges           | `rounded-full` |

## Component Library

**IBM Carbon Design System (`@carbon/react` + `@carbon/styles`)** as the
primary foundation for web. Tailwind CSS and shadcn/ui are not used on
web — Carbon brings its own Sass-based styling system, and running both
would cause styling conflicts. Base theme: Carbon's `g10` theme (light),
with our custom accent/semantic colors from the table above layered on top
via Carbon's theme override mechanism.

Create custom FloodLink components for domain-specific elements (incident
reports, AI response plans, dispatch reviews, inventory status, validation
results, approvals, routes, audit timelines) by composing Carbon primitives
— prioritize `DataTable`, `Tag`, `Tile`, `Modal`, `Tabs`, and
`InlineNotification` first, since they map directly onto workflow-run
lists, status badges, approval modals, and agent-failure alerts.

## Layout Patterns

- **Web shell:** fixed Carbon-style left sidebar (Active Runs, Approvals
  Queue, Routes, Logs) using Carbon's `Header`/`SideNav`, with a compact top
  header and main operational workspace
- **Dashboard:** map-first layout combining incident, resource, and AI
  response information. **Map library: decide and record here before
  building** — this choice should be made together with the Route/ETA
  Agent's maps/routing API, since some providers (e.g. Mapbox) bundle both
  a map renderer and a directions API and pairing them avoids juggling two
  vendors
- **Data screens:** Carbon `DataTable` for inventory, incidents, dispatches,
  and audit records
- **Detail screens (approval flow):** Information → Map/Evidence → AI
  Recommendation → Validation → Human Approval → Audit, shown as a
  horizontal stepper (Carbon `ProgressIndicator`) with Approve/Reject/
  Request Revision actions fixed at the bottom
- **Modals:** Carbon `Modal` with backdrop, used for approval confirmation
  and agent-failure detail (`AgentExecutionLog` entries)
- **Mobile:** task-first Flutter interface for field volunteers — large
  touch targets, minimal text entry, camera/GPS-first reporting flow.
  "Offline-friendly" is a real architecture requirement, not just a layout
  note: it implies local storage/sync (e.g. `drift`, `hive`, or `sqflite`)
  for queuing field reports without connectivity — needs its own design
  decision outside this doc, flag it to whoever owns the mobile reporting
  feature

## Icons

**IBM Carbon Icons (`@carbon/icons-react`)**. Simple, clean icons with
consistent stroke weight. Use icons to communicate meaning and status (e.g.
urgency, validation state) rather than decoration.