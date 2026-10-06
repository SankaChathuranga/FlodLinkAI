// Response models for Member D's dispatch endpoints.
// Field names mirror the backend JSON (camelCase as produced by .NET).

/// GET /api/dispatches/by-workflow/{workflowRunId}
class DispatchStatus {
  const DispatchStatus({
    required this.workflowRunId,
    required this.state,
    this.decision,
    this.notes,
    this.dispatchedAt,
    this.isDelivered,
    this.deliveredAt,
  });

  final String workflowRunId;
  final String state;
  final String? decision;
  final String? notes;
  final DateTime? dispatchedAt;
  final bool? isDelivered;
  final DateTime? deliveredAt;

  factory DispatchStatus.fromJson(Map<String, dynamic> json) => DispatchStatus(
        workflowRunId: json['workflowRunId'] as String,
        state: json['state'] as String,
        decision: json['decision'] as String?,
        notes: json['notes'] as String?,
        dispatchedAt: _parseDate(json['dispatchedAt']),
        isDelivered: json['isDelivered'] as bool?,
        deliveredAt: _parseDate(json['deliveredAt']),
      );
}

/// POST /api/dispatches/{workflowRunId}/confirm-delivery
class DeliveryResult {
  const DeliveryResult({
    required this.workflowRunId,
    required this.isDelivered,
    this.state,
    this.notes,
  });

  final String workflowRunId;
  final bool isDelivered;
  final String? state;
  final String? notes;

  factory DeliveryResult.fromJson(Map<String, dynamic> json) => DeliveryResult(
        workflowRunId: json['workflowRunId'] as String,
        isDelivered: json['isDelivered'] as bool? ?? false,
        state: json['state'] as String?,
        notes: json['notes'] as String?,
      );
}

DateTime? _parseDate(Object? value) =>
    value is String ? DateTime.tryParse(value) : null;