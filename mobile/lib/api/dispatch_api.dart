import 'dart:convert';

import 'package:http/http.dart' as http;

import 'api_client.dart';
import 'models.dart';

/// Error raised when the backend responds with a non-2xx status.
/// Carries the API's { error, message } body so screens can surface it as-is.
class ApiException implements Exception {
  const ApiException(this.statusCode, this.code, this.message);

  final int statusCode;
  final String code;
  final String message;

  @override
  String toString() => 'ApiException $statusCode $code - $message';
}

/// Minimal JSON client for Member D's dispatch endpoints, sent through [ApiClient]
/// so every call carries the signed-in user's token.
class DispatchApi {
  const DispatchApi(this._api);

  final ApiClient _api;

  Future<Map<String, dynamic>> _send(
    String method,
    String path, [
    Object? body,
  ]) async {
    final http.Response response =
        method == 'GET' ? await _api.get(path) : await _api.postJson(path, body);

    final decoded = response.body.isEmpty
        ? <String, dynamic>{}
        : jsonDecode(response.body) as Map<String, dynamic>;

    if (response.statusCode >= 200 && response.statusCode < 300) {
      return decoded;
    }

    throw ApiException(
      response.statusCode,
      decoded['error'] as String? ?? 'REQUEST_FAILED',
      decoded['message'] as String? ?? 'HTTP ${response.statusCode}',
    );
  }

  /// Current dispatch status for a workflow run (decision, reason, state).
  Future<DispatchStatus> fetchStatus(String workflowRunId) async {
    final json = await _send('GET', '/api/dispatches/by-workflow/$workflowRunId');
    return DispatchStatus.fromJson(json);
  }

  /// Uploads a photo proving delivery of an Approved run and returns its URL path
  /// (e.g. `/uploads/deliveries/….jpg`), to pass as `photoReference` to [confirmDelivery].
  Future<String> uploadDeliveryPhoto(String workflowRunId, List<int> bytes, String fileName) async {
    final request = http.MultipartRequest('POST', _api.uri('/api/dispatches/$workflowRunId/delivery-photo'))
      ..files.add(http.MultipartFile.fromBytes('photo', bytes, filename: fileName));
    final response = await _api.sendMultipart(request);
    final decoded = response.body.isEmpty ? <String, dynamic>{} : jsonDecode(response.body) as Map<String, dynamic>;
    if (response.statusCode == 201 || response.statusCode == 200) {
      return decoded['photoUrl'] as String;
    }
    throw ApiException(
      response.statusCode,
      decoded['error'] as String? ?? 'REQUEST_FAILED',
      decoded['message'] as String? ?? 'Photo upload failed (HTTP ${response.statusCode}).',
    );
  }

  /// Records a field-worker delivery confirmation (audit-flow, see backend).
  Future<DeliveryResult> confirmDelivery(
    String workflowRunId, {
    String? notes,
    String? photoReference,
  }) async {
    final json = await _send('POST',
        '/api/dispatches/$workflowRunId/confirm-delivery', {
      'notes': notes,
      'photoReference': photoReference,
    });
    return DeliveryResult.fromJson(json);
  }
}