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
      backgroundColor: const Color(0xFFF4F4F4), // --bg-base
      appBar: AppBar(
        title: const Text('Submit Field Intake Report'),
        backgroundColor: const Color(0xFF0F62FE), // --accent-primary
        foregroundColor: Colors.white,
        elevation: 0,
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
                      color: const Color(0xFFDA1E28).withValues(alpha: 0.1), // --state-error
                      borderRadius: BorderRadius.circular(4), // rounded-sm
                      border: Border.all(color: const Color(0xFFDA1E28)),
                    ),
                    child: Row(
                      children: [
                        const Icon(Icons.error_outline, color: Color(0xFFDA1E28)),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            reportProvider.submitError!,
                            style: const TextStyle(color: Color(0xFFDA1E28), fontSize: 13),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),
                ],

                // 1. TARGET SHELTER SELECTION
                const Text(
                  '1. Select Target Shelter *',
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: Color(0xFF161616)),
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
                          child: CircularProgressIndicator(strokeWidth: 2, color: Color(0xFF0F62FE)),
                        ),
                        SizedBox(width: 12),
                        Text('Loading shelter list...', style: TextStyle(color: Color(0xFF6F6F6F))),
                      ],
                    ),
                  )
                else if (reportProvider.sheltersError != null)
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: const Color(0xFFF1C21B).withValues(alpha: 0.15), // --state-warning
                      borderRadius: BorderRadius.circular(4),
                      border: Border.all(color: const Color(0xFFF1C21B)),
                    ),
                    child: Row(
                      children: [
                        const Icon(Icons.warning, color: Color(0xFF161616)),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            reportProvider.sheltersError!,
                            style: const TextStyle(fontSize: 12, color: Color(0xFF161616)), // Warning contrast rule
                          ),
                        ),
                        TextButton(
                          onPressed: () => reportProvider.fetchShelters(),
                          child: const Text('Retry', style: TextStyle(color: Color(0xFF0F62FE))),
                        ),
                      ],
                    ),
                  )
                else if (reportProvider.shelters.isEmpty)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text(
                      'No shelters available. Using default shelter #1.',
                      style: TextStyle(color: Color(0xFF6F6F6F), fontSize: 13),
                    ),
                  )
                else
                  DropdownButtonFormField<int>(
                    key: ValueKey(_selectedShelterId),
                    initialValue: _selectedShelterId,
                    decoration: const InputDecoration(
                      fillColor: Color(0xFFFFFFFF),
                      filled: true,
                      border: OutlineInputBorder(
                        borderSide: BorderSide(color: Color(0xFFC6C6C6)),
                      ),
                      enabledBorder: OutlineInputBorder(
                        borderSide: BorderSide(color: Color(0xFFC6C6C6)),
                      ),
                      contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                    ),
                    items: reportProvider.shelters.map((Shelter shelter) {
                      return DropdownMenuItem<int>(
                        value: shelter.id,
                        child: Text(
                          '${shelter.name} (${shelter.currentOccupancy}/${shelter.capacity})',
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(color: Color(0xFF161616)),
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
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: Color(0xFF161616)),
                ),
                const SizedBox(height: 6),
                DropdownButtonFormField<String>(
                  key: ValueKey(_selectedNeedType),
                  initialValue: _selectedNeedType,
                  decoration: const InputDecoration(
                    fillColor: Color(0xFFFFFFFF),
                    filled: true,
                    border: OutlineInputBorder(
                      borderSide: BorderSide(color: Color(0xFFC6C6C6)),
                    ),
                    enabledBorder: OutlineInputBorder(
                      borderSide: BorderSide(color: Color(0xFFC6C6C6)),
                    ),
                    contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                  ),
                  items: _needTypes.map((type) {
                    return DropdownMenuItem<String>(
                      value: type,
                      child: Text(type, style: const TextStyle(color: Color(0xFF161616))),
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
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: Color(0xFF161616)),
                ),
                const SizedBox(height: 6),
                TextFormField(
                  controller: _quantityController,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(
                    fillColor: Color(0xFFFFFFFF),
                    filled: true,
                    border: OutlineInputBorder(
                      borderSide: BorderSide(color: Color(0xFFC6C6C6)),
                    ),
                    enabledBorder: OutlineInputBorder(
                      borderSide: BorderSide(color: Color(0xFFC6C6C6)),
                    ),
                    hintText: 'e.g. 100',
                    contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                  ),
                  style: const TextStyle(color: Color(0xFF161616)),
                  validator: (val) {
                    if (val == null || val.trim().isEmpty) {
                      return 'Please enter quantity needed.';
                    }
                    final n = int.tryParse(val.trim());
                    if (n == null || n <= 0) {
                      return 'Quantity must be greater than 0.';
                    }
                    return null;
                  },
                ),

                const SizedBox(height: 16),

                // 4. NOTES / DETAILS
                const Text(
                  '4. Additional Notes & Justification',
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: Color(0xFF161616)),
                ),
                const SizedBox(height: 6),
                TextFormField(
                  controller: _notesController,
                  maxLines: 3,
                  decoration: const InputDecoration(
                    fillColor: Color(0xFFFFFFFF),
                    filled: true,
                    border: OutlineInputBorder(
                      borderSide: BorderSide(color: Color(0xFFC6C6C6)),
                    ),
                    enabledBorder: OutlineInputBorder(
                      borderSide: BorderSide(color: Color(0xFFC6C6C6)),
                    ),
                    hintText: 'Provide specific field observations...',
                  ),
                  style: const TextStyle(color: Color(0xFF161616)),
                ),

                const SizedBox(height: 20),

                // 5. CAMERA PHOTO EVIDENCE PREVIEW & BUTTON
                Container(
                  decoration: BoxDecoration(
                    color: const Color(0xFFFFFFFF), // --bg-surface
                    borderRadius: BorderRadius.circular(6), // rounded-md
                    border: Border.all(color: const Color(0xFFC6C6C6)),
                  ),
                  padding: const EdgeInsets.all(12.0),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          const Text(
                            'Photo Evidence',
                            style: TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF161616)),
                          ),
                          TextButton.icon(
                            onPressed: () => context.push('/camera'),
                            icon: const Icon(Icons.camera_alt, size: 18, color: Color(0xFF0F62FE)),
                            label: Text(
                              reportProvider.capturedPhotoPath == null ? 'Capture' : 'Change',
                              style: const TextStyle(color: Color(0xFF0F62FE)),
                            ),
                          ),
                        ],
                      ),
                      if (reportProvider.capturedPhotoPath != null) ...[
                        const SizedBox(height: 8),
                        ClipRRect(
                          borderRadius: BorderRadius.circular(4),
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
                          style: TextStyle(color: Color(0xFF6F6F6F), fontSize: 12),
                        ),
                    ],
                  ),
                ),

                const SizedBox(height: 12),

                // 6. GPS LOCATION PREVIEW & BUTTON
                Container(
                  decoration: BoxDecoration(
                    color: const Color(0xFFFFFFFF), // --bg-surface
                    borderRadius: BorderRadius.circular(6), // rounded-md
                    border: Border.all(color: const Color(0xFFC6C6C6)),
                  ),
                  padding: const EdgeInsets.all(12.0),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          const Text(
                            'GPS Coordinates',
                            style: TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF161616)),
                          ),
                          TextButton.icon(
                            onPressed: () => context.push('/gps'),
                            icon: const Icon(Icons.my_location, size: 18, color: Color(0xFF0F62FE)),
                            label: Text(
                              reportProvider.selectedLat == null ? 'Set Location' : 'Adjust Pin',
                              style: const TextStyle(color: Color(0xFF0F62FE)),
                            ),
                          ),
                        ],
                      ),
                      if (reportProvider.selectedLat != null)
                        Text(
                          'Lat: ${reportProvider.selectedLat!.toStringAsFixed(5)}, Lng: ${reportProvider.selectedLng!.toStringAsFixed(5)}',
                          style: const TextStyle(
                            fontFamily: 'IBM Plex Mono', // --font-mono
                            fontSize: 12,
                            color: Color(0xFF0F62FE),
                          ),
                        )
                      else
                        const Text(
                          'Defaulting to shelter GPS coordinates.',
                          style: TextStyle(color: Color(0xFF6F6F6F), fontSize: 12),
                        ),
                    ],
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
                      backgroundColor: const Color(0xFF0F62FE), // --accent-primary
                      foregroundColor: Colors.white,
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(4), // rounded-sm
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
