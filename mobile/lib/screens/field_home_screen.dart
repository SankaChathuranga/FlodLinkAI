import 'package:flutter/material.dart';

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
        backgroundColor: Theme.of(context).colorScheme.primary,
        title: const Text(
          'FloodLink Field',
          style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold),
        ),
      ),
      body: const Padding(
        padding: EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _HomeOptionCard(
              icon: Icons.assignment_late_outlined,
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
      clipBehavior: Clip.antiAlias,
      child: ListTile(
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        leading: Icon(icon, color: Theme.of(context).colorScheme.primary),
        title: Text(title, style: const TextStyle(fontWeight: FontWeight.w600)),
        subtitle: Text(subtitle),
        trailing: const Icon(Icons.chevron_right),
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute<void>(builder: (_) => destination),
        ),
      ),
    );
  }
}