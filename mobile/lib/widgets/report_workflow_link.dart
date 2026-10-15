import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../main.dart' show FloodLinkColors;
import '../models/report.dart';

/// Bottom row of a report card: where the report is in its workflow, and a link to the status
/// tracker (4.07). Reports not yet in a workflow say so instead.
class ReportWorkflowLink extends StatelessWidget {
  const ReportWorkflowLink({super.key, required this.report});

  final FieldReport report;

  static String label(FieldReport r) {
    if (r.workflowRunId == null) return 'Waiting for a coordinator to plan supplies';
    return switch (r.workflowState) {
      'Triage' || 'Matching' || 'Routing' || 'Validating' => 'Supplies being planned',
      'RevisionRequested' => 'Plan being revised',
      'PendingApproval' => 'Awaiting coordinator approval',
      'Approved' => 'Approved — supplies dispatched',
      'Rejected' => 'Request rejected',
      'Failed' => 'Planning stopped — coordinator notified',
      _ => 'View status',
    };
  }

  @override
  Widget build(BuildContext context) {
    final runId = report.workflowRunId;
    final text = Text(
      label(report),
      style: TextStyle(
        fontSize: 13,
        fontWeight: FontWeight.w600,
        color: runId == null ? FloodLinkColors.textMuted : FloodLinkColors.accentPrimary,
      ),
    );
    if (runId == null) {
      return Padding(padding: const EdgeInsets.only(top: 10), child: text);
    }
    return Padding(
      padding: const EdgeInsets.only(top: 6),
      child: InkWell(
        key: Key('report-status-${report.id}'),
        onTap: () => context.push('/status/$runId'),
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 8),
          child: Row(
            children: [
              const Icon(Icons.local_shipping_outlined, size: 18, color: FloodLinkColors.accentPrimary),
              const SizedBox(width: 6),
              Expanded(child: text),
              const Icon(Icons.chevron_right, color: FloodLinkColors.accentPrimary),
            ],
          ),
        ),
      ),
    );
  }
}
