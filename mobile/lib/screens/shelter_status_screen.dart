import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../main.dart' show FloodLinkColors;
import '../models/shelter.dart';
import '../providers/report_provider.dart';

/// Shelter status (8.07): every shelter's occupancy against capacity, its status, and when it
/// was last updated, so a volunteer can see where help is most needed.
class ShelterStatusScreen extends StatefulWidget {
  const ShelterStatusScreen({super.key});

  @override
  State<ShelterStatusScreen> createState() => _ShelterStatusScreenState();
}

class _ShelterStatusScreenState extends State<ShelterStatusScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => context.read<ReportProvider>().fetchShelters());
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ReportProvider>();
    // Fullest shelters first: they need help soonest.
    final shelters = [...provider.shelters]..sort((a, b) => b.occupancyRatio.compareTo(a.occupancyRatio));

    Widget body;
    if (provider.isLoadingShelters && shelters.isEmpty) {
      body = const Center(child: CircularProgressIndicator());
    } else if (provider.sheltersError != null) {
      body = ListView(padding: const EdgeInsets.all(32), children: [
        const Icon(Icons.error_outline, color: FloodLinkColors.stateError, size: 48),
        const SizedBox(height: 12),
        Text(provider.sheltersError!, textAlign: TextAlign.center),
      ]);
    } else if (shelters.isEmpty) {
      body = ListView(padding: const EdgeInsets.all(32), children: const [
        Text('No shelters are registered yet.', textAlign: TextAlign.center),
      ]);
    } else {
      body = ListView.separated(
        padding: const EdgeInsets.all(16),
        itemCount: shelters.length,
        separatorBuilder: (_, __) => const SizedBox(height: 8),
        itemBuilder: (_, index) => _ShelterCard(shelter: shelters[index]),
      );
    }

    return Scaffold(
      appBar: AppBar(
        title: const Text('Shelter status'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'Refresh',
            onPressed: provider.isLoadingShelters ? null : provider.fetchShelters,
          ),
        ],
      ),
      body: RefreshIndicator(onRefresh: provider.fetchShelters, child: body),
    );
  }
}

class _ShelterCard extends StatelessWidget {
  const _ShelterCard({required this.shelter});
  final Shelter shelter;

  Color get _barColor {
    final ratio = shelter.occupancyRatio;
    if (ratio >= 0.9) return FloodLinkColors.stateError;
    if (ratio >= 0.7) return const Color(0xFFB28600);
    return FloodLinkColors.stateSuccess;
  }

  String _updated(DateTime? at) {
    if (at == null) return 'Last update unknown';
    final ago = DateTime.now().difference(at);
    if (ago.inMinutes < 1) return 'Updated just now';
    if (ago.inHours < 1) return 'Updated ${ago.inMinutes} min ago';
    if (ago.inDays < 1) return 'Updated ${ago.inHours} h ago';
    return 'Updated ${at.year}-${at.month.toString().padLeft(2, '0')}-${at.day.toString().padLeft(2, '0')}';
  }

  @override
  Widget build(BuildContext context) {
    final percent = (shelter.occupancyRatio * 100).round();
    final closed = shelter.status != 'Active';
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(shelter.name,
                      style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15, color: FloodLinkColors.textPrimary)),
                ),
                Chip(
                  label: Text(shelter.status),
                  visualDensity: VisualDensity.compact,
                  backgroundColor: closed ? const Color(0xFFE0E0E0) : FloodLinkColors.badgeSuccessBg,
                ),
              ],
            ),
            const SizedBox(height: 8),
            Semantics(
              label: 'Occupancy $percent percent',
              child: LinearProgressIndicator(
                value: shelter.occupancyRatio.clamp(0, 1).toDouble(),
                minHeight: 8,
                color: _barColor,
                backgroundColor: const Color(0xFFE0E0E0),
              ),
            ),
            const SizedBox(height: 6),
            Text(
              '${shelter.currentOccupancy} / ${shelter.capacity} people · $percent%'
              '${shelter.occupancyRatio > 1 ? ' — over capacity' : ''}',
              style: const TextStyle(color: FloodLinkColors.textPrimary),
            ),
            const SizedBox(height: 2),
            Text(_updated(shelter.updatedAt), style: const TextStyle(fontSize: 12, color: FloodLinkColors.textMuted)),
          ],
        ),
      ),
    );
  }
}
