import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'providers/report_provider.dart';
import 'screens/reports_list_screen.dart';
import 'screens/new_report_screen.dart';
import 'screens/camera_capture_screen.dart';
import 'screens/gps_capture_screen.dart';

void main() {
  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => AppState()),
        ChangeNotifierProvider(create: (_) => ReportProvider()),
      ],
      child: const FloodLinkApp(),
    ),
  );
}

class AppState extends ChangeNotifier {
  String apiBaseUrl = 'http://localhost:5000';
}

final GoRouter _router = GoRouter(
  initialLocation: '/',
  routes: [
    GoRoute(
      path: '/',
      builder: (context, state) => const ReportsListScreen(),
    ),
    GoRoute(
      path: '/new-report',
      builder: (context, state) => const NewReportScreen(),
    ),
    GoRoute(
      path: '/camera',
      builder: (context, state) => const CameraCaptureScreen(),
    ),
    GoRoute(
      path: '/gps',
      builder: (context, state) => const GpsCaptureScreen(),
    ),
  ],
);

class FloodLinkApp extends StatelessWidget {
  const FloodLinkApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: 'FloodLink Field App',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF0369A1)),
        useMaterial3: true,
      ),
      routerConfig: _router,
    );
  }
}
