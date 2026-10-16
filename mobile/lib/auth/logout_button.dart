import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'auth_provider.dart';

/// App-bar action that signs out; the router guard then returns to /login.
class LogoutButton extends StatelessWidget {
  const LogoutButton({super.key, this.color});

  final Color? color;

  @override
  Widget build(BuildContext context) {
    return IconButton(
      icon: Icon(Icons.logout, color: color),
      tooltip: 'Sign out',
      onPressed: () => context.read<AuthProvider>().logout(),
    );
  }
}
