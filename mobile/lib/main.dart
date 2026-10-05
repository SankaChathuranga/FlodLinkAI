import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'screens/field_home_screen.dart';
import 'state/app_state.dart';

// ── State Management Pattern ──────────────────────────────────────────────────
//
// STATE MANAGEMENT RULE (see CONTRIBUTING.md):
// All shared state MUST go through the `provider` package only.
// Do NOT add Riverpod, BLoC, GetX, or any other state management library.
//
// PATTERN IN USE:
// 1. Define a ChangeNotifier class (see AppState in lib/state/app_state.dart).
// 2. Wrap the MaterialApp with ChangeNotifierProvider at the root.
// 3. Consume state with:
//    final appState = context.watch<AppState>();   // rebuilds on change
//    final appState = context.read<AppState>();    // one-time read, no rebuild
// ─────────────────────────────────────────────────────────────────────────────

void main() {
  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => AppState()),
      ],
      child: const FloodLinkApp(),
    ),
  );
}

// ── Design tokens mirroring ui-context.md CSS variables ─────────────────────
// These hex values MUST stay in sync with index.css :root tokens.
class FloodLinkColors {
  FloodLinkColors._();

  static const bgBase        = Color(0xFFF4F4F4);
  static const bgSurface     = Color(0xFFFFFFFF);
  static const textPrimary   = Color(0xFF161616);
  static const textMuted     = Color(0xFF6F6F6F);
  static const accentPrimary = Color(0xFF0F62FE);
  static const borderDefault = Color(0xFFC6C6C6);
  static const stateError    = Color(0xFFDA1E28);
  static const stateWarning  = Color(0xFFF1C21B);
  static const stateSuccess  = Color(0xFF198038);
  static const stateInfo     = Color(0xFF0043CE);

  // Badge backgrounds
  static const badgeSuccessBg = Color(0xFFD0E9D7);
  static const badgeErrorBg   = Color(0xFFFFD7D9);
  static const badgeWarningBg = Color(0xFFFFF0C1);
  static const badgeInfoBg    = Color(0xFFD0E2FF);
}

// ── App widget ───────────────────────────────────────────────────────────────

class FloodLinkApp extends StatelessWidget {
  const FloodLinkApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'FloodLink Field App',
      debugShowCheckedModeBanner: false,
      // Carbon g10-inspired theme – accentPrimary (#0F62FE) as seed
      theme: ThemeData(
        useMaterial3: true,
        fontFamily: 'IBMPlexSans', // Falls back to system sans if not bundled
        colorScheme: const ColorScheme.light(
          primary:        FloodLinkColors.accentPrimary,
          onPrimary:      FloodLinkColors.bgSurface,
          secondary:      FloodLinkColors.stateInfo,
          onSecondary:    FloodLinkColors.bgSurface,
          error:          FloodLinkColors.stateError,
          onError:        FloodLinkColors.bgSurface,
          surface:        FloodLinkColors.bgSurface,
          onSurface:      FloodLinkColors.textPrimary,
          surfaceContainerHighest: FloodLinkColors.bgBase,
        ),
        scaffoldBackgroundColor: FloodLinkColors.bgBase,
        appBarTheme: const AppBarTheme(
          backgroundColor: FloodLinkColors.textPrimary, // Carbon dark header
          foregroundColor: FloodLinkColors.bgBase,
          elevation: 0,
          titleTextStyle: TextStyle(
            color:      FloodLinkColors.bgBase,
            fontSize:   16,
            fontWeight: FontWeight.w600,
          ),
        ),
        cardTheme: CardThemeData(
          color: FloodLinkColors.bgSurface,
          elevation: 0,
          shape: RoundedRectangleBorder(
            side: const BorderSide(color: FloodLinkColors.borderDefault),
            borderRadius: BorderRadius.circular(4),
          ),
          margin: EdgeInsets.zero,
        ),
        inputDecorationTheme: InputDecorationTheme(
          filled: true,
          fillColor: FloodLinkColors.bgSurface,
          contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(4),
            borderSide: const BorderSide(color: FloodLinkColors.borderDefault),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(4),
            borderSide: const BorderSide(color: FloodLinkColors.borderDefault),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(4),
            borderSide: const BorderSide(color: FloodLinkColors.accentPrimary, width: 2),
          ),
          labelStyle: const TextStyle(color: FloodLinkColors.textMuted, fontSize: 14),
          hintStyle: const TextStyle(color: FloodLinkColors.textMuted, fontSize: 14),
        ),
        filledButtonTheme: FilledButtonThemeData(
          style: FilledButton.styleFrom(
            backgroundColor: FloodLinkColors.accentPrimary,
            foregroundColor: FloodLinkColors.bgSurface,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
            textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14),
            minimumSize: const Size(0, 44), // large touch target per spec
          ),
        ),
        outlinedButtonTheme: OutlinedButtonThemeData(
          style: OutlinedButton.styleFrom(
            foregroundColor: FloodLinkColors.textPrimary,
            side: const BorderSide(color: FloodLinkColors.borderDefault),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
            textStyle: const TextStyle(fontWeight: FontWeight.w500, fontSize: 14),
            minimumSize: const Size(0, 44),
          ),
        ),
        dividerTheme: const DividerThemeData(
          color: FloodLinkColors.borderDefault,
          space: 0,
        ),
      ),
      home: const FieldHomeScreen(),
    );
  }
}
