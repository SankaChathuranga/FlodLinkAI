import 'dart:io';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import '../providers/report_provider.dart';
import '../models/shelter.dart';

class NewReportScreen extends StatefulWidget {
  const NewReportScreen({super.key});

  @override
  State<NewReportScreen> createState() => _NewReportScreenState();
}

class _NewReportScreenState extends State<NewReportScreen> {
  final _formKey = GlobalKey<FormState>();

  int? _selectedShelterId;
  String _selectedNeedType = 'Water';
  final TextEditingController _quantityController = TextEditingController(text: '100');
  final TextEditingController _notesController = TextEditingController();

  final List<String> _needTypes = [
    'Water',
    'Food',
    'Medical',
    'Shelter-Repair',
    'Other'
  ];

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final provider = context.read<ReportProvider>();
      provider.fetchShelters().then((_) {
        if (provider.shelters.isNotEmpty) {
          setState(() {
            _selectedShelterId = provider.shelters.first.id;
          });
        }
      });
    });
  }

  @override
  void dispose() {
    _quantityController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _handleSubmit() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    if (_selectedShelterId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please select a target shelter.')),
      );
      return;
    }

    final provider = context.read<ReportProvider>();
    final int quantity = int.parse(_quantityController.text.trim());

    final bool success = await provider.submitReport(
      shelterId: _selectedShelterId!,
      needType: _selectedNeedType,
      quantityNeeded: quantity,
      notes: _notesController.text.trim(),
    );

    if (mounted) {
      if (success) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Field report submitted successfully!'),
            backgroundColor: Colors.green,
          ),
        );
        context.pop();
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(provider.submitError ?? 'Submission failed.'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final reportProvider = context.watch<ReportProvider>();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Submit Field Intake Report'),
        backgroundColor: Theme.of(context).colorScheme.primary,
        foregroundColor: Colors.white,
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(16.0),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // SUBMIT / SERVER ERROR STATE BANNER
                if (reportProvider.submitError != null) ...[
                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: Colors.red.shade50,
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(color: Colors.red.shade200),
                    ),
                    child: Row(
                      children: [
                        const Icon(Icons.error_outline, color: Colors.red),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            reportProvider.submitError!,
                            style: const TextStyle(color: Colors.red, fontSize: 13),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),
                ],

                // 1. TARGET SHELTER SELECTION (LOADING / EMPTY / ERROR STATES)
                const Text(
                  '1. Select Target Shelter *',
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                ),
                const SizedBox(height: 6),
                if (reportProvider.isLoadingShelters)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 12),
                    child: Row(
                      children: [
                        SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        ),
                        SizedBox(width: 12),
                        Text('Loading shelter list...'),
                      ],
                    ),
                  )
                else if (reportProvider.sheltersError != null)
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: Colors.amber.shade50,
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Row(
                      children: [
                        const Icon(Icons.warning, color: Colors.amber),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            reportProvider.sheltersError!,
                            style: const TextStyle(fontSize: 12),
                          ),
                        ),
                        TextButton(
                          onPressed: () => reportProvider.fetchShelters(),
                          child: const Text('Retry'),
                        ),
                      ],
                    ),
                  )
                else if (reportProvider.shelters.isEmpty)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text(
                      'No shelters available. Using default shelter #1.',
                      style: TextStyle(color: Colors.grey, fontSize: 13),
                    ),
                  )
                else
                  DropdownButtonFormField<int>(
                    value: _selectedShelterId,
                    decoration: const InputDecoration(
                      border: OutlineInputBorder(),
                      contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                    ),
                    items: reportProvider.shelters.map((Shelter shelter) {
                      return DropdownMenuItem<int>(
                        value: shelter.id,
                        child: Text(
                          '${shelter.name} (${shelter.currentOccupancy}/${shelter.capacity})',
                          overflow: TextOverflow.ellipsis,
                        ),
                      );
                    }).toList(),
                    onChanged: (val) {
                      setState(() {
                        _selectedShelterId = val;
                      });
                    },
                    validator: (val) => val == null ? 'Shelter is required' : null,
                  ),

                const SizedBox(height: 16),

                // 2. NEED TYPE DROPDOWN
                const Text(
                  '2. Need Type *',
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                ),
                const SizedBox(height: 6),
                DropdownButtonFormField<String>(
                  value: _selectedNeedType,
                  decoration: const InputDecoration(
                    border: OutlineInputBorder(),
                    contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                  ),
                  items: _needTypes.map((type) {
                    return DropdownMenuItem<String>(
                      value: type,
                      child: Text(type),
                    );
                  }).toList(),
                  onChanged: (val) {
                    if (val != null) {
                      setState(() {
                        _selectedNeedType = val;
                      });
                    }
                  },
                ),

                const SizedBox(height: 16),

                // 3. QUANTITY NEEDED INPUT
                const Text(
                  '3. Quantity Required *',
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                ),
                const SizedBox(height: 6),
                TextFormField(
                  controller: _quantityController,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(
                    border: OutlineInputBorder(),
                    hintText: 'e.g. 100',
                    contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                  ),
                  validator: (val) {
                    if (val == null || val.trim().isEmpty) {
                      return 'Quantity required is mandatory';
                    }
                    final n = int.tryParse(val.trim());
                    if (n == null || n <= 0) {
                      return 'Must be a positive integer greater than 0';
                    }
                    return null;
                  },
                ),

                const SizedBox(height: 16),

                // 4. NOTES / DETAILS
                const Text(
                  '4. Additional Notes & Justification',
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                ),
                const SizedBox(height: 6),
                TextFormField(
                  controller: _notesController,
                  maxLines: 3,
                  decoration: const InputDecoration(
                    border: OutlineInputBorder(),
                    hintText: 'Provide specific field observations...',
                  ),
                ),

                const SizedBox(height: 20),

                // 5. CAMERA PHOTO EVIDENCE PREVIEW & BUTTON
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(12.0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.between,
                          children: [
                            const Text(
                              'Photo Evidence',
                              style: TextStyle(fontWeight: FontWeight.bold),
                            ),
                            TextButton.icon(
                              onPressed: () => context.push('/camera'),
                              icon: const Icon(Icons.camera_alt, size: 18),
                              label: Text(reportProvider.capturedPhotoPath == null ? 'Capture' : 'Change'),
                            ),
                          ],
                        ),
                        if (reportProvider.capturedPhotoPath != null) ...[
                          const SizedBox(height: 8),
                          ClipRRect(
                            borderRadius: BorderRadius.circular(8),
                            child: Image.file(
                              File(reportProvider.capturedPhotoPath!),
                              height: 140,
                              width: double.infinity,
                              fit: BoxFit.cover,
                            ),
                          ),
                        ] else
                          const Text(
                            'No photo captured yet (Optional).',
                            style: TextStyle(color: Colors.grey, fontSize: 12),
                          ),
                      ],
                    ),
                  ),
                ),

                const SizedBox(height: 12),

                // 6. GPS LOCATION PREVIEW & BUTTON
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(12.0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.between,
                          children: [
                            const Text(
                              'GPS Coordinates',
                              style: TextStyle(fontWeight: FontWeight.bold),
                            ),
                            TextButton.icon(
                              onPressed: () => context.push('/gps'),
                              icon: const Icon(Icons.my_location, size: 18),
                              label: Text(reportProvider.selectedLat == null ? 'Set Location' : 'Adjust Pin'),
                            ),
                          ],
                        ),
                        if (reportProvider.selectedLat != null)
                          Text(
                            'Lat: ${reportProvider.selectedLat!.toStringAsFixed(5)}, Lng: ${reportProvider.selectedLng!.toStringAsFixed(5)}',
                            style: const TextStyle(fontFamily: 'monospace', fontSize: 12, color: Colors.blueAccent),
                          )
                        else
                          const Text(
                            'Defaulting to shelter GPS coordinates.',
                            style: TextStyle(color: Colors.grey, fontSize: 12),
                          ),
                      ],
                    ),
                  ),
                ),

                const SizedBox(height: 24),

                // SUBMIT ACTION BUTTON
                SizedBox(
                  width: double.infinity,
                  height: 50,
                  child: ElevatedButton(
                    onPressed: reportProvider.isSubmitting ? null : _handleSubmit,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Theme.of(context).colorScheme.primary,
                      foregroundColor: Colors.white,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(8),
                      ),
                    ),
                    child: reportProvider.isSubmitting
                        ? const CircularProgressIndicator(color: Colors.white)
                        : const Text(
                            'Submit Field Report',
                            style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                          ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
