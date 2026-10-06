/// Backend base URL, injected at build time:
///   flutter build apk --dart-define=FLOODLINK_API_BASE_URL=https://api.example.com
///
/// Defaults to localhost for local development. Use 10.0.2.2 on an Android
/// emulator (the emulator's loopback maps to the host machine via 10.0.2.2).
const String kApiBaseUrl = String.fromEnvironment(
  'FLOODLINK_API_BASE_URL',
  defaultValue: 'http://localhost:5000',
);
