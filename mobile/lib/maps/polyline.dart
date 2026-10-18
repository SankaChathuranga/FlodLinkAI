import 'package:latlong2/latlong.dart';

/// Decodes an encoded polyline (precision 5, as Mapbox `geometries=polyline` returns and the Route
/// agent stores). Returns an empty list for text that is not a valid polyline.
List<LatLng> decodePolyline(String encoded, {int precision = 5}) {
  final factor = _pow10(precision);
  final points = <LatLng>[];
  var index = 0;
  var lat = 0;
  var lng = 0;

  int? next() {
    var result = 0;
    var shift = 0;
    int byte;
    do {
      if (index >= encoded.length) return null;
      byte = encoded.codeUnitAt(index++) - 63;
      if (byte < 0 || byte > 63 + 31) return null;
      result |= (byte & 0x1f) << shift;
      shift += 5;
    } while (byte >= 0x20);
    return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
  }

  while (index < encoded.length) {
    final dLat = next();
    final dLng = next();
    if (dLat == null || dLng == null) return [];
    lat += dLat;
    lng += dLng;
    points.add(LatLng(lat / factor, lng / factor));
  }
  return points;
}

double _pow10(int n) {
  var value = 1.0;
  for (var i = 0; i < n; i++) {
    value *= 10;
  }
  return value;
}
