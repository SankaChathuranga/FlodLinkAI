import 'dart:convert';
import 'dart:math';

import 'package:shared_preferences/shared_preferences.dart';

/// A field report waiting on the phone to be sent (8.09). [clientReference] travels with it, so a re-send
/// after a lost response is matched by the API instead of saved twice.
class QueuedReport {
  const QueuedReport({
    required this.clientReference,
    required this.shelterId,
    required this.needType,
    required this.quantityNeeded,
    required this.notes,
    required this.queuedAt,
    this.gpsLat,
    this.gpsLng,
    this.photoPath,
    this.lastError,
  });

  final String clientReference;
  final int shelterId;
  final String needType;
  final int quantityNeeded;
  final String notes;
  final double? gpsLat;
  final double? gpsLng;
  final String? photoPath;
  final DateTime queuedAt;

  /// Why the server refused it (it will not be retried automatically); null while it is just waiting.
  final String? lastError;

  bool get refused => lastError != null;

  QueuedReport withError(String error) => QueuedReport(
        clientReference: clientReference,
        shelterId: shelterId,
        needType: needType,
        quantityNeeded: quantityNeeded,
        notes: notes,
        queuedAt: queuedAt,
        gpsLat: gpsLat,
        gpsLng: gpsLng,
        photoPath: photoPath,
        lastError: error,
      );

  Map<String, Object?> toJson() => {
        'clientReference': clientReference,
        'shelterId': shelterId,
        'needType': needType,
        'quantityNeeded': quantityNeeded,
        'notes': notes,
        'gpsLat': gpsLat,
        'gpsLng': gpsLng,
        'photoPath': photoPath,
        'queuedAt': queuedAt.toUtc().toIso8601String(),
        'lastError': lastError,
      };

  factory QueuedReport.fromJson(Map<String, dynamic> json) => QueuedReport(
        clientReference: json['clientReference'] as String,
        shelterId: json['shelterId'] as int,
        needType: json['needType'] as String,
        quantityNeeded: json['quantityNeeded'] as int,
        notes: json['notes'] as String? ?? '',
        gpsLat: (json['gpsLat'] as num?)?.toDouble(),
        gpsLng: (json['gpsLng'] as num?)?.toDouble(),
        photoPath: json['photoPath'] as String?,
        queuedAt:
            DateTime.tryParse(json['queuedAt'] as String? ?? '')?.toLocal() ??
                DateTime.now(),
        lastError: json['lastError'] as String?,
      );
}

/// Where the queue is kept between app launches.
abstract class QueueStore {
  Future<String?> read();
  Future<void> write(String value);
}

/// Keeps the queue only while the app runs (tests, and platforms without local storage).
class MemoryQueueStore implements QueueStore {
  MemoryQueueStore([this._value]);
  String? _value;

  @override
  Future<String?> read() async => _value;

  @override
  Future<void> write(String value) async => _value = value;
}

/// Keeps the queue in the app's local key-value storage, so it survives restarts.
class SharedPreferencesQueueStore implements QueueStore {
  static const _key = 'floodlink.offlineReports.v1';

  @override
  Future<String?> read() async =>
      (await SharedPreferences.getInstance()).getString(_key);

  @override
  Future<void> write(String value) async =>
      (await SharedPreferences.getInstance()).setString(_key, value);
}

/// The reports waiting to be sent, oldest first, persisted after every change.
class ReportQueue {
  ReportQueue(this._store);

  factory ReportQueue.memory() => ReportQueue(MemoryQueueStore());

  final QueueStore _store;
  List<QueuedReport> _items = [];
  bool _loaded = false;

  List<QueuedReport> get items => List.unmodifiable(_items);

  Future<List<QueuedReport>> load() async {
    if (_loaded) return items;
    try {
      final raw = await _store.read();
      _items = raw == null
          ? []
          : (jsonDecode(raw) as List<dynamic>)
              .map((e) => QueuedReport.fromJson(e as Map<String, dynamic>))
              .toList();
    } catch (_) {
      _items = []; // unreadable storage: start empty rather than crash
    }
    _loaded = true;
    return items;
  }

  Future<void> add(QueuedReport report) async {
    await load();
    _items = [
      ..._items.where((r) => r.clientReference != report.clientReference),
      report
    ];
    await _save();
  }

  Future<void> remove(String clientReference) async {
    await load();
    _items = _items.where((r) => r.clientReference != clientReference).toList();
    await _save();
  }

  Future<void> replace(QueuedReport report) async {
    await load();
    _items = [
      for (final r in _items)
        r.clientReference == report.clientReference ? report : r
    ];
    await _save();
  }

  Future<void> _save() =>
      _store.write(jsonEncode(_items.map((r) => r.toJson()).toList()));
}

/// A random id for one report submission: 32 hex characters (accepted by the API's ClientReference rule).
String newClientReference([Random? random]) {
  final rng = random ?? Random.secure();
  return List.generate(
      16, (_) => rng.nextInt(256).toRadixString(16).padLeft(2, '0')).join();
}
