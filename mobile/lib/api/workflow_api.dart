import 'dart:convert';

import 'api_client.dart';
import 'dispatch_api.dart' show ApiException;

/// GET /api/workflows/{id}/status — the lightweight status a volunteer polls for a run that
/// includes one of their reports.
class WorkflowStatus {
  const WorkflowStatus({
    required this.id,
    required this.state,
    required this.stageLabel,
    required this.progressPercent,
    required this.isFinished,
    required this.awaitingApproval,
    required this.isDelivered,
    required this.updatedAt,
    this.failedAtState,
    this.failureReason,
    this.decision,
    this.coordinatorNote,
    this.decidedAt,
    this.distanceKm,
    this.etaMinutes,
    this.routeLegs = const [],
  });

  final String id;
  final String state;
  final String stageLabel;
  final int progressPercent;
  final bool isFinished;
  final bool awaitingApproval;
  final bool isDelivered;
  final DateTime updatedAt;
  final String? failedAtState;
  final String? failureReason;
  final String? decision;
  final String? coordinatorNote;
  final DateTime? decidedAt;
  final double? distanceKm;
  final double? etaMinutes;

  /// The delivery legs (depot → shelter) once the plan is routed, for the route map.
  final List<RouteLegInfo> routeLegs;

  bool get isApproved => state == 'Approved';

  /// Nothing more will change: finished, and either not approved or already delivered.
  bool get isSettled => isFinished && (!isApproved || isDelivered);

  factory WorkflowStatus.fromJson(Map<String, dynamic> json) => WorkflowStatus(
        id: json['id'] as String,
        state: json['state'] as String,
        stageLabel: json['stageLabel'] as String? ?? json['state'] as String,
        progressPercent: json['progressPercent'] as int? ?? 0,
        isFinished: json['isFinished'] as bool? ?? false,
        awaitingApproval: json['awaitingApproval'] as bool? ?? false,
        isDelivered: json['isDelivered'] as bool? ?? false,
        updatedAt: DateTime.tryParse(json['updatedAt'] as String? ?? '') ?? DateTime.now(),
        failedAtState: json['failedAtState'] as String?,
        failureReason: json['failureReason'] as String?,
        decision: json['decision'] as String?,
        coordinatorNote: json['coordinatorNote'] as String?,
        decidedAt: DateTime.tryParse(json['decidedAt'] as String? ?? ''),
        distanceKm: (json['distanceKm'] as num?)?.toDouble(),
        etaMinutes: (json['etaMinutes'] as num?)?.toDouble(),
        routeLegs: ((json['routeLegs'] as List<dynamic>?) ?? const [])
            .map((leg) => RouteLegInfo.fromJson(leg as Map<String, dynamic>))
            .toList(),
      );
}

/// One delivery leg of a routed plan: both end points, distance, ETA and the encoded road geometry.
class RouteLegInfo {
  const RouteLegInfo({
    required this.depotName,
    required this.depotLatitude,
    required this.depotLongitude,
    required this.shelterName,
    required this.shelterLatitude,
    required this.shelterLongitude,
    required this.distanceKm,
    required this.etaMinutes,
    required this.polyline,
  });

  final String depotName;
  final double depotLatitude;
  final double depotLongitude;
  final String shelterName;
  final double shelterLatitude;
  final double shelterLongitude;
  final double distanceKm;
  final double etaMinutes;
  final String polyline;

  factory RouteLegInfo.fromJson(Map<String, dynamic> json) => RouteLegInfo(
        depotName: json['depotName'] as String? ?? 'Depot ${json['depotId']}',
        depotLatitude: (json['depotLatitude'] as num).toDouble(),
        depotLongitude: (json['depotLongitude'] as num).toDouble(),
        shelterName: json['shelterName'] as String? ?? 'Shelter ${json['shelterId']}',
        shelterLatitude: (json['shelterLatitude'] as num).toDouble(),
        shelterLongitude: (json['shelterLongitude'] as num).toDouble(),
        distanceKm: (json['distanceKm'] as num?)?.toDouble() ?? 0,
        etaMinutes: (json['etaMinutes'] as num?)?.toDouble() ?? 0,
        polyline: json['polyline'] as String? ?? '',
      );
}

/// Workflow endpoints the field app uses, through the shared authenticated [ApiClient].
class WorkflowApi {
  const WorkflowApi(this._api);

  final ApiClient _api;

  Future<WorkflowStatus> fetchStatus(String workflowRunId) async {
    final response = await _api.get('/api/workflows/$workflowRunId/status');
    if (response.statusCode == 200) {
      return WorkflowStatus.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
    }
    final message = switch (response.statusCode) {
      404 => 'This workflow was not found, or it does not include your reports.',
      401 => 'Your session has expired. Sign in again.',
      403 => 'Your account cannot view this workflow.',
      _ => 'The server returned HTTP ${response.statusCode}.',
    };
    throw ApiException(response.statusCode, 'STATUS_FAILED', message);
  }
}
