class FieldReport {
  final int? id;
  final int shelterId;
  final int reportedBy;
  final String needType;
  final int quantityNeeded;
  final int urgencyLevel;
  final String? photoUrl;
  final double? gpsLat;
  final double? gpsLng;
  final String status;
  final DateTime? createdAt;

  // From GET /api/reports/mine: the run handling this report and the coordinator's decision.
  final String? shelterName;
  final String? workflowRunId;
  final String? workflowState;
  final String? decision;

  FieldReport({
    this.id,
    required this.shelterId,
    required this.reportedBy,
    required this.needType,
    required this.quantityNeeded,
    required this.urgencyLevel,
    this.photoUrl,
    this.gpsLat,
    this.gpsLng,
    required this.status,
    this.createdAt,
    this.shelterName,
    this.workflowRunId,
    this.workflowState,
    this.decision,
  });

  factory FieldReport.fromJson(Map<String, dynamic> json) {
    return FieldReport(
      id: json['id'] as int?,
      shelterId: json['shelterId'] as int? ?? 1,
      reportedBy: json['reportedBy'] as int? ?? 1,
      needType: json['needType'] as String? ?? 'Water',
      quantityNeeded: json['quantityNeeded'] as int? ?? 1,
      urgencyLevel: json['urgencyLevel'] as int? ?? 50,
      photoUrl: json['photoUrl'] as String?,
      gpsLat: (json['gpsLat'] as num?)?.toDouble(),
      gpsLng: (json['gpsLng'] as num?)?.toDouble(),
      status: json['status'] as String? ?? 'New',
      createdAt: json['createdAt'] != null
          ? DateTime.tryParse(json['createdAt'] as String)
          : null,
      shelterName: json['shelterName'] as String?,
      workflowRunId: json['workflowRunId'] as String?,
      workflowState: json['workflowState'] as String?,
      decision: json['decision'] as String?,
    );
  }
}
