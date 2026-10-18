import 'dart:io' show Platform;

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/widgets.dart';
import 'package:flutter_map/flutter_map.dart';

/// Whether to load OpenStreetMap tiles. Off under `flutter test` (no network there), so map
/// screens still render their markers and lines in widget tests.
bool get mapTilesEnabled => kIsWeb || !Platform.environment.containsKey('FLUTTER_TEST');

/// The base map: OpenStreetMap tiles (no API key), identified by the app's package name as the
/// OSM tile usage policy asks. Nothing when tiles are disabled.
Widget osmTiles() => mapTilesEnabled
    ? TileLayer(
        urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
        userAgentPackageName: 'lk.floodlink.mobile',
        maxZoom: 19,
      )
    : const SizedBox.shrink();

/// The attribution OpenStreetMap requires on every map that shows its tiles.
Widget osmAttribution() => mapTilesEnabled
    ? const SimpleAttributionWidget(source: Text('OpenStreetMap contributors'))
    : const SizedBox.shrink();
