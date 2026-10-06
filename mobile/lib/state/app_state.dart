import 'package:flutter/foundation.dart';

/// Root application state. Owned by the whole team; consumed via Provider.
///
/// Currently holds only the API base URL. Add real shared fields here as the
/// team builds features (see CONTRIBUTING.md — Provider only, no other state libs).
class AppState extends ChangeNotifier {
  /// API base URL for FloodLink backend requests.
  ///
  /// TODO (Week 3): Replace with flutter_dotenv value:
  ///   dotenv.env['API_BASE_URL'] ?? 'http://localhost:5000'
  ///
  /// Use 10.0.2.2 instead of localhost when running on an Android emulator
  /// (the emulator's loopback maps to the host machine via 10.0.2.2).
  String apiBaseUrl = 'http://localhost:5000';
}