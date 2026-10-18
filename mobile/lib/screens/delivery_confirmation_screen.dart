import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:provider/provider.dart';

import '../api/dispatch_api.dart';
import '../api/models.dart';
import '../main.dart' show FloodLinkColors;
import '../api/api_client.dart';

/// Picks a photo from the camera or gallery; null when the user cancels.
typedef PhotoPicker = Future<XFile?> Function(ImageSource source);

/// Field-worker screen: record that an approved dispatch arrived on-site.
/// Notes and an optional photo of the delivery (8.16) are stored with the audit entry.
class DeliveryConfirmationScreen extends StatefulWidget {
  const DeliveryConfirmationScreen({super.key, this.workflowRunId, this.pickPhoto});

  /// Pre-filled run id when navigating from the status screen.
  final String? workflowRunId;

  /// Injected in tests; otherwise the device camera / gallery.
  final PhotoPicker? pickPhoto;

  @override
  State<DeliveryConfirmationScreen> createState() =>
      _DeliveryConfirmationScreenState();
}

class _DeliveryConfirmationScreenState extends State<DeliveryConfirmationScreen> {
  late final TextEditingController _runIdController;
  final TextEditingController _notesController = TextEditingController();
  XFile? _photo;
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
    super.dispose();
  }

  // The server decides the image type from the extension; picked files without a name are camera JPEGs.
  static String _photoName(XFile photo) => photo.name.contains('.') ? photo.name : 'delivery.jpg';

  Future<void> _pick(ImageSource source) async {
    final picker = widget.pickPhoto ?? (s) => ImagePicker().pickImage(source: s, maxWidth: 1920, imageQuality: 85);
    try {
      final photo = await picker(source);
      if (photo != null && mounted) setState(() => _photo = photo);
    } catch (_) {
      if (mounted) setState(() => _error = 'Could not open the camera or gallery.');
    }
  }

  Future<void> _confirm() async {
    final runId = _runIdController.text.trim();
    if (runId.isEmpty) {
      setState(() => _error = 'Enter the workflow run id you delivered to.');
      return;
    }
    setState(() { _submitting = true; _error = null; _result = null; });
    try {
      final api = DispatchApi(context.read<ApiClient>());
      // Upload the photo first; its URL is stored with the confirmation.
      final photo = _photo;
      final photoUrl = photo == null ? null : await api.uploadDeliveryPhoto(runId, await photo.readAsBytes(), _photoName(photo));
      final result = await api.confirmDelivery(
        runId,
        notes: _notesController.text.trim().isEmpty ? null : _notesController.text.trim(),
        photoReference: photoUrl,
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
                  Text(
                    _photo == null ? 'Photo of the delivery (optional)' : 'Photo attached: ${_photoName(_photo!)}',
                    key: const Key('photo-status'),
                    style: const TextStyle(color: FloodLinkColors.textPrimary),
                  ),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    children: [
                      OutlinedButton.icon(
                        onPressed: _submitting ? null : () => _pick(ImageSource.camera),
                        icon: const Icon(Icons.photo_camera_outlined),
                        label: const Text('Take photo'),
                      ),
                      OutlinedButton.icon(
                        onPressed: _submitting ? null : () => _pick(ImageSource.gallery),
                        icon: const Icon(Icons.photo_library_outlined),
                        label: const Text('Choose photo'),
                      ),
                      if (_photo != null)
                        TextButton(
                          onPressed: _submitting ? null : () => setState(() => _photo = null),
                          child: const Text('Remove photo'),
                        ),
                    ],
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
