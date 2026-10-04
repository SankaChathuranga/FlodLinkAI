import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'providers/report_provider.dart';
import 'screens/reports_list_screen.dart';
import 'screens/new_report_screen.dart';
import 'screens/camera_capture_screen.dart';
import 'screens/gps_capture_screen.dart';

import 'screens/my_submitted_reports_screen.dart';

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
      path: '/my-submitted-reports',
      builder: (context, state) => const MySubmittedReportsScreen(),
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
        useMaterial3: true,
        fontFamily: 'IBM Plex Sans',
        scaffoldBackgroundColor: const Color(0xFFF4F4F4), // --bg-base
        colorScheme: const ColorScheme(
          brightness: Brightness.light,
          primary: Color(0xFF0F62FE), // --accent-primary
          onPrimary: Color(0xFFFFFFFF),
          secondary: Color(0xFF0043CE), // --state-info
          onSecondary: Color(0xFFFFFFFF),
          error: Color(0xFFDA1E28), // --state-error
          onError: Color(0xFFFFFFFF),
          surface: Color(0xFFFFFFFF), // --bg-surface
          onSurface: Color(0xFF161616), // --text-primary
          surfaceContainerHighest: Color(0xFFF4F4F4),
          onSurfaceVariant: Color(0xFF6F6F6F), // --text-muted
          outline: Color(0xFFC6C6C6), // --border-default
        ),
        appBarTheme: const AppBarTheme(
          backgroundColor: Color(0xFF0F62FE),
          foregroundColor: Color(0xFFFFFFFF),
          elevation: 0,
        ),
        cardTheme: CardThemeData(
          color: const Color(0xFFFFFFFF),
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(6), // rounded-md
            side: const BorderSide(color: Color(0xFFC6C6C6), width: 1),
          ),
        ),
        elevatedButtonTheme: ElevatedButtonThemeData(
          style: ElevatedButton.styleFrom(
            backgroundColor: const Color(0xFF0F62FE),
            foregroundColor: const Color(0xFFFFFFFF),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(4), // rounded-sm
            ),
          ),
        ),
      ),
      routerConfig: _router,
    );
  }
}

