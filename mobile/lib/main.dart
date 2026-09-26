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
    // MultiProvider allows multiple providers at the root.
    // Add more ChangeNotifierProvider entries here as features are built.
    MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => AppState()),
        // TODO (Week 3): Add feature-specific providers here, e.g.:
        //   ChangeNotifierProvider(create: (_) => WorkflowStatusNotifier()),
        //   ChangeNotifierProvider(create: (_) => ReportNotifier()),
      ],
      child: const FloodLinkApp(),
    ),
  );
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
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF0369A1)),
        useMaterial3: true,
      ),
      home: const FieldHomeScreen(),
    );
  }
}
