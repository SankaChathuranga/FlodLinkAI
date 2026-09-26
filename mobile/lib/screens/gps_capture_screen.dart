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
      appBar: AppBar(
        title: const Text('GPS Location & Pin Adjust'),
        backgroundColor: Theme.of(context).colorScheme.primary,
        foregroundColor: Colors.white,
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
                color: Colors.amber.shade50,
                padding: const EdgeInsets.all(12),
                child: Row(
                  children: [
                    const Icon(Icons.warning_amber_rounded, color: Colors.amber),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        _errorMessage!,
                        style: const TextStyle(fontSize: 12, color: Colors.black80),
                      ),
                    ),
                    TextButton(
                      onPressed: _autoCaptureLocation,
                      child: const Text('Retry'),
                    ),
                  ],
                ),
              ),

            // LOADING STATE
            if (_isAcquiring)
              Container(
                color: Colors.blue.shade50,
                padding: const EdgeInsets.all(12),
                child: const Row(
                  children: [
                    SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                    SizedBox(width: 12),
                    Text(
                      'Acquiring satellite GPS fix...',
                      style: TextStyle(fontSize: 13, fontWeight: FontWeight.bold),
                    ),
                  ],
                ),
              ),

            // SIMULATED MAP / PIN VISUALIZER & EMPTY STATE
            Expanded(
              child: Container(
                margin: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: Colors.slate.shade900,
                  borderRadius: BorderRadius.circular(16),
                  boxShadow: const [
                    BoxShadow(color: Colors.black12, blurRadius: 8)
                  ],
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
                            color: Colors.black.withOpacity(0.8),
                            borderRadius: BorderRadius.circular(8),
                            border: Border.all(color: Colors.blueAccent),
                          ),
                          child: Text(
                            'Lat: ${_latitude.toStringAsFixed(5)}, Lng: ${_longitude.toStringAsFixed(5)}',
                            style: const TextStyle(
                              color: Colors.white,
                              fontSize: 11,
                              fontFamily: 'monospace',
                            ),
                          ),
                        ),
                        const SizedBox(height: 4),
                        Icon(
                          Icons.location_on,
                          size: 48,
                          color: Theme.of(context).colorScheme.primary,
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
                          color: Colors.black.withOpacity(0.7),
                          borderRadius: BorderRadius.circular(6),
                        ),
                        child: Text(
                          'Accuracy: ~${_accuracy.toStringAsFixed(1)}m',
                          style: const TextStyle(color: Colors.greenAccent, fontSize: 10),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),

            // MANUAL PIN-ADJUST CONTROLS
            Card(
              margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Padding(
                padding: const EdgeInsets.all(16.0),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Manual Pin Fine-Tuning',
                      style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        const SizedBox(width: 70, child: Text('Latitude:')),
                        Expanded(
                          child: Slider(
                            min: 6.0,
                            max: 8.0,
                            value: _latitude.clamp(6.0, 8.0),
                            onChanged: (val) => setState(() => _latitude = val),
                          ),
                        ),
                        Text(
                          _latitude.toStringAsFixed(4),
                          style: const TextStyle(fontFamily: 'monospace', fontSize: 12),
                        ),
                      ],
                    ),
                    Row(
                      children: [
                        const SizedBox(width: 70, child: Text('Longitude:')),
                        Expanded(
                          child: Slider(
                            min: 79.0,
                            max: 81.0,
                            value: _longitude.clamp(79.0, 81.0),
                            onChanged: (val) => setState(() => _longitude = val),
                          ),
                        ),
                        Text(
                          _longitude.toStringAsFixed(4),
                          style: const TextStyle(fontFamily: 'monospace', fontSize: 12),
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
                    backgroundColor: Theme.of(context).colorScheme.primary,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(vertical: 14),
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
      color = Colors.white.withOpacity(0.05)
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
