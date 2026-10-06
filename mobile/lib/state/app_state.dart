import 'package:flutter/foundation.dart';

import '../config/api_config.dart';

/// Root application state. Owned by the whole team; consumed via Provider.
///
/// Currently holds only the API base URL. Add real shared fields here as the
/// team builds features (see CONTRIBUTING.md — Provider only, no other state libs).
class AppState extends ChangeNotifier {
  /// API base URL for FloodLink backend requests (see config/api_config.dart).
  String apiBaseUrl = kApiBaseUrl;
}