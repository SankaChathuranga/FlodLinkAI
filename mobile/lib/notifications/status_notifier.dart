import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';

import '../api/workflow_api.dart';

/// Shows a notification when a tracked supply request changes stage (8.12).
abstract class StatusNotifier {
  Future<void> notify(String title, String body);
}

/// Does nothing — used where notifications are unavailable and in tests.
class SilentStatusNotifier implements StatusNotifier {
  const SilentStatusNotifier();

  @override
  Future<void> notify(String title, String body) async {}
}

/// Local (on-device) notifications through flutter_local_notifications. Call [init] once at startup.
class LocalStatusNotifier implements StatusNotifier {
  LocalStatusNotifier([FlutterLocalNotificationsPlugin? plugin]) : _plugin = plugin ?? FlutterLocalNotificationsPlugin();

  final FlutterLocalNotificationsPlugin _plugin;
  bool _ready = false;
  int _nextId = 0;

  static const _channel = AndroidNotificationDetails(
    'workflow_status',
    'Supply request updates',
    channelDescription: 'When a coordinator approves, rejects or revises your supply request.',
    importance: Importance.high,
    priority: Priority.high,
  );

  Future<void> init() async {
    try {
      _ready = await _plugin.initialize(
            settings: const InitializationSettings(
              android: AndroidInitializationSettings('@mipmap/ic_launcher'),
              iOS: DarwinInitializationSettings(),
              linux: LinuxInitializationSettings(defaultActionName: 'Open'),
            ),
          ) ??
          false;
      // Android 13+ asks the user before an app may post notifications.
      await _plugin
          .resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>()
          ?.requestNotificationsPermission();
    } catch (e) {
      // A platform without notification support: carry on silently.
      debugPrint('Notifications unavailable: $e');
      _ready = false;
    }
  }

  @override
  Future<void> notify(String title, String body) async {
    if (!_ready) return;
    try {
      await _plugin.show(
        id: _nextId++,
        title: title,
        body: body,
        notificationDetails: const NotificationDetails(android: _channel, iOS: DarwinNotificationDetails()),
      );
    } catch (e) {
      debugPrint('Notification failed: $e');
    }
  }
}

/// The notification for a change from [previous] to [current], or null when nothing worth telling
/// the volunteer happened (same stage, or an internal step like Matching → Routing).
({String title, String body})? statusChangeMessage(WorkflowStatus? previous, WorkflowStatus current) {
  if (previous == null) return null;
  final delivered = current.isDelivered && !previous.isDelivered;
  if (previous.state == current.state && !delivered) return null;

  final note = current.coordinatorNote?.trim();
  final withNote = note == null || note.isEmpty ? '' : ' “$note”';
  if (delivered) return (title: 'Delivery confirmed', body: 'The supplies for your request were delivered.');
  switch (current.state) {
    case 'PendingApproval':
      return (title: 'Plan ready', body: 'A supply plan for your request is waiting for coordinator approval.');
    case 'Approved':
      final eta = current.etaMinutes;
      return (
        title: eta != null ? 'Supplies approved — ETA ${eta.round()} min' : 'Supplies approved',
        body: 'The coordinator approved the delivery.$withNote',
      );
    case 'Rejected':
      return (title: 'Request not approved', body: 'The coordinator rejected the plan.$withNote');
    case 'RevisionRequested':
      return (title: 'Plan being revised', body: 'The coordinator asked for changes.$withNote');
    case 'Failed':
      return (title: 'Planning stopped', body: 'The plan could not be completed; a coordinator can retry it.');
    default:
      return null;
  }
}
