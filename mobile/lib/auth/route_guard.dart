import 'auth_provider.dart';

/// Pages anyone can open without signing in.
const publicPaths = {'/login', '/register'};

/// Pages only depot managers (and admins) use; everything else is the volunteer side.
const depotManagerPaths = {'/stock-check-in', '/low-stock'};

/// The screen a role starts on after signing in.
String homeFor(AuthUser user) => user.isDepotManager ? '/stock-check-in' : '/';

/// GoRouter redirect rule: signed-out users go to /login, signed-in users skip the
/// login pages, and each role stays on its own screens. Returns null to stay put.
String? authRedirect(AuthProvider auth, String location) {
  if (auth.status == AuthStatus.unknown) {
    return location == '/splash' ? null : '/splash';
  }

  final user = auth.user;
  if (user == null) {
    return publicPaths.contains(location) ? null : '/login';
  }

  if (publicPaths.contains(location) || location == '/splash') {
    return homeFor(user);
  }
  if (user.isAdmin) return null;

  final depotPage = depotManagerPaths.contains(location);
  if (user.isDepotManager != depotPage) {
    return homeFor(user);
  }
  return null;
}
