import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/dispatch_api.dart';
import '../api/models.dart';
import '../state/app_state.dart';

/// Field-worker screen: record that an approved dispatch arrived on-site.
/// Notes and an optional photo reference are stored with the audit entry.
class DeliveryConfirmationScreen extends StatefulWidget {
  const DeliveryConfirmationScreen({super.key, this.workflowRunId});

  /// Pre-filled run id when navigating from the status screen.
  final String? workflowRunId;

  @override
  State<DeliveryConfirmationScreen> createState() =>
      _DeliveryConfirmationScreenState();
}

class _DeliveryConfirmationScreenState extends State<DeliveryConfirmationScreen> {
  late final TextEditingController _runIdController;
  final TextEditingController _notesController = TextEditingController();
  final TextEditingController _photoController = TextEditingController();
  bool _submitting = false;
  DeliveryResult? _result;
  String? _error;

  @override
  void initState() {
    super.initState();
    _runIdController =
        TextEditingController(text: widget.workflowRunId ?? '');
  }

  @override
  void dispose() {
    _runIdController.dispose();
    _notesController.dispose();
    _photoController.dispose();
    super.dispose();
  }

  Future<void> _confirm() async {
    final runId = _runIdController.text.trim();
    if (runId.isEmpty) {
      setState(() => _error = 'Enter the workflow run id you delivered to.');
      return;
    }

    setState(() {
      _submitting = true;
      _error = null;
      _result = null;
    });

    try {
      final api = DispatchApi(context.read<AppState>().apiBaseUrl);
      final result = await api.confirmDelivery(
        runId,
        notes: _notesController.text.trim().isEmpty
            ? null
            : _notesController.text.trim(),
        photoReference: _photoController.text.trim().isEmpty
            ? null
            : _photoController.text.trim(),
      );
      if (!mounted) return;
      setState(() {
        _result = result;
        _submitting = false;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _error = e.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _error = 'Could not reach the API. Is the backend running?';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final result = _result;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Confirm delivery'),
        backgroundColor: Theme.of(context).colorScheme.primary,
        foregroundColor: Colors.white,
      ),
      body: result != null && result.isDelivered
          ? _DeliverySuccess(
              workflowRunId: result.workflowRunId,
              onDone: () => Navigator.of(context).pop(),
            )
          : ListView(
              padding: const EdgeInsets.all(16),
              children: [
                TextField(
                  controller: _runIdController,
                  decoration: const InputDecoration(
                    labelText: 'Workflow run id',
                    border: OutlineInputBorder(),
                    prefixIcon: Icon(Icons.tag),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _notesController,
                  decoration: const InputDecoration(
                    labelText: 'Notes (optional)',
                    hintText: 'e.g. Handed 12x water packs to Shelter #12 staff.',
                    border: OutlineInputBorder(),
                    prefixIcon: Icon(Icons.notes),
                  ),
                  maxLines: 3,
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _photoController,
                  decoration: const InputDecoration(
                    labelText: 'Photo reference (optional)',
                    hintText: 'e.g. IMG_0421.jpg — full upload lands in Week 5+',
                    border: OutlineInputBorder(),
                    prefixIcon: Icon(Icons.photo_library_outlined),
                  ),
                ),
                const SizedBox(height: 20),
                FilledButton.icon(
                  onPressed: _submitting ? null : _confirm,
                  icon: const Icon(Icons.task_alt),
                  label: Text(_submitting ? 'Confirming…' : 'Confirm delivery'),
                ),
                if (_error != null) ...[
                  const SizedBox(height: 16),
                  _ErrorBanner(text: _error!),
                ],
              ],
            ),
    );
  }
}

class _DeliverySuccess extends StatelessWidget {
  const _DeliverySuccess({required this.workflowRunId, required this.onDone});

  final String workflowRunId;
  final VoidCallback onDone;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.check_circle, color: Colors.green, size: 72),
            const SizedBox(height: 16),
            const Text(
              'Delivery confirmed',
              style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            Text(
              workflowRunId,
              style: const TextStyle(color: Colors.grey, fontSize: 12),
            ),
            const SizedBox(height: 24),
            FilledButton(onPressed: onDone, child: const Text('Done')),
          ],
        ),
      ),
    );
  }
}

class _ErrorBanner extends StatelessWidget {
  const _ErrorBanner({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.red.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: Colors.red),
      ),
      child: Row(
        children: [
          const Icon(Icons.error_outline, color: Colors.red),
          const SizedBox(width: 8),
          Expanded(child: Text(text)),
        ],
      ),
    );
  }
}