import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/dispatch_api.dart';
import '../api/models.dart';
import '../main.dart' show FloodLinkColors;
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
    _runIdController = TextEditingController(text: widget.workflowRunId ?? '');
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
    setState(() { _submitting = true; _error = null; _result = null; });
    try {
      final api = DispatchApi(context.read<AppState>().apiBaseUrl);
      final result = await api.confirmDelivery(
        runId,
        notes: _notesController.text.trim().isEmpty ? null : _notesController.text.trim(),
        photoReference: _photoController.text.trim().isEmpty ? null : _photoController.text.trim(),
      );
      if (!mounted) return;
      setState(() { _result = result; _submitting = false; });
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() { _submitting = false; _error = e.message; });
    } catch (_) {
      if (!mounted) return;
      setState(() { _submitting = false; _error = 'Could not reach the API. Is the backend running?'; });
    }
  }

  @override
  Widget build(BuildContext context) {
    final result = _result;
    return Scaffold(
      appBar: AppBar(title: const Text('Confirm delivery')),
      body: Container(
        color: FloodLinkColors.bgBase,
        child: result != null && result.isDelivered
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
                      prefixIcon: Icon(Icons.tag, color: FloodLinkColors.textMuted),
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _notesController,
                    decoration: const InputDecoration(
                      labelText: 'Notes (optional)',
                      hintText: 'e.g. Handed 12x water packs to Shelter #12 staff.',
                      prefixIcon: Icon(Icons.notes, color: FloodLinkColors.textMuted),
                    ),
                    maxLines: 3,
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _photoController,
                    decoration: const InputDecoration(
                      labelText: 'Photo reference (optional)',
                      hintText: 'e.g. IMG_0421.jpg — full upload lands in Week 5+',
                      prefixIcon: Icon(Icons.photo_library_outlined, color: FloodLinkColors.textMuted),
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
                    _InlineNotification(text: _error!),
                  ],
                ],
              ),
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
            Container(
              padding: const EdgeInsets.all(16),
              decoration: const BoxDecoration(
                color: FloodLinkColors.badgeSuccessBg,
                shape: BoxShape.circle,
              ),
              child: const Icon(Icons.check_circle_outline, color: FloodLinkColors.stateSuccess, size: 48),
            ),
            const SizedBox(height: 16),
            const Text(
              'Delivery confirmed',
              style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: FloodLinkColors.textPrimary),
            ),
            const SizedBox(height: 8),
            // Monospace for IDs per spec
            Text(
              workflowRunId,
              style: const TextStyle(
                fontSize: 11,
                color: FloodLinkColors.textMuted,
                fontFamily: 'IBMPlexMono',
              ),
            ),
            const SizedBox(height: 24),
            FilledButton(onPressed: onDone, child: const Text('Done')),
          ],
        ),
      ),
    );
  }
}

class _InlineNotification extends StatelessWidget {
  const _InlineNotification({required this.text});
  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: const Color(0xFFFFF1F1),
        borderRadius: BorderRadius.circular(4),
        border: const Border(left: BorderSide(color: FloodLinkColors.stateError, width: 4)),
      ),
      child: Row(
        children: [
          const Icon(Icons.error_outline, color: FloodLinkColors.stateError, size: 18),
          const SizedBox(width: 8),
          Expanded(child: Text(text, style: const TextStyle(fontSize: 14, color: FloodLinkColors.textPrimary))),
        ],
      ),
    );
  }
}
