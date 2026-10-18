import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../api/workflow_api.dart';
import '../maps/map_tiles.dart';
import '../maps/polyline.dart';

const _legColors = [Color(0xFF0F62FE), Color(0xFFDA1E28), Color(0xFF198038), Color(0xFF8A3FFC), Color(0xFFB28600)];

/// The approved supply route on a map (8.11): every depot → shelter leg drawn on OpenStreetMap,
/// with the depots and shelters marked, and each leg's distance and ETA listed underneath.
class RouteMapScreen extends StatelessWidget {
  const RouteMapScreen({super.key, required this.legs});

  final List<RouteLegInfo> legs;

  @override
  Widget build(BuildContext context) {
    final lines = [
      for (var i = 0; i < legs.length; i++)
        (leg: legs[i], color: _legColors[i % _legColors.length], points: _pointsOf(legs[i])),
    ];
    // Fit the camera to the roads and to every depot and shelter, so no marker starts off-screen.
    final all = [
      ...lines.expand((l) => l.points),
      for (final l in lines) ...[
        LatLng(l.leg.depotLatitude, l.leg.depotLongitude),
        LatLng(l.leg.shelterLatitude, l.leg.shelterLongitude),
      ],
    ];

    return Scaffold(
      appBar: AppBar(title: const Text('Supply route')),
      body: lines.isEmpty
          ? const Center(child: Text('This plan has no route to show yet.'))
          : Column(
              children: [
                Expanded(
                  child: FlutterMap(
                    options: MapOptions(
                      initialCameraFit: all.length > 1
                          ? CameraFit.bounds(bounds: LatLngBounds.fromPoints(all), padding: const EdgeInsets.all(40))
                          : null,
                      initialCenter: all.first,
                      initialZoom: 12,
                    ),
                    children: [
                      osmTiles(),
                      PolylineLayer(polylines: [
                        for (final l in lines) Polyline(points: l.points, color: l.color, strokeWidth: 4),
                      ]),
                      MarkerLayer(markers: [
                        for (final l in lines) ...[
                          Marker(
                            point: LatLng(l.leg.depotLatitude, l.leg.depotLongitude),
                            width: 36,
                            height: 36,
                            child: Tooltip(
                              message: 'Depot: ${l.leg.depotName}',
                              child: Icon(Icons.warehouse, color: l.color, semanticLabel: 'Depot ${l.leg.depotName}'),
                            ),
                          ),
                          Marker(
                            point: LatLng(l.leg.shelterLatitude, l.leg.shelterLongitude),
                            width: 36,
                            height: 36,
                            child: Tooltip(
                              message: 'Shelter: ${l.leg.shelterName}',
                              child: Icon(Icons.location_on, color: l.color, semanticLabel: 'Shelter ${l.leg.shelterName}'),
                            ),
                          ),
                        ],
                      ]),
                      osmAttribution(),
                    ],
                  ),
                ),
                SafeArea(
                  top: false,
                  child: ListView(
                    shrinkWrap: true,
                    padding: const EdgeInsets.all(12),
                    children: [
                      for (final l in lines)
                        ListTile(
                          dense: true,
                          leading: Container(width: 16, height: 4, color: l.color),
                          title: Text('${l.leg.depotName} → ${l.leg.shelterName}'),
                          subtitle: Text('${l.leg.distanceKm.toStringAsFixed(1)} km · about ${l.leg.etaMinutes.round()} min'),
                        ),
                    ],
                  ),
                ),
              ],
            ),
    );
  }

  // The road geometry, or a straight line between the two ends when it can't be decoded.
  static List<LatLng> _pointsOf(RouteLegInfo leg) {
    final decoded = decodePolyline(leg.polyline);
    return decoded.length >= 2
        ? decoded
        : [LatLng(leg.depotLatitude, leg.depotLongitude), LatLng(leg.shelterLatitude, leg.shelterLongitude)];
  }
}
