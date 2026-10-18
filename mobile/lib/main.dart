import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'api/api_client.dart';
import 'auth/auth_provider.dart';
import 'auth/route_guard.dart';
import 'auth/token_store.dart';
import 'config/api_config.dart';
import 'providers/report_provider.dart';
import 'providers/inventory_provider.dart';
import 'screens/reports_list_screen.dart';
import 'screens/new_report_screen.dart';
import 'screens/camera_capture_screen.dart';
import 'screens/gps_capture_screen.dart';
import 'screens/login_screen.dart';
import 'screens/register_screen.dart';
import 'screens/workflow_status_screen.dart';

import 'screens/my_submitted_reports_screen.dart';
import 'screens/stock_check_in_screen.dart';
import 'screens/field_home_screen.dart';
import 'screens/low_stock_screen.dart';
import 'screens/shelter_status_screen.dart';
import 'state/app_state.dart';
import 'notifications/status_notifier.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final auth = AuthProvider(baseUrl: kApiBaseUrl, store: SecureTokenStore());
  final api = ApiClient(baseUrl: kApiBaseUrl, auth: auth);
  final notifier = LocalStatusNotifier();
  runApp(FloodLinkProviders(auth: auth, api: api, notifier: notifier, child: const FloodLinkApp()));
  // Ask for notification permission and set up the channel without delaying the first frame.
  await notifier.init();
  // Restores a saved session; until it finishes the router shows the splash screen.
  await auth.restore();
}

/// The app's provider tree. Every provider reaches the API through the one [ApiClient],
/// which adds the signed-in user's token. Tests build the same tree with fakes.
class FloodLinkProviders extends StatelessWidget {
  const FloodLinkProviders({
    super.key,
    required this.auth,
    required this.api,
    required this.child,
    this.notifier = const SilentStatusNotifier(),
  });

  final AuthProvider auth;
  final ApiClient api;
  final StatusNotifier notifier;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthProvider>.value(value: auth),
        Provider<ApiClient>.value(value: api),
        Provider<StatusNotifier>.value(value: notifier),
        ChangeNotifierProvider(create: (_) => AppState()),
        ChangeNotifierProvider(create: (_) => ReportProvider(api)),
        ChangeNotifierProvider(create: (_) => InventoryProvider(api)),
      ],
      child: child,
    );
  }
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

/// Routes, guarded by [authRedirect]: signed-out users only see /login and /register,
/// volunteers their reports and status, depot managers stock check-in.
GoRouter buildRouter(AuthProvider auth) => GoRouter(
  initialLocation: '/',
  refreshListenable: auth,
  redirect: (context, state) => authRedirect(auth, state.matchedLocation),
  routes: [
    GoRoute(
      path: '/splash',
      builder: (context, state) => const Scaffold(body: Center(child: CircularProgressIndicator())),
    ),
    GoRoute(
      path: '/login',
      builder: (context, state) => const LoginScreen(),
    ),
    GoRoute(
      path: '/register',
      builder: (context, state) => const RegisterScreen(),
    ),
    GoRoute(
      path: '/',
      builder: (context, state) => const ReportsListScreen(),
    ),
    GoRoute(
      path: '/stock-check-in',
      builder: (context, state) => const StockCheckInScreen(),
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
      path: '/dispatch',
      builder: (context, state) => const FieldHomeScreen(),
    ),
    GoRoute(
      path: '/status/:runId',
      builder: (context, state) => WorkflowStatusScreen(workflowRunId: state.pathParameters['runId']!),
    ),
    GoRoute(
      path: '/low-stock',
      builder: (context, state) => const LowStockScreen(),
    ),
    GoRoute(
      path: '/shelters',
      builder: (context, state) => const ShelterStatusScreen(),
    ),
    GoRoute(
      path: '/gps',
      builder: (context, state) => const GpsCaptureScreen(),
    ),
  ],
);

class FloodLinkApp extends StatefulWidget {
  const FloodLinkApp({super.key});

  @override
  State<FloodLinkApp> createState() => _FloodLinkAppState();
}

class _FloodLinkAppState extends State<FloodLinkApp> {
  late final GoRouter _router = buildRouter(context.read<AuthProvider>());

  @override
  void dispose() {
    _router.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
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
      routerConfig: _router,
    );
  }
}
