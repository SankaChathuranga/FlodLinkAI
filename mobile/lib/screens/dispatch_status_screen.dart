import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/dispatch_api.dart';
import '../api/models.dart';
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

    setState(() {
      _loading = true;
      _error = null;
      _status = null;
    });

    try {
      final api = DispatchApi(context.read<AppState>().apiBaseUrl);
      final status = await api.fetchStatus(runId);
      if (!mounted) return;
      setState(() {
        _status = status;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = 'Could not reach the API. Is the backend running?';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final status = _status;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Dispatch status'),
        backgroundColor: Theme.of(context).colorScheme.primary,
        foregroundColor: Colors.white,
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          TextField(
            controller: _runIdController,
            decoration: const InputDecoration(
              labelText: 'Workflow run id',
              hintText: 'UUID, e.g. 9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d',
              border: OutlineInputBorder(),
              prefixIcon: Icon(Icons.search),
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
            _Banner(icon: Icons.error_outline, tone: Colors.red, text: _error!),
          ],
          if (status != null) ...[
            const SizedBox(height: 16),
            _StatusCard(status: status),
          ],
        ],
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
        tone: Colors.red,
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
        tone: delivered ? Colors.green : Colors.teal,
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
                    builder: (_) => DeliveryConfirmationScreen(
                      workflowRunId: status.workflowRunId,
                    ),
                  ),
                ),
                icon: const Icon(Icons.task_alt),
                label: const Text('Mark as delivered'),
              ),
            ),
        ],
      );
    }

    return _DecisionCard(
      tone: Colors.blueGrey,
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
    required this.tone,
    required this.title,
    required this.icon,
    required this.children,
  });

  final Color tone;
  final String title;
  final IconData icon;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 2,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, color: tone),
                const SizedBox(width: 8),
                Text(
                  title,
                  style: TextStyle(
                    fontSize: 20,
                    fontWeight: FontWeight.bold,
                    color: tone,
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
          Text(
            label,
            style: const TextStyle(fontSize: 12, color: Colors.grey),
          ),
          const SizedBox(height: 2),
          Text(value),
        ],
      ),
    );
  }
}

class _Banner extends StatelessWidget {
  const _Banner({required this.icon, required this.tone, required this.text});

  final IconData icon;
  final Color tone;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: tone.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: tone),
      ),
      child: Row(
        children: [
          Icon(icon, color: tone),
          const SizedBox(width: 8),
          Expanded(child: Text(text)),
        ],
      ),
    );
  }
}

String _formatDate(DateTime value) => value.toLocal().toString();