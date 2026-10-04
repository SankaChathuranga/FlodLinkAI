import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import '../providers/report_provider.dart';

class GpsCaptureScreen extends StatefulWidget {
  const GpsCaptureScreen({super.key});

  @override
  State<GpsCaptureScreen> createState() => _GpsCaptureScreenState();
}

class _GpsCaptureScreenState extends State<GpsCaptureScreen> {
  double _latitude = 6.9271; // Default Colombo coordinates
  double _longitude = 79.8612;
  double _accuracy = 10.0;

  bool _isAcquiring = false;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    final provider = context.read<ReportProvider>();
    if (provider.selectedLat != null && provider.selectedLng != null) {
      _latitude = provider.selectedLat!;
      _longitude = provider.selectedLng!;
    } else {
      _autoCaptureLocation();
    }
  }

  Future<void> _autoCaptureLocation() async {
    setState(() {
      _isAcquiring = true;
      _errorMessage = null;
    });

    try {
      bool serviceEnabled = await Geolocator.isLocationServiceEnabled();
      if (!serviceEnabled) {
        setState(() {
          _errorMessage = 'GPS location services are disabled on device.';
        });
        return;
      }

      LocationPermission permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
        if (permission == LocationPermission.denied) {
          setState(() {
            _errorMessage = 'Location permissions were denied.';
          });
          return;
        }
      }

      if (permission == LocationPermission.deniedForever) {
        setState(() {
          _errorMessage = 'Location permissions are permanently denied.';
        });
        return;
      }

      Position position = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.high,
          timeLimit: Duration(seconds: 8),
        ),
      );

      setState(() {
        _latitude = position.latitude;
        _longitude = position.longitude;
        _accuracy = position.accuracy;
      });
    } catch (e) {
      setState(() {
        _errorMessage = 'Failed to acquire GPS position: $e. Using manual pin.';
      });
    } finally {
      setState(() {
        _isAcquiring = false;
      });
    }
  }

  void _confirmCoordinates() {
    context.read<ReportProvider>().setSelectedGps(_latitude, _longitude);
    context.pop();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF4F4F4), // --bg-base
      appBar: AppBar(
        title: const Text('GPS Location & Pin Adjust'),
        backgroundColor: const Color(0xFF0F62FE), // --accent-primary
        foregroundColor: Colors.white,
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.my_location),
            onPressed: _isAcquiring ? null : _autoCaptureLocation,
            tooltip: 'Auto-acquire GPS Position',
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            // ERROR / PERMISSION DENIED BANNER
            if (_errorMessage != null)
              Container(
                color: const Color(0xFFF1C21B).withValues(alpha: 0.15), // --state-warning
                padding: const EdgeInsets.all(12),
                child: Row(
                  children: [
                    const Icon(Icons.warning_amber_rounded, color: Color(0xFF161616)),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        _errorMessage!,
                        style: const TextStyle(fontSize: 12, color: Color(0xFF161616)), // Warning contrast rule
                      ),
                    ),
                    TextButton(
                      onPressed: _autoCaptureLocation,
                      child: const Text('Retry', style: TextStyle(color: Color(0xFF0F62FE))),
                    ),
                  ],
                ),
              ),

            // LOADING STATE
            if (_isAcquiring)
              Container(
                color: const Color(0xFF0043CE).withValues(alpha: 0.1), // --state-info
                padding: const EdgeInsets.all(12),
                child: const Row(
                  children: [
                    SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2, color: Color(0xFF0043CE)),
                    ),
                    SizedBox(width: 12),
                    Text(
                      'Acquiring satellite GPS fix...',
                      style: TextStyle(fontSize: 13, fontWeight: FontWeight.bold, color: Color(0xFF161616)),
                    ),
                  ],
                ),
              ),

            // SIMULATED MAP / PIN VISUALIZER
            Expanded(
              child: Container(
                margin: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: const Color(0xFF161616), // --text-primary dark background for map viewer
                  borderRadius: BorderRadius.circular(6), // rounded-md
                  border: Border.all(color: const Color(0xFFC6C6C6)),
                ),
                child: Stack(
                  alignment: Alignment.center,
                  children: [
                    // Grid background lines
                    CustomPaint(
                      size: Size.infinite,
                      painter: MapGridPainter(),
                    ),

                    // Pin Marker
                    Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                          decoration: BoxDecoration(
                            color: const Color(0xFF161616).withValues(alpha: 0.85),
                            borderRadius: BorderRadius.circular(4),
                            border: Border.all(color: const Color(0xFF0F62FE)),
                          ),
                          child: Text(
                            'Lat: ${_latitude.toStringAsFixed(5)}, Lng: ${_longitude.toStringAsFixed(5)}',
                            style: const TextStyle(
                              color: Colors.white,
                              fontSize: 11,
                              fontFamily: 'IBM Plex Mono', // --font-mono
                            ),
                          ),
                        ),
                        const SizedBox(height: 4),
                        const Icon(
                          Icons.location_on,
                          size: 48,
                          color: Color(0xFF0F62FE), // --accent-primary
                        ),
                      ],
                    ),

                    // Floating GPS Accuracy badge
                    Positioned(
                      top: 12,
                      right: 12,
                      child: Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        decoration: BoxDecoration(
                          color: const Color(0xFF161616).withValues(alpha: 0.8),
                          borderRadius: BorderRadius.circular(16), // rounded-full
                          border: Border.all(color: const Color(0xFF198038)),
                        ),
                        child: Text(
                          'Accuracy: ~${_accuracy.toStringAsFixed(1)}m',
                          style: const TextStyle(color: Color(0xFF198038), fontSize: 10, fontWeight: FontWeight.bold),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),

            // MANUAL PIN-ADJUST CONTROLS
            Container(
              margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              decoration: BoxDecoration(
                color: const Color(0xFFFFFFFF), // --bg-surface
                borderRadius: BorderRadius.circular(6), // rounded-md
                border: Border.all(color: const Color(0xFFC6C6C6)),
              ),
              child: Padding(
                padding: const EdgeInsets.all(16.0),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Manual Pin Fine-Tuning',
                      style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: Color(0xFF161616)),
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        const SizedBox(width: 70, child: Text('Latitude:', style: TextStyle(color: Color(0xFF161616)))),
                        Expanded(
                          child: Slider(
                            activeColor: const Color(0xFF0F62FE),
                            min: 6.0,
                            max: 8.0,
                            value: _latitude.clamp(6.0, 8.0),
                            onChanged: (val) => setState(() => _latitude = val),
                          ),
                        ),
                        Text(
                          _latitude.toStringAsFixed(4),
                          style: const TextStyle(fontFamily: 'IBM Plex Mono', fontSize: 12, color: Color(0xFF161616)),
                        ),
                      ],
                    ),
                    Row(
                      children: [
                        const SizedBox(width: 70, child: Text('Longitude:', style: TextStyle(color: Color(0xFF161616)))),
                        Expanded(
                          child: Slider(
                            activeColor: const Color(0xFF0F62FE),
                            min: 79.0,
                            max: 81.0,
                            value: _longitude.clamp(79.0, 81.0),
                            onChanged: (val) => setState(() => _longitude = val),
                          ),
                        ),
                        Text(
                          _longitude.toStringAsFixed(4),
                          style: const TextStyle(fontFamily: 'IBM Plex Mono', fontSize: 12, color: Color(0xFF161616)),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),

            // CONFIRM ACTION
            Padding(
              padding: const EdgeInsets.all(16.0),
              child: SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  onPressed: _confirmCoordinates,
                  icon: const Icon(Icons.check_circle_outline),
                  label: const Text('Confirm GPS Coordinates'),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF0F62FE), // --accent-primary
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(4), // rounded-sm
                    ),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class MapGridPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = Colors.white.withValues(alpha: 0.05)
      ..strokeWidth = 1.0;

    for (double i = 0; i < size.width; i += 30) {
      canvas.drawLine(Offset(i, 0), Offset(i, size.height), paint);
    }
    for (double j = 0; j < size.height; j += 30) {
      canvas.drawLine(Offset(0, j), Offset(size.width, j), paint);
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

