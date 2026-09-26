import 'dart:convert';
import 'dart:io';

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
/// Uses dart:io HttpClient — no extra package dependencies.
class DispatchApi {
  const DispatchApi(this.baseUrl);

  final String baseUrl;

  Future<Map<String, dynamic>> _send(
    String method,
    String path, [
    Object? body,
  ]) async {
    final client = HttpClient();

    try {
      final uri = Uri.parse('$baseUrl$path');
      final request =
          method == 'GET' ? await client.getUrl(uri) : await client.postUrl(uri);
      request.headers.contentType = ContentType.json;

      if (body != null) {
        request.write(jsonEncode(body));
      }

      final response = await request.close();
      final text = await response.transform(utf8.decoder).join();
      final decoded = text.isEmpty
          ? <String, dynamic>{}
          : jsonDecode(text) as Map<String, dynamic>;

      if (response.statusCode >= 200 && response.statusCode < 300) {
        return decoded;
      }

      throw ApiException(
        response.statusCode,
        decoded['error'] as String? ?? 'REQUEST_FAILED',
        decoded['message'] as String? ?? response.reasonPhrase,
      );
    } finally {
      client.close(force: true);
    }
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