import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../auth/auth_provider.dart';
import '../auth/validators.dart';
import '../main.dart' show FloodLinkColors;
import 'login_screen.dart' show AuthErrorBanner;

/// Volunteer self-registration. The server always creates a Volunteer account and signs
/// it in; the router then opens the volunteer home screen.
class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _phone = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();
  bool _submitting = false;
  String? _error;

  @override
  void dispose() {
    for (final c in [_name, _email, _phone, _password, _confirm]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() => _error = null);
    if (!_formKey.currentState!.validate()) return;
    setState(() => _submitting = true);
    try {
      await context.read<AuthProvider>().register(
            name: _name.text,
            email: _email.text,
            phone: _phone.text,
            password: _password.text,
          );
    } on AuthException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Create volunteer account'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          tooltip: 'Back to sign in',
          onPressed: () => context.go('/login'),
        ),
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Text(
                      'Field volunteers can report shelter needs and follow their status.',
                      style: TextStyle(color: FloodLinkColors.textMuted),
                    ),
                    const SizedBox(height: 16),
                    if (_error != null) ...[
                      AuthErrorBanner(message: _error!),
                      const SizedBox(height: 16),
                    ],
                    TextFormField(
                      key: const Key('register-name'),
                      controller: _name,
                      textCapitalization: TextCapitalization.words,
                      decoration: const InputDecoration(labelText: 'Full name'),
                      validator: AuthValidators.name,
                      enabled: !_submitting,
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      key: const Key('register-email'),
                      controller: _email,
                      keyboardType: TextInputType.emailAddress,
                      decoration: const InputDecoration(labelText: 'Email'),
                      validator: AuthValidators.email,
                      enabled: !_submitting,
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      key: const Key('register-phone'),
                      controller: _phone,
                      keyboardType: TextInputType.phone,
                      decoration: const InputDecoration(labelText: 'Phone', hintText: '+94771234567'),
                      validator: AuthValidators.phone,
                      enabled: !_submitting,
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      key: const Key('register-password'),
                      controller: _password,
                      obscureText: true,
                      decoration: const InputDecoration(labelText: 'Password (8+ characters)'),
                      validator: AuthValidators.newPassword,
                      enabled: !_submitting,
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      key: const Key('register-confirm'),
                      controller: _confirm,
                      obscureText: true,
                      decoration: const InputDecoration(labelText: 'Confirm password'),
                      validator: (v) => v == _password.text ? null : 'Passwords do not match.',
                      enabled: !_submitting,
                    ),
                    const SizedBox(height: 20),
                    FilledButton(
                      key: const Key('register-submit'),
                      onPressed: _submitting ? null : _submit,
                      child: _submitting
                          ? const SizedBox(
                              height: 20,
                              width: 20,
                              child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                            )
                          : const Text('Create account'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
