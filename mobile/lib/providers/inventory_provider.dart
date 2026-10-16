import 'dart:convert';

import 'package:flutter/material.dart';
import '../api/api_client.dart';
import '../models/inventory.dart';

/// Depots and stock for the depot manager's check-in screen, through [ApiClient].
class InventoryProvider extends ChangeNotifier {
  InventoryProvider(this._api);

  final ApiClient _api;

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
      final response = await _api.get('/api/depots?pageSize=100');
      if (response.statusCode != 200) {
        _error = response.statusCode == 403
            ? 'Your account cannot view depots.'
            : 'Failed to load depots (HTTP ${response.statusCode}).';
        return;
      }
      final data = _pageItems(response.body);
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
      final response = await _api.get('/api/inventory?depotId=$depotId&sort=item&pageSize=100');
      if (response.statusCode != 200) {
        _error = 'Failed to load stock (HTTP ${response.statusCode}).';
        return;
      }
      final data = _pageItems(response.body);
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
      final response = await _api.putJson(
        '/api/inventory/$itemId/check-in',
        {'quantityReceived': quantityReceived},
      );
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

  /// List endpoints return a page: `{ items, page, pageSize, totalItems, totalPages }`.
  static List<dynamic> _pageItems(String body) =>
      (jsonDecode(body) as Map<String, dynamic>)['items'] as List<dynamic>;
}
