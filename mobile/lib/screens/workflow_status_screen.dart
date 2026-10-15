import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../api/dispatch_api.dart' show ApiException;
import '../api/workflow_api.dart';
import '../main.dart' show FloodLinkColors;
import 'delivery_confirmation_screen.dart';

/// Status tracker for one workflow run (4.06). Polls the status endpoint every
/// [pollInterval] and shows: processing → matched → routed → awaiting approval → the
/// coordinator's decision (approved / rejected / revision), with the coordinator's note, the
/// ETA once approved, and a link to confirm delivery. Polling stops once nothing can change.
class WorkflowStatusScreen extends StatefulWidget {
  const WorkflowStatusScreen({
    super.key,
    required this.workflowRunId,
    this.pollInterval = const Duration(seconds: 5),
    this.api,
  });

  final String workflowRunId;
  final Duration pollInterval;

  /// Injected in tests; otherwise built from the app's [ApiClient].
  final WorkflowApi? api;

  @override
  State<WorkflowStatusScreen> createState() => _WorkflowStatusScreenState();
}

class _WorkflowStatusScreenState extends State<WorkflowStatusScreen> {
  late final WorkflowApi _api = widget.api ?? WorkflowApi(context.read<ApiClient>());
  Timer? _timer;
  WorkflowStatus? _status;
  String? _error;
  bool _loading = true;
  DateTime? _checkedAt;

  @override
  void initState() {
    super.initState();
    _poll();
    _timer = Timer.periodic(widget.pollInterval, (_) => _poll());
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _poll() async {
    try {
      final status = await _api.fetchStatus(widget.workflowRunId);
      if (!mounted) return;
      setState(() {
        _status = status;
        _error = null;
        _loading = false;
        _checkedAt = DateTime.now();
      });
      if (status.isSettled) _timer?.cancel();
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _loading = false;
      });
      if (e.statusCode == 404 || e.statusCode == 403) _timer?.cancel();
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'No connection. Retrying automatically…';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final status = _status;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Supply request status'),
        actions: [
          IconButton(icon: const Icon(Icons.refresh), tooltip: 'Refresh', onPressed: _poll),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _poll,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            if (_loading && status == null)
              const Padding(
                padding: EdgeInsets.only(top: 48),
                child: Center(child: CircularProgressIndicator()),
              ),
            if (_error != null) _Banner(text: _error!, error: true),
            if (status != null) ...[
              _Header(status: status),
              const SizedBox(height: 16),
              _Stages(status: status),
              if (status.coordinatorNote != null && status.coordinatorNote!.isNotEmpty) ...[
                const SizedBox(height: 16),
                _NoteCard(status: status),
              ],
              if (status.state == 'Failed') ...[
                const SizedBox(height: 16),
                _Banner(
                  text: 'The plan could not be completed'
                      '${status.failedAtState != null ? ' (${status.failedAtState})' : ''}. '
                      'A coordinator has been notified and can retry it.',
                  error: true,
                ),
              ],
              if (status.isApproved) ...[
                const SizedBox(height: 16),
                _DeliveryCard(status: status, onDelivered: _poll),
              ],
              const SizedBox(height: 16),
              Text(
                _checkedAt == null
                    ? ''
                    : status.isSettled
                        ? 'Final status. Checked ${_time(_checkedAt!)}.'
                        : 'Updates every ${widget.pollInterval.inSeconds} s · last checked ${_time(_checkedAt!)}',
                style: const TextStyle(fontSize: 12, color: FloodLinkColors.textMuted),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

String _time(DateTime t) =>
    '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}:${t.second.toString().padLeft(2, '0')}';

class _Header extends StatelessWidget {
  const _Header({required this.status});
  final WorkflowStatus status;

  @override
  Widget build(BuildContext context) {
    final failed = status.state == 'Failed' || status.state == 'Rejected';
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              status.stageLabel,
              key: const Key('stage-label'),
              style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w600, color: FloodLinkColors.textPrimary),
            ),
            const SizedBox(height: 12),
            Semantics(
              label: 'Progress ${status.progressPercent} percent',
              child: LinearProgressIndicator(
                value: status.progressPercent / 100,
                minHeight: 6,
                color: failed ? FloodLinkColors.stateError : FloodLinkColors.accentPrimary,
                backgroundColor: FloodLinkColors.borderDefault,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The steps a volunteer sees: processing → matched → routed → awaiting approval → outcome.
class _Stages extends StatelessWidget {
  const _Stages({required this.status});
  final WorkflowStatus status;

  static const _order = ['Triage', 'Matching', 'Routing', 'Validating', 'PendingApproval'];

  int get _reached {
    final state = status.state == 'Failed' ? (status.failedAtState ?? 'Triage') : status.state;
    if (state == 'RevisionRequested') return 1; // back to matching
    final i = _order.indexOf(state);
    return i == -1 ? _order.length : i; // decided → past every step
  }

  @override
  Widget build(BuildContext context) {
    final reached = _reached;
    final steps = <(String, bool, bool)>[
      ('Processing reports', reached > 0, reached == 0),
      ('Supplies matched', reached > 1, reached == 1),
      ('Route planned', reached > 3, reached == 2 || reached == 3),
      ('Awaiting coordinator approval', reached > 4, reached == 4),
    ];
    final outcome = switch (status.state) {
      'Approved' => ('Approved', FloodLinkColors.stateSuccess, Icons.check_circle),
      'Rejected' => ('Rejected', FloodLinkColors.stateError, Icons.cancel),
      'RevisionRequested' => ('Revision requested — re-planning', FloodLinkColors.stateWarning, Icons.replay),
      'Failed' => ('Stopped', FloodLinkColors.stateError, Icons.error),
      _ => null,
    };

    return Card(
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Column(
          children: [
            for (final (label, done, current) in steps)
              ListTile(
                dense: true,
                leading: Icon(
                  done ? Icons.check_circle : current ? Icons.radio_button_checked : Icons.radio_button_unchecked,
                  color: done
                      ? FloodLinkColors.stateSuccess
                      : current
                          ? FloodLinkColors.accentPrimary
                          : FloodLinkColors.textMuted,
                ),
                title: Text(
                  label,
                  style: TextStyle(
                    fontWeight: current ? FontWeight.w600 : FontWeight.normal,
                    color: done || current ? FloodLinkColors.textPrimary : FloodLinkColors.textMuted,
                  ),
                ),
              ),
            if (outcome != null)
              ListTile(
                dense: true,
                leading: Icon(outcome.$3, color: outcome.$2),
                title: Text(outcome.$1, key: const Key('outcome'), style: TextStyle(fontWeight: FontWeight.w600, color: outcome.$2)),
              ),
          ],
        ),
      ),
    );
  }
}

class _NoteCard extends StatelessWidget {
  const _NoteCard({required this.status});
  final WorkflowStatus status;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Coordinator note', style: TextStyle(fontWeight: FontWeight.w600)),
            const SizedBox(height: 6),
            Text(status.coordinatorNote!, key: const Key('coordinator-note')),
          ],
        ),
      ),
    );
  }
}

class _DeliveryCard extends StatelessWidget {
  const _DeliveryCard({required this.status, required this.onDelivered});
  final WorkflowStatus status;
  final Future<void> Function() onDelivered;

  @override
  Widget build(BuildContext context) {
    final eta = status.etaMinutes;
    final distance = status.distanceKm;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              status.isDelivered
                  ? 'Delivered'
                  : eta != null
                      ? 'Supplies on the way — ETA about ${eta.round()} min'
                      : 'Supplies approved for dispatch',
              key: const Key('eta'),
              style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600, color: FloodLinkColors.stateSuccess),
            ),
            if (distance != null && !status.isDelivered)
              Text('${distance.toStringAsFixed(1)} km by road', style: const TextStyle(color: FloodLinkColors.textMuted)),
            if (!status.isDelivered) ...[
              const SizedBox(height: 12),
              FilledButton.icon(
                icon: const Icon(Icons.local_shipping_outlined),
                label: const Text('Confirm delivery'),
                onPressed: () async {
                  await Navigator.of(context).push(MaterialPageRoute<void>(
                    builder: (_) => DeliveryConfirmationScreen(workflowRunId: status.id),
                  ));
                  await onDelivered();
                },
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Banner extends StatelessWidget {
  const _Banner({required this.text, required this.error});
  final String text;
  final bool error;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      liveRegion: true,
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: error ? FloodLinkColors.badgeErrorBg : FloodLinkColors.badgeInfoBg,
          border: Border.all(color: error ? FloodLinkColors.stateError : FloodLinkColors.stateInfo),
          borderRadius: BorderRadius.circular(4),
        ),
        child: Text(text),
      ),
    );
  }
}
