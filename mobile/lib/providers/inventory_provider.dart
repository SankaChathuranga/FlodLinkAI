import 'dart:convert';

import 'package:flutter/material.dart';
import '../api/api_client.dart';
import '../models/inventory.dart';

/// Depots and stock for the depot manager's check-in screen, through [ApiClient].
class InventoryProvider extends ChangeNotifier {
  InventoryProvider(this._api);

  final ApiClient _api;

  List<Depot> _depots = [];
  List<InventoryItem> _lowStock = [];
  bool _isLoadingLowStock = false;
  String? _lowStockError;
  List<InventoryItem> _items = [];
  bool _isLoadingDepots = false;
  bool _isLoadingItems = false;
  bool _isSubmitting = false;
  String? _error;

  List<Depot> get depots => _depots;
  List<InventoryItem> get lowStock => _lowStock;
  bool get isLoadingLowStock => _isLoadingLowStock;
  String? get lowStockError => _lowStockError;

  /// Low-stock lines of the depots the signed-in manager runs, least free stock first.
  Future<void> fetchLowStock() async {
    _isLoadingLowStock = true;
    _lowStockError = null;
    notifyListeners();
    try {
      final response =
          await _api.get('/api/inventory?managedByMe=true&lowStock=true&sort=quantity_asc&pageSize=100');
      if (response.statusCode != 200) {
        _lowStockError = 'Failed to load low stock (HTTP ${response.statusCode}).';
        return;
      }
      _lowStock = _pageItems(response.body)
          .map((item) => InventoryItem.fromJson(item as Map<String, dynamic>))
          .toList();
    } catch (_) {
      _lowStockError = 'No internet connection. Please try again.';
    } finally {
      _isLoadingLowStock = false;
      notifyListeners();
    }
  }
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

  // ── Scanned stock labels (8.06) ───────────────────────────────────────────

  /// One stock line by id (from a scanned label), or null with [error] set when it can't be found.
  Future<InventoryItem?> fetchItem(int id) async {
    _error = null;
    try {
      final response = await _api.get('/api/inventory/$id');
      if (response.statusCode == 200) {
        return InventoryItem.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
      }
      _error = response.statusCode == 404
          ? 'This label is for a stock line that no longer exists.'
          : 'Could not look up the label (HTTP ${response.statusCode}).';
    } catch (_) {
      _error = 'No internet connection. Please try again.';
    }
    notifyListeners();
    return null;
  }

  // ── Reserved stock to check (8.05) ────────────────────────────────────────

  List<ReservedLine> _reservations = [];
  bool _isLoadingReservations = false;
  String? _reservationsError;

  List<ReservedLine> get reservations => _reservations;
  bool get isLoadingReservations => _isLoadingReservations;
  String? get reservationsError => _reservationsError;

  /// Reserved lines at the depots the signed-in manager runs, waiting for a coordinator's decision.
  Future<void> fetchReservations() async {
    _isLoadingReservations = true;
    _reservationsError = null;
    notifyListeners();
    try {
      final response = await _api.get('/api/allocations?status=Reserved&managedByMe=true&pageSize=100');
      if (response.statusCode != 200) {
        _reservationsError = 'Failed to load reserved stock (HTTP ${response.statusCode}).';
        return;
      }
      _reservations = _pageItems(response.body)
          .map((item) => ReservedLine.fromJson(item as Map<String, dynamic>))
          .toList();
    } catch (_) {
      _reservationsError = 'No internet connection. Please try again.';
    } finally {
      _isLoadingReservations = false;
      notifyListeners();
    }
  }

  /// Records what was counted on the shelf for a reserved line. Returns null on success, else the reason.
  Future<String?> confirmReservation(int allocationId, double countedQuantity, {String? note}) async {
    try {
      final response = await _api.postJson('/api/allocations/$allocationId/confirm-stock', {
        'countedQuantity': countedQuantity,
        'note': (note == null || note.trim().isEmpty) ? null : note.trim(),
      });
      if (response.statusCode != 200) {
        final body = response.body.isEmpty ? null : jsonDecode(response.body);
        return (body is Map<String, dynamic> ? body['message'] as String? : null) ??
            'Could not save the count (HTTP ${response.statusCode}).';
      }
      final updated = ReservedLine.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
      _reservations = [for (final r in _reservations) r.id == allocationId ? updated : r];
      notifyListeners();
      return null;
    } catch (_) {
      return 'No internet connection. Please try again.';
    }
  }

  /// List endpoints return a page: `{ items, page, pageSize, totalItems, totalPages }`.
  static List<dynamic> _pageItems(String body) =>
      (jsonDecode(body) as Map<String, dynamic>)['items'] as List<dynamic>;
}
