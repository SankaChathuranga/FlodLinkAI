import 'dart:io';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';
import 'package:provider/provider.dart';
import '../providers/report_provider.dart';

class CameraCaptureScreen extends StatefulWidget {
  const CameraCaptureScreen({super.key});

  @override
  State<CameraCaptureScreen> createState() => _CameraCaptureScreenState();
}

class _CameraCaptureScreenState extends State<CameraCaptureScreen> {
  final ImagePicker _picker = ImagePicker();
  XFile? _imageFile;
  bool _isProcessing = false;
  String? _errorMessage;

  Future<void> _pickImage(ImageSource source) async {
    setState(() {
      _isProcessing = true;
      _errorMessage = null;
    });

    try {
      final XFile? photo = await _picker.pickImage(
        source: source,
        maxWidth: 1920,
        maxHeight: 1080,
        imageQuality: 85,
      );

      if (photo != null) {
        setState(() {
          _imageFile = photo;
        });
      }
    } catch (e) {
      setState(() {
        _errorMessage = 'Failed to access camera or gallery: $e';
      });
    } finally {
      setState(() {
        _isProcessing = false;
      });
    }
  }

  void _confirmPhoto() {
    if (_imageFile != null) {
      context.read<ReportProvider>().setCapturedPhoto(_imageFile!.path);
      context.pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF4F4F4), // --bg-base
      appBar: AppBar(
        title: const Text('Capture Photo Evidence'),
        backgroundColor: const Color(0xFF0F62FE), // --accent-primary
        foregroundColor: Colors.white,
        elevation: 0,
      ),
      body: SafeArea(
        child: Column(
          children: [
            // ERROR STATE
            if (_errorMessage != null)
              Container(
                color: const Color(0xFFDA1E28).withValues(alpha: 0.1), // --state-error
                width: double.infinity,
                padding: const EdgeInsets.all(12),
                child: Row(
                  children: [
                    const Icon(Icons.error_outline, color: Color(0xFFDA1E28)),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        _errorMessage!,
                        style: const TextStyle(color: Color(0xFFDA1E28), fontSize: 13),
                      ),
                    ),
                    IconButton(
                      icon: const Icon(Icons.refresh, color: Color(0xFFDA1E28)),
                      onPressed: () => _pickImage(ImageSource.camera),
                    ),
                  ],
                ),
              ),

            // IMAGE PREVIEW / EMPTY STATE / LOADING STATE
            Expanded(
              child: Container(
                margin: const EdgeInsets.all(16),
                width: double.infinity,
                decoration: BoxDecoration(
                  color: const Color(0xFFFFFFFF), // --bg-surface
                  borderRadius: BorderRadius.circular(6), // rounded-md
                  border: Border.all(color: const Color(0xFFC6C6C6)), // --border-default
                ),
                child: _isProcessing
                    ? const Center(
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            CircularProgressIndicator(color: Color(0xFF0F62FE)),
                            SizedBox(height: 16),
                            Text('Processing photo...', style: TextStyle(color: Color(0xFF6F6F6F))),
                          ],
                        ),
                      )
                    : _imageFile != null
                        ? ClipRRect(
                            borderRadius: BorderRadius.circular(6),
                            child: Image.file(
                              File(_imageFile!.path),
                              fit: BoxFit.cover,
                            ),
                          )
                        : Column(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              const Icon(
                                Icons.add_a_photo_outlined,
                                size: 64,
                                color: Color(0xFF6F6F6F), // --text-muted
                              ),
                              const SizedBox(height: 16),
                              const Text(
                                'No Photo Evidence Captured',
                                style: TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                  color: Color(0xFF161616),
                                ),
                              ),
                              const SizedBox(height: 8),
                              const Text(
                                'Tap below to capture a photo from camera or select from gallery.',
                                textAlign: TextAlign.center,
                                style: TextStyle(
                                  fontSize: 12,
                                  color: Color(0xFF6F6F6F),
                                ),
                              ),
                            ],
                          ),
              ),
            ),

            // CONTROLS & ACTIONS
            Padding(
              padding: const EdgeInsets.all(16.0),
              child: Column(
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: ElevatedButton.icon(
                          onPressed: _isProcessing
                              ? null
                              : () => _pickImage(ImageSource.camera),
                          icon: const Icon(Icons.camera_alt),
                          label: Text(_imageFile == null ? 'Take Photo' : 'Retake'),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: const Color(0xFF0F62FE), // --accent-primary
                            foregroundColor: Colors.white,
                            padding: const EdgeInsets.symmetric(vertical: 14),
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(4), // rounded-sm
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: OutlinedButton.icon(
                          onPressed: _isProcessing
                              ? null
                              : () => _pickImage(ImageSource.gallery),
                          icon: const Icon(Icons.photo_library),
                          label: const Text('Gallery'),
                          style: OutlinedButton.styleFrom(
                            foregroundColor: const Color(0xFF0F62FE),
                            side: const BorderSide(color: Color(0xFF0F62FE)),
                            padding: const EdgeInsets.symmetric(vertical: 14),
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(4), // rounded-sm
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                  if (_imageFile != null) ...[
                    const SizedBox(height: 12),
                    SizedBox(
                      width: double.infinity,
                      child: ElevatedButton.icon(
                        onPressed: _confirmPhoto,
                        icon: const Icon(Icons.check_circle),
                        label: const Text('Confirm & Attach Photo'),
                        style: ElevatedButton.styleFrom(
                          backgroundColor: const Color(0xFF198038), // --state-success
                          foregroundColor: Colors.white,
                          padding: const EdgeInsets.symmetric(vertical: 14),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(4), // rounded-sm
                          ),
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
