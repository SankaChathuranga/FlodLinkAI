// ── FloodLink AI — WorkflowState Badge ───────────────────────────────────────
//
// Semantic colour mapping (from the FloodLink UI spec):
//
//   Triage / Matching / Routing / Validating → --state-info  (blue)
//   PendingApproval / RevisionRequested       → --state-warning (amber, dark text)
//   Approved                                  → --state-success (green)
//   Rejected / Failed                         → --state-error  (red)
//
// RULE: --accent-primary (#0F62FE) is NEVER used in badges.
//       --state-info (#0043CE) is ONLY for the in-progress states above.
// ─────────────────────────────────────────────────────────────────────────────

export type WorkflowState =
  | 'Triage'
  | 'Matching'
  | 'Routing'
  | 'Validating'
  | 'PendingApproval'
  | 'RevisionRequested'
  | 'Approved'
  | 'Rejected'
  | 'Failed';

type BadgeVariant = 'info' | 'warning' | 'success' | 'error';

const STATE_VARIANT: Record<WorkflowState, BadgeVariant> = {
  Triage:             'info',
  Matching:           'info',
  Routing:            'info',
  Validating:         'info',
  PendingApproval:    'warning',
  RevisionRequested:  'warning',
  Approved:           'success',
  Rejected:           'error',
  Failed:             'error',
};

interface WorkflowStateBadgeProps {
  state: WorkflowState;
  /** Render a coloured dot before the label. Defaults to true. */
  dot?: boolean;
}

export function WorkflowStateBadge({ state, dot = true }: WorkflowStateBadgeProps) {
  const variant = STATE_VARIANT[state];

  return (
    <span className={`fl-badge fl-badge--${variant}`} aria-label={`Status: ${state}`}>
      {dot && <span className="fl-badge__dot" aria-hidden="true" />}
      {state}
    </span>
  );
}
