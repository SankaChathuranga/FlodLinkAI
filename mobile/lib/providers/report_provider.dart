import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import '../api/api_client.dart';
import '../models/shelter.dart';
import '../models/report.dart';

/// Shelters, the signed-in volunteer's own reports, and report submission.
/// All calls go through [ApiClient], which adds the bearer token.
class ReportProvider extends ChangeNotifier {
  ReportProvider(this._api);

  final ApiClient _api;

  List<Shelter> _shelters = [];
  List<FieldReport> _reports = [];

  bool _isLoadingShelters = false;
  bool _isLoadingReports = false;
  bool _isSubmitting = false;

  String? _sheltersError;
  String? _reportsError;
  String? _submitError;

  // Selected evidence for new report creation
  String? _capturedPhotoPath;
  double? _selectedLat;
  double? _selectedLng;

  // Getters
  String get apiBaseUrl => _api.baseUrl;
  List<Shelter> get shelters => _shelters;
  List<FieldReport> get reports => _reports;

  bool get isLoadingShelters => _isLoadingShelters;
  bool get isLoadingReports => _isLoadingReports;
  bool get isSubmitting => _isSubmitting;

  String? get sheltersError => _sheltersError;
  String? get reportsError => _reportsError;
  String? get submitError => _submitError;

  String? get capturedPhotoPath => _capturedPhotoPath;
  double? get selectedLat => _selectedLat;
  double? get selectedLng => _selectedLng;

  void setCapturedPhoto(String? path) {
    _capturedPhotoPath = path;
    notifyListeners();
  }

  void setSelectedGps(double lat, double lng) {
    _selectedLat = lat;
    _selectedLng = lng;
    notifyListeners();
  }

  void clearCapturedData() {
    _capturedPhotoPath = null;
    _selectedLat = null;
    _selectedLng = null;
    notifyListeners();
  }

  Future<void> fetchShelters() async {
    _isLoadingShelters = true;
    _sheltersError = null;
    notifyListeners();

    try {
      final response = await _api.get('/api/shelters?pageSize=100');

      if (response.statusCode == 200) {
        final data = jsonDecode(response.body);
        final List items = data['items'] ?? [];
        _shelters = items.map((e) => Shelter.fromJson(e)).toList();
      } else {
        _sheltersError = 'Failed to load shelters (HTTP ${response.statusCode})';
      }
    } catch (e) {
      _sheltersError = 'Error connecting to backend server: $e';
    } finally {
      _isLoadingShelters = false;
      notifyListeners();
    }
  }

  Future<void> fetchReports() async {
    _isLoadingReports = true;
    _reportsError = null;
    notifyListeners();

    try {
      // Only the signed-in volunteer's reports, with their workflow stage.
      final response = await _api.get('/api/reports/mine');

      if (response.statusCode == 200) {
        final List data = jsonDecode(response.body);
        _reports = data.map((e) => FieldReport.fromJson(e)).toList();
      } else {
        _reportsError = 'Failed to load reports (HTTP ${response.statusCode})';
      }
    } catch (e) {
      _reportsError = 'Error connecting to backend server: $e';
    } finally {
      _isLoadingReports = false;
      notifyListeners();
    }
  }

  Future<bool> submitReport({
    required int shelterId,
    required String needType,
    required int quantityNeeded,
    required String notes,
  }) async {
    _isSubmitting = true;
    _submitError = null;
    notifyListeners();

    try {
      final request = http.MultipartRequest('POST', _api.uri('/api/reports'));

      request.fields['ShelterId'] = shelterId.toString();
      request.fields['NeedType'] = needType;
      request.fields['QuantityNeeded'] = quantityNeeded.toString();
      request.fields['Status'] = 'New';

      if (_selectedLat != null) {
        request.fields['GpsLat'] = _selectedLat.toString();
      }
      if (_selectedLng != null) {
        request.fields['GpsLng'] = _selectedLng.toString();
      }

      if (_capturedPhotoPath != null && _capturedPhotoPath!.isNotEmpty) {
        request.files.add(await http.MultipartFile.fromPath('Photo', _capturedPhotoPath!));
      }

      // The server takes the reporter from the token, never from the form.
      final response = await _api.sendMultipart(request);

      if (response.statusCode == 201 || response.statusCode == 200) {
        clearCapturedData();
        await fetchReports();
        return true;
      } else {
        _submitError = 'Server returned HTTP ${response.statusCode}: ${response.body}';
        return false;
      }
    } catch (e) {
      _submitError = 'Failed to submit report: $e';
      return false;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }
}
