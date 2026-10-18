import 'dart:async';
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import '../api/api_client.dart';
import '../models/shelter.dart';
import '../models/report.dart';
import '../offline/report_queue.dart';

/// What happened to a submitted report.
enum SubmitOutcome {
  /// The server saved it.
  sent,

  /// No connection: it is saved on the phone and will be sent automatically (8.09).
  queued,

  /// The server refused it (see [ReportProvider.submitError]).
  failed,
}

/// Shelters, the signed-in volunteer's own reports, and report submission — including the offline queue
/// of reports that could not be sent yet. All calls go through [ApiClient], which adds the bearer token.
class ReportProvider extends ChangeNotifier {
  ReportProvider(this._api,
      {ReportQueue? queue, this.retryEvery = const Duration(minutes: 1)})
      : _queue = queue ?? ReportQueue.memory() {
    _loadQueue();
  }

  final ApiClient _api;
  final ReportQueue _queue;

  /// How often waiting reports are retried (only while there are any).
  final Duration retryEvery;
  Timer? _retryTimer;
  List<QueuedReport> _queued = [];
  bool _isSendingQueue = false;

  /// Reports saved on the phone because there was no connection, oldest first.
  List<QueuedReport> get queuedReports => _queued;
  bool get isSendingQueue => _isSendingQueue;

  bool get _hasWaiting => _queued.any((r) => !r.refused);

  Future<void> _loadQueue() async {
    _queued = await _queue.load();
    _syncRetryTimer();
    notifyListeners();
    if (_hasWaiting) await sendQueuedReports();
  }

  // The retry timer runs only while reports are waiting to be sent.
  void _syncRetryTimer() {
    if (_hasWaiting && _retryTimer == null) {
      _retryTimer = Timer.periodic(retryEvery, (_) => sendQueuedReports());
    } else if (!_hasWaiting) {
      _retryTimer?.cancel();
      _retryTimer = null;
    }
  }

  @override
  void dispose() {
    _retryTimer?.cancel();
    super.dispose();
  }

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
        _sheltersError =
            'Failed to load shelters (HTTP ${response.statusCode})';
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
        // We're online again: send anything that was waiting.
        if (_hasWaiting && !_isSendingQueue) unawaited(sendQueuedReports());
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

  /// Sends a new report. Without a connection the report is kept on the phone and sent later
  /// ([SubmitOutcome.queued]); the captured photo and position go with it.
  Future<SubmitOutcome> submitReport({
    required int shelterId,
    required String needType,
    required int quantityNeeded,
    required String notes,
  }) async {
    _isSubmitting = true;
    _submitError = null;
    notifyListeners();

    final report = QueuedReport(
      clientReference: newClientReference(),
      shelterId: shelterId,
      needType: needType,
      quantityNeeded: quantityNeeded,
      notes: notes,
      gpsLat: _selectedLat,
      gpsLng: _selectedLng,
      photoPath: _capturedPhotoPath,
      queuedAt: DateTime.now(),
    );

    try {
      final response = await _send(report);
      if (response.statusCode == 201 || response.statusCode == 200) {
        clearCapturedData();
        await fetchReports();
        return SubmitOutcome.sent;
      }
      _submitError = _describeRefusal(response);
      return SubmitOutcome.failed;
    } on TimeoutException {
      return _keepForLater(report);
    } on http.ClientException {
      return _keepForLater(report);
    } catch (e) {
      _submitError = 'Failed to submit report: $e';
      return SubmitOutcome.failed;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }

  Future<SubmitOutcome> _keepForLater(QueuedReport report) async {
    await _queue.add(report);
    _queued = _queue.items;
    _syncRetryTimer();
    clearCapturedData();
    return SubmitOutcome.queued;
  }

  /// Tries to send every waiting report, oldest first. Stops at the first connection failure; a report
  /// the server refuses is kept with the reason (it won't succeed by retrying) until [discardQueuedReport].
  Future<void> sendQueuedReports() async {
    if (_isSendingQueue) return;
    _isSendingQueue = true;
    notifyListeners();
    var sentAny = false;
    try {
      for (final report in (await _queue.load()).where((r) => !r.refused)) {
        final http.Response response;
        try {
          response = await _send(report);
        } on TimeoutException {
          break;
        } on http.ClientException {
          break;
        }
        if (response.statusCode == 201 || response.statusCode == 200) {
          await _queue.remove(report
              .clientReference); // 200: it had arrived before; not saved twice
          sentAny = true;
        } else if (response.statusCode == 401 || response.statusCode >= 500) {
          break; // signed out or server trouble: try again later
        } else {
          await _queue.replace(report.withError(_describeRefusal(response)));
        }
      }
    } finally {
      _queued = _queue.items;
      _isSendingQueue = false;
      _syncRetryTimer();
      notifyListeners();
    }
    if (sentAny) await fetchReports();
  }

  /// Drops a waiting report the user no longer wants to send.
  Future<void> discardQueuedReport(String clientReference) async {
    await _queue.remove(clientReference);
    _queued = _queue.items;
    _syncRetryTimer();
    notifyListeners();
  }

  Future<http.Response> _send(QueuedReport report) async {
    final request = http.MultipartRequest('POST', _api.uri('/api/reports'));
    request.fields['ClientReference'] = report.clientReference;
    request.fields['ShelterId'] = report.shelterId.toString();
    request.fields['NeedType'] = report.needType;
    request.fields['QuantityNeeded'] = report.quantityNeeded.toString();
    request.fields['Status'] = 'New';
    if (report.notes.isNotEmpty) request.fields['Notes'] = report.notes;
    if (report.gpsLat != null) {
      request.fields['GpsLat'] = report.gpsLat.toString();
    }
    if (report.gpsLng != null) {
      request.fields['GpsLng'] = report.gpsLng.toString();
    }

    final photo = report.photoPath;
    if (photo != null && photo.isNotEmpty) {
      try {
        request.files.add(await http.MultipartFile.fromPath('Photo', photo));
      } catch (_) {
        // The photo file is gone (e.g. cleared from the cache while queued): send the report without it.
      }
    }

    // The server takes the reporter from the token, never from the form.
    return _api.sendMultipart(request);
  }

  static String _describeRefusal(http.Response response) {
    try {
      final body = jsonDecode(response.body);
      if (body is Map<String, dynamic>) {
        final errors = body['errors'];
        if (errors is Map<String, dynamic> && errors.isNotEmpty) {
          return errors.values.expand((v) => v is List ? v : [v]).join(' ');
        }
        final message = body['message'] ?? body['title'];
        if (message is String) return message;
      }
    } catch (_) {}
    return 'Server returned HTTP ${response.statusCode}.';
  }
}
