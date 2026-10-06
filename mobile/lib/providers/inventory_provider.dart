import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../config/api_config.dart';
import '../models/inventory.dart';

class InventoryProvider extends ChangeNotifier {
  final String _apiBaseUrl = kApiBaseUrl;

  List<Depot> _depots = [];
  List<InventoryItem> _items = [];
  bool _isLoadingDepots = false;
  bool _isLoadingItems = false;
  bool _isSubmitting = false;
  String? _error;

  List<Depot> get depots => _depots;
  List<InventoryItem> get items => _items;
  bool get isLoadingDepots => _isLoadingDepots;
  bool get isLoadingItems => _isLoadingItems;
  bool get isSubmitting => _isSubmitting;
  String? get error => _error;

  Future<void> fetchDepots() async {
    _isLoadingDepots = true;
    _error = null;
    notifyListeners();
    try {
      final response = await http
          .get(Uri.parse('$_apiBaseUrl/api/depots'))
          .timeout(const Duration(seconds: 10));
      if (response.statusCode != 200) {
        _error = 'Failed to load depots (HTTP ${response.statusCode}).';
        return;
      }
      final data = jsonDecode(response.body) as List<dynamic>;
      _depots = data
          .map((item) => Depot.fromJson(item as Map<String, dynamic>))
          .toList();
    } catch (_) {
      _error = 'No internet connection. Please try again.';
    } finally {
      _isLoadingDepots = false;
      notifyListeners();
    }
  }

  Future<void> fetchItems(int depotId) async {
    _isLoadingItems = true;
    _items = [];
    _error = null;
    notifyListeners();
    try {
      final response = await http
          .get(Uri.parse('$_apiBaseUrl/api/inventory?depotId=$depotId'))
          .timeout(const Duration(seconds: 10));
      if (response.statusCode != 200) {
        _error = 'Failed to load stock (HTTP ${response.statusCode}).';
        return;
      }
      final data = jsonDecode(response.body) as List<dynamic>;
      _items = data
          .map((item) => InventoryItem.fromJson(item as Map<String, dynamic>))
          .toList();
    } catch (_) {
      _error = 'No internet connection. Please try again.';
    } finally {
      _isLoadingItems = false;
      notifyListeners();
    }
  }

  Future<bool> checkIn({required int itemId, required double quantityReceived}) async {
    _isSubmitting = true;
    _error = null;
    notifyListeners();
    try {
      final response = await http
          .put(
            Uri.parse('$_apiBaseUrl/api/inventory/$itemId/check-in'),
            headers: const {'Content-Type': 'application/json'},
            body: jsonEncode({'quantityReceived': quantityReceived}),
          )
          .timeout(const Duration(seconds: 10));
      if (response.statusCode != 200) {
        _error = 'Stock update failed (HTTP ${response.statusCode}).';
        return false;
      }
      return true;
    } catch (_) {
      _error = 'No internet connection. Please try again.';
      return false;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }
}
