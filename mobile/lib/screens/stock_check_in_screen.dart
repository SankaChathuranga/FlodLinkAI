<<<<<<< HEAD
import 'package:flutter/material.dart';

class StockCheckInScreen extends StatelessWidget {
  const StockCheckInScreen({super.key});

  @override
=======
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:provider/provider.dart';

import '../main.dart'; 

class StockCheckInScreen extends StatefulWidget {
  const StockCheckInScreen({super.key});

  @override
  State<StockCheckInScreen> createState() => _StockCheckInScreenState();
}

class _StockCheckInScreenState extends State<StockCheckInScreen> {
  final _formKey = GlobalKey<FormState>();
  final _itemNameController = TextEditingController();
  final _quantityController = TextEditingController();
  final _unitController = TextEditingController();
  
  int? _selectedDepotId;
  bool _isLoading = false;

  final Map<int, String> _depots = {1: 'Central Warehouse', 2: 'North Depot', 3: 'South Depot'};

  @override
  void dispose() {
    _itemNameController.dispose();
    _quantityController.dispose();
    _unitController.dispose();
    super.dispose();
  }

  Future<void> _submitForm() async {
    if (!_formKey.currentState!.validate()) return;
    if (_selectedDepotId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please select a depot')),
      );
      return;
    }

    setState(() => _isLoading = true);

    try {
      final appState = context.read<AppState>();
      final url = Uri.parse('${appState.apiBaseUrl}/api/inventory');
      
      final response = await http.post(
        url,
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'depotId': _selectedDepotId,
          'itemName': _itemNameController.text.trim(),
          'quantityAvailable': double.parse(_quantityController.text.trim()),
          'quantityReserved': 0,
          'unit': _unitController.text.trim(),
        }),
      );

      if (response.statusCode >= 200 && response.statusCode < 300) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Stock checked in successfully!')),
          );
          _formKey.currentState!.reset();
          setState(() => _selectedDepotId = null);
        }
      } else {
        throw Exception('Failed to check in stock: ${response.statusCode}');
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Error: $e')),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _isLoading = false);
      }
    }
  }

  @override
>>>>>>> 6c8d2ece674506c5f8db44076b4aeabe57cf9f87
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Stock Check-In'),
<<<<<<< HEAD
      ),
      body: const SafeArea(
        child: Padding(
          padding: EdgeInsets.all(24.0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Stock Check-In',
                style: TextStyle(fontSize: 28, fontWeight: FontWeight.bold),
              ),
              SizedBox(height: 16),
              Text(
                'This screen was missing from the project and has been restored as a placeholder while the feature is developed.',
                style: TextStyle(fontSize: 16),
              ),
              SizedBox(height: 32),
              Card(
                child: Padding(
                  padding: EdgeInsets.all(16.0),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Feature status',
                        style: TextStyle(fontWeight: FontWeight.bold),
                      ),
                      SizedBox(height: 8),
                      Text('Ready for the stock intake form and validation workflow.'),
                    ],
                  ),
=======
        backgroundColor: Theme.of(context).colorScheme.primary,
        foregroundColor: Colors.white,
      ),
      body: Padding(
        padding: const EdgeInsets.all(16.0),
        child: Form(
          key: _formKey,
          child: ListView(
            children: [
              DropdownButtonFormField<int>(
                decoration: const InputDecoration(
                  labelText: 'Select Depot',
                  border: OutlineInputBorder(),
                ),
                initialValue: _selectedDepotId,
                items: _depots.entries.map((entry) {
                  return DropdownMenuItem<int>(
                    value: entry.key,
                    child: Text(entry.value),
                  );
                }).toList(),
                onChanged: (value) => setState(() => _selectedDepotId = value),
                validator: (value) => value == null ? 'Depot is required' : null,
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _itemNameController,
                decoration: const InputDecoration(
                  labelText: 'Item Name',
                  border: OutlineInputBorder(),
                ),
                validator: (value) {
                  if (value == null || value.trim().isEmpty) {
                    return 'Item name is required';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _quantityController,
                decoration: const InputDecoration(
                  labelText: 'Quantity',
                  border: OutlineInputBorder(),
                ),
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                validator: (value) {
                  if (value == null || value.trim().isEmpty) {
                    return 'Quantity is required';
                  }
                  final quantity = double.tryParse(value);
                  if (quantity == null || quantity <= 0) {
                    return 'Please enter a valid positive number';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _unitController,
                decoration: const InputDecoration(
                  labelText: 'Unit (e.g., kg, boxes)',
                  border: OutlineInputBorder(),
                ),
                validator: (value) {
                  if (value == null || value.trim().isEmpty) {
                    return 'Unit is required';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 32),
              SizedBox(
                height: 50,
                child: ElevatedButton(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Theme.of(context).colorScheme.primary,
                    foregroundColor: Colors.white,
                  ),
                  onPressed: _isLoading ? null : _submitForm,
                  child: _isLoading
                      ? const CircularProgressIndicator(color: Colors.white)
                      : const Text(
                          'Submit Check-In',
                          style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                        ),
>>>>>>> 6c8d2ece674506c5f8db44076b4aeabe57cf9f87
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
