import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/dispatch_api.dart';
import '../api/models.dart';
import '../main.dart' show FloodLinkColors;
import '../state/app_state.dart';
import 'delivery_confirmation_screen.dart';

/// Field-worker screen: look up a workflow run and see whether it was approved,
/// rejected (with the stated reason) or already delivered.
class DispatchStatusScreen extends StatefulWidget {
  const DispatchStatusScreen({super.key});

  @override
  State<DispatchStatusScreen> createState() => _DispatchStatusScreenState();
}

class _DispatchStatusScreenState extends State<DispatchStatusScreen> {
  final TextEditingController _runIdController = TextEditingController();
  DispatchStatus? _status;
  bool _loading = false;
  String? _error;

  @override
  void dispose() {
    _runIdController.dispose();
    super.dispose();
  }

  Future<void> _lookup() async {
    final runId = _runIdController.text.trim();
    if (runId.isEmpty) {
      setState(() => _error = 'Enter a workflow run id first.');
      return;
    }
    setState(() { _loading = true; _error = null; _status = null; });
    try {
      final api = DispatchApi(context.read<AppState>().apiBaseUrl);
      final status = await api.fetchStatus(runId);
      if (!mounted) return;
      setState(() { _status = status; _loading = false; });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() { _loading = false; _error = e.message; });
    } catch (_) {
      if (!mounted) return;
      setState(() { _loading = false; _error = 'Could not reach the API. Is the backend running?'; });
    }
  }

  @override
  Widget build(BuildContext context) {
    final status = _status;
    return Scaffold(
      appBar: AppBar(title: const Text('Dispatch status')),
      body: Container(
        color: FloodLinkColors.bgBase,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            TextField(
              controller: _runIdController,
              decoration: const InputDecoration(
                labelText: 'Workflow run id',
                hintText: 'UUID, e.g. 9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d',
                prefixIcon: Icon(Icons.search, color: FloodLinkColors.textMuted),
              ),
              onSubmitted: (_) => _lookup(),
            ),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: _loading ? null : _lookup,
              icon: const Icon(Icons.search),
              label: Text(_loading ? 'Loading…' : 'Look up'),
            ),
            if (_error != null) ...[
              const SizedBox(height: 16),
              _InlineNotification(tone: _NotificationTone.error, text: _error!),
            ],
            if (status != null) ...[
              const SizedBox(height: 16),
              _StatusCard(status: status),
            ],
          ],
        ),
      ),
    );
  }
}

class _StatusCard extends StatelessWidget {
  const _StatusCard({required this.status});
  final DispatchStatus status;

  @override
  Widget build(BuildContext context) {
    final decision = status.decision;

    if (decision == 'Rejected') {
      return _DecisionCard(
        toneColor: FloodLinkColors.stateError,
        badgeBg: FloodLinkColors.badgeErrorBg,
        title: 'Rejected',
        icon: Icons.cancel_outlined,
        children: [
          _InfoRow(label: 'Reason', value: status.notes ?? 'No reason recorded.'),
        ],
      );
    }

    if (decision == 'Approved') {
      final delivered = status.isDelivered ?? false;
      return _DecisionCard(
        toneColor: delivered ? FloodLinkColors.stateSuccess : FloodLinkColors.accentPrimary,
        badgeBg: delivered ? FloodLinkColors.badgeSuccessBg : FloodLinkColors.badgeInfoBg,
        title: delivered ? 'Delivered ✓' : 'Approved',
        icon: delivered ? Icons.check_circle_outline : Icons.local_shipping_outlined,
        children: [
          if (status.notes != null) _InfoRow(label: 'Notes', value: status.notes!),
          if (delivered && status.deliveredAt != null)
            _InfoRow(label: 'Delivered at', value: _formatDate(status.deliveredAt!)),
          if (!delivered)
            Align(
              alignment: Alignment.centerLeft,
              child: FilledButton.icon(
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute<void>(
                    builder: (_) => DeliveryConfirmationScreen(workflowRunId: status.workflowRunId),
                  ),
                ),
                icon: const Icon(Icons.task_alt),
                label: const Text('Mark as delivered'),
              ),
            ),
        ],
      );
    }

    // In-progress — info tone (Triage/Matching/Routing/Validating → --state-info)
    return _DecisionCard(
      toneColor: FloodLinkColors.stateInfo,
      badgeBg: FloodLinkColors.badgeInfoBg,
      title: 'In progress',
      icon: Icons.hourglass_top,
      children: [
        _InfoRow(label: 'State', value: status.state),
        if (status.notes != null) _InfoRow(label: 'Notes', value: status.notes!),
      ],
    );
  }
}

class _DecisionCard extends StatelessWidget {
  const _DecisionCard({
    required this.toneColor,
    required this.badgeBg,
    required this.title,
    required this.icon,
    required this.children,
  });

  final Color toneColor;
  final Color badgeBg;
  final String title;
  final IconData icon;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(
                    color: badgeBg,
                    borderRadius: BorderRadius.circular(9999),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(icon, color: toneColor, size: 14),
                      const SizedBox(width: 4),
                      Text(
                        title,
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                          color: toneColor,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            ...children,
          ],
        ),
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(fontSize: 11, color: FloodLinkColors.textMuted)),
          const SizedBox(height: 2),
          Text(value, style: const TextStyle(fontSize: 14, color: FloodLinkColors.textPrimary)),
        ],
      ),
    );
  }
}

enum _NotificationTone { error, success, warning, info }

class _InlineNotification extends StatelessWidget {
  const _InlineNotification({required this.tone, required this.text});
  final _NotificationTone tone;
  final String text;

  @override
  Widget build(BuildContext context) {
    final (bg, border, icon) = switch (tone) {
      _NotificationTone.error   => (const Color(0xFFFFF1F1), FloodLinkColors.stateError,   Icons.error_outline),
      _NotificationTone.success => (const Color(0xFFDEFBE6), FloodLinkColors.stateSuccess, Icons.check_circle_outline),
      _NotificationTone.warning => (const Color(0xFFFFF8E1), FloodLinkColors.stateWarning, Icons.warning_amber_outlined),
      _NotificationTone.info    => (const Color(0xFFEDF5FF), FloodLinkColors.stateInfo,    Icons.info_outline),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(4),
        border: Border(left: BorderSide(color: border, width: 4)),
      ),
      child: Row(
        children: [
          Icon(icon, color: border, size: 18),
          const SizedBox(width: 8),
          Expanded(child: Text(text, style: const TextStyle(fontSize: 14, color: FloodLinkColors.textPrimary))),
        ],
      ),
    );
  }
}

String _formatDate(DateTime value) => value.toLocal().toString();
