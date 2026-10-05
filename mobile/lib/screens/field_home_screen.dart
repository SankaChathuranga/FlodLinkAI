import 'package:flutter/material.dart';

import '../main.dart' show FloodLinkColors;
import 'delivery_confirmation_screen.dart';
import 'dispatch_status_screen.dart';

/// Landing screen for the field app: entry points to Member D's field-worker
/// flows. Navigation uses the plain Material Navigator — no routing package
/// yet (Member A owns go_router in Week 4).
class FieldHomeScreen extends StatelessWidget {
  const FieldHomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('FloodLink Field'),
      ),
      body: Container(
        color: FloodLinkColors.bgBase,
        padding: const EdgeInsets.all(16),
        child: const Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _HomeOptionCard(
              icon: Icons.assignment_outlined,
              title: 'Check dispatch status',
              subtitle: 'Look up a workflow run and see its decision, reason and delivery state.',
              destination: DispatchStatusScreen(),
            ),
            SizedBox(height: 12),
            _HomeOptionCard(
              icon: Icons.local_shipping_outlined,
              title: 'Confirm delivery',
              subtitle: 'Mark an approved dispatch as delivered on-site.',
              destination: DeliveryConfirmationScreen(),
            ),
          ],
        ),
      ),
    );
  }
}

class _HomeOptionCard extends StatelessWidget {
  const _HomeOptionCard({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.destination,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final Widget destination;

  @override
  Widget build(BuildContext context) {
    return Card(
      // CardTheme in main.dart provides border + radius + bg
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute<void>(builder: (_) => destination),
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
          child: Row(
            children: [
              Icon(icon, color: FloodLinkColors.accentPrimary, size: 24),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: const TextStyle(
                        fontWeight: FontWeight.w600,
                        fontSize: 15,
                        color: FloodLinkColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      subtitle,
                      style: const TextStyle(
                        fontSize: 13,
                        color: FloodLinkColors.textMuted,
                      ),
                    ),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right, color: FloodLinkColors.textMuted, size: 20),
            ],
          ),
        ),
      ),
    );
  }
}
