import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/report_provider.dart';

/// Shows reports saved on the phone while offline (8.09): how many are waiting, a "Send now" button,
/// and any the server refused, with the reason and a way to discard them. Nothing when the queue is empty.
class QueuedReportsBanner extends StatelessWidget {
  const QueuedReportsBanner({super.key});

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ReportProvider>();
    final queued = provider.queuedReports;
    if (queued.isEmpty) return const SizedBox.shrink();
    final waiting = queued.where((r) => !r.refused).length;
    final refused = queued.where((r) => r.refused).toList();

    return Material(
      color: const Color(0xFFEDF5FF),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 8, 8, 8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (waiting > 0)
              Row(
                children: [
                  const Icon(Icons.cloud_upload_outlined,
                      color: Color(0xFF0043CE)),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      '$waiting report${waiting == 1 ? '' : 's'} saved on this phone, waiting for a connection',
                      style: const TextStyle(color: Color(0xFF161616)),
                    ),
                  ),
                  TextButton(
                    onPressed: provider.isSendingQueue
                        ? null
                        : provider.sendQueuedReports,
                    child:
                        Text(provider.isSendingQueue ? 'Sending…' : 'Send now'),
                  ),
                ],
              ),
            for (final report in refused)
              Row(
                children: [
                  const Icon(Icons.error_outline, color: Color(0xFFDA1E28)),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      '${report.needType} × ${report.quantityNeeded} was not accepted: ${report.lastError}',
                      style: const TextStyle(color: Color(0xFF161616)),
                    ),
                  ),
                  TextButton(
                    onPressed: () =>
                        provider.discardQueuedReport(report.clientReference),
                    child: const Text('Discard'),
                  ),
                ],
              ),
          ],
        ),
      ),
    );
  }
}
