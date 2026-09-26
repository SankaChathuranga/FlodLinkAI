import 'dart:convert';

import 'package:http/http.dart' as http;

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

/// Minimal JSON client for Member D's dispatch endpoints.
/// Uses package:http so it works on Chrome (web), desktop and mobile.
class DispatchApi {
  const DispatchApi(this.baseUrl);

  final String baseUrl;

  Future<Map<String, dynamic>> _send(
    String method,
    String path, [
    Object? body,
  ]) async {
    final uri = Uri.parse('$baseUrl$path');
    final headers = {'Content-Type': 'application/json'};

    final http.Response response = method == 'GET'
        ? await http.get(uri, headers: headers)
        : await http.post(uri, headers: headers, body: jsonEncode(body));

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