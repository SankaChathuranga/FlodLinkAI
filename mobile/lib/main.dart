import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'screens/stock_check_in_screen.dart';

// ── State Management Pattern ──────────────────────────────────────────────────
//
// STATE MANAGEMENT RULE (see CONTRIBUTING.md):
// All shared state MUST go through the `provider` package only.
// Do NOT add Riverpod, BLoC, GetX, or any other state management library.
//
// PATTERN IN USE:
// 1. Define a ChangeNotifier class (see AppState below).
// 2. Wrap the MaterialApp with ChangeNotifierProvider at the root.
// 3. Consume state with:
//    final appState = context.watch<AppState>();   // rebuilds on change
//    final appState = context.read<AppState>();    // one-time read, no rebuild
//
// As the app grows, add more specific ChangeNotifier classes per feature area
// (e.g. WorkflowStatusNotifier, ReportNotifier) and nest additional Providers
// inside MultiProvider alongside AppState.
//
// Environment / API base URL:
// When ready to add environment config (Week 3), add `flutter_dotenv` to
// pubspec.yaml and load it before runApp():
//
//   import 'package:flutter_dotenv/flutter_dotenv.dart';
//   await dotenv.load(fileName: ".env");
//   final apiBaseUrl = dotenv.env['API_BASE_URL'] ?? 'http://localhost:5000';
//
// For now, the URL is hardcoded in AppState as a placeholder.
// ─────────────────────────────────────────────────────────────────────────────

void main() {
  runApp(
    // MultiProvider allows multiple providers at the root.
    // Add more ChangeNotifierProvider entries here as features are built.
    MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => AppState()),
        // Planned for Week 3: Add feature-specific providers here, e.g.:
        //   ChangeNotifierProvider(create: (_) => WorkflowStatusNotifier()),
        //   ChangeNotifierProvider(create: (_) => ReportNotifier()),
      ],
      child: const FloodLinkApp(),
    ),
  );
}

// ── Placeholder shared state ──────────────────────────────────────────────────

/// Root application state. Currently holds only the API base URL as a placeholder.
///
/// Add real shared fields here as the team builds features:
///
/// ```dart
/// User? currentUser;
///
/// void setCurrentUser(User user) {
///   currentUser = user;
///   notifyListeners(); // triggers rebuild in widgets using context.watch<AppState>()
/// }
/// ```
class AppState extends ChangeNotifier {
  /// API base URL for FloodLink backend requests.
  ///
  /// Planned for Week 3: Replace with flutter_dotenv value:
  ///   dotenv.env['API_BASE_URL'] ?? 'http://localhost:5000'
  ///
  /// Use 10.0.2.2 instead of localhost when running on an Android emulator
  /// (the emulator's loopback maps to the host machine via 10.0.2.2).
  String apiBaseUrl = 'http://localhost:5000';

  // Add real state fields here as features are implemented.
  // Example:
  //   User? currentUser;
  //   String? currentWorkflowId;
}

// ── FloodLink Semantic Colours ────────────────────────────────────────────────
//
// Hex values match the web CSS variable table exactly.
// Use these constants in any widget that needs semantic state colours that
// don't fit neatly into ColorScheme (warning and success).
//
// Usage:
//   Container(color: AppColors.stateWarning)   // amber background
//   Text('Low', style: TextStyle(color: AppColors.stateError))
//
// Contrast rule (mirrors web spec):
//   stateWarning (#F1C21B) has poor contrast — always place dark text on top.
//   Never use white text on a warning background.

abstract final class AppColors {
  // Interactive (matches --accent-primary)
  static const accent = Color(0xFF0F62FE);

  // Semantic states
  static const stateError   = Color(0xFFDA1E28); // --state-error
  static const stateWarning = Color(0xFFF1C21B); // --state-warning (dark text only!)
  static const stateSuccess = Color(0xFF198038); // --state-success
  static const stateInfo    = Color(0xFF0043CE); // --state-info (in-progress badges only)

  // Surfaces and text
  static const bgBase      = Color(0xFFF4F4F4); // --bg-base
  static const bgSurface   = Color(0xFFFFFFFF); // --bg-surface
  static const textPrimary = Color(0xFF161616); // --text-primary
  static const textMuted   = Color(0xFF6F6F6F); // --text-muted
  static const border      = Color(0xFFC6C6C6); // --border-default

  // Warning contrast helper: text colour to use ON a warning background
  static const onWarning = Color(0xFF161616); // --text-primary (dark)
}

// ── App widget ────────────────────────────────────────────────────────────────

class FloodLinkApp extends StatelessWidget {
  const FloodLinkApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'FloodLink Field App',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        useMaterial3: true,
        // ── FloodLink Color Scheme ──────────────────────────────────────────
        // Hex values match the web design token table exactly so status colours
        // look identical on web (CSS vars) and mobile (Flutter ColorScheme).
        // Update both places if any colour changes in the spec.
        colorScheme: const ColorScheme.light(
          // Primary (accent) — interactive elements only
          primary:        Color(0xFF0F62FE), // --accent-primary
          onPrimary:      Color(0xFFFFFFFF),
          primaryContainer: Color(0xFF0043CE), // --state-info
          onPrimaryContainer: Color(0xFFFFFFFF),
          // Surfaces
          surface:        Color(0xFFFFFFFF), // --bg-surface
          onSurface:      Color(0xFF161616), // --text-primary
          surfaceContainerHighest: Color(0xFFF4F4F4), // --bg-base
          // Semantic states
          error:          Color(0xFFDA1E28), // --state-error
          onError:        Color(0xFFFFFFFF),
          // Borders / muted
          outline:        Color(0xFFC6C6C6), // --border-default
          outlineVariant: Color(0xFF6F6F6F), // --text-muted
          // Warning and success are not in ColorScheme natively;
          // access them via AppColors constants (see below).
        ),
        // ── Typography ────────────────────────────────────────────────────
        // IBM Plex Sans requires the google_fonts package (add in Week 3).
        // Until then, falls back to the system sans-serif.
        //
        // To activate IBM Plex Sans:
        //   1. Add google_fonts: ^6.0.0 to pubspec.yaml
        //   2. Replace fontFamily below with:
        //        fontFamily: GoogleFonts.ibmPlexSans().fontFamily
        //   3. Import: import 'package:google_fonts/google_fonts.dart';
        fontFamily: 'sans-serif', // placeholder — swap for GoogleFonts.ibmPlexSans()
      ),
      home: const PlaceholderScreen(),
    );
  }
}

// ── Placeholder screen ────────────────────────────────────────────────────────

/// Placeholder home screen. Replace with the real navigation shell once
/// Member A adds go_router in Week 4.
class PlaceholderScreen extends StatelessWidget {
  const PlaceholderScreen({super.key});

  @override
  Widget build(BuildContext context) {
    // Read the API base URL from AppState via Provider.
    // Using context.watch here so the widget rebuilds if apiBaseUrl changes.
    final appState = context.watch<AppState>();

    return Scaffold(
      appBar: AppBar(
        backgroundColor: Theme.of(context).colorScheme.primary,
        title: const Text(
          'FloodLink',
          style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold),
        ),
      ),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24.0),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              // App icon placeholder
              Container(
                width: 80,
                height: 80,
                decoration: BoxDecoration(
                  color: Theme.of(context).colorScheme.primary,
                  borderRadius: BorderRadius.circular(20),
                ),
                child: const Icon(
                  Icons.flood,
                  color: Colors.white,
                  size: 48,
                ),
              ),
              const SizedBox(height: 24),
              const Text(
                'FloodLink Field App',
                style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              const Text(
                '— under construction —',
                style: TextStyle(fontSize: 16, color: Colors.grey),
              ),
              const SizedBox(height: 32),
              // Provider smoke-test: shows that AppState is accessible
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16.0),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'Provider ✓',
                        style: TextStyle(
                          fontWeight: FontWeight.bold,
                          color: Colors.green,
                          fontSize: 12,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'API: ${appState.apiBaseUrl}',
                        style: const TextStyle(fontSize: 12, color: Colors.grey),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 32),
              ElevatedButton.icon(
                onPressed: () {
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (context) => const StockCheckInScreen(),
                    ),
                  );
                },
                icon: const Icon(Icons.inventory),
                label: const Text('Stock Check-In'),
                style: ElevatedButton.styleFrom(
                  padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
