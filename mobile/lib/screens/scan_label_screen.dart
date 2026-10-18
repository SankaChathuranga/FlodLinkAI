import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../stock/stock_label.dart';

/// Camera scanner for FloodLink stock labels (8.06). Pops with the scanned stock line's id as soon as a
/// valid label is seen; other codes are ignored with a hint.
class ScanLabelScreen extends StatefulWidget {
  const ScanLabelScreen({super.key});

  @override
  State<ScanLabelScreen> createState() => _ScanLabelScreenState();
}

class _ScanLabelScreenState extends State<ScanLabelScreen> {
  final MobileScannerController _controller = MobileScannerController(formats: const [BarcodeFormat.qrCode]);
  bool _done = false;
  String? _hint;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _onDetect(BarcodeCapture capture) {
    if (_done) return;
    for (final barcode in capture.barcodes) {
      final id = parseStockLabel(barcode.rawValue);
      if (id != null) {
        _done = true;
        Navigator.of(context).pop(id);
        return;
      }
    }
    setState(() => _hint = 'That code is not a FloodLink stock label.');
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Scan stock label')),
      body: Stack(
        children: [
          MobileScanner(
            controller: _controller,
            onDetect: _onDetect,
            errorBuilder: (context, error) => Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Text(
                  error.errorCode == MobileScannerErrorCode.permissionDenied
                      ? 'Camera permission is needed to scan labels. Allow it in the phone settings.'
                      : 'The camera could not start (${error.errorCode.name}).',
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: Colors.white),
                ),
              ),
            ),
          ),
          Positioned(
            left: 16,
            right: 16,
            bottom: 32,
            child: Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(color: Colors.black.withValues(alpha: 0.7), borderRadius: BorderRadius.circular(6)),
              child: Text(
                _hint ?? 'Point the camera at the QR code on the shelf label.',
                textAlign: TextAlign.center,
                style: const TextStyle(color: Colors.white),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
