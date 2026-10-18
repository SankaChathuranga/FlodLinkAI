import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../main.dart' show FloodLinkColors;
import '../models/inventory.dart';
import '../providers/inventory_provider.dart';

/// Depot manager's check of reserved stock before dispatch (8.05): each reserved line at their depots,
/// to confirm it is on the shelf or to report how many are really there. The coordinator sees the result
/// in the approval queue before deciding.
class ReservationConfirmationScreen extends StatefulWidget {
  const ReservationConfirmationScreen({super.key});

  @override
  State<ReservationConfirmationScreen> createState() => _ReservationConfirmationScreenState();
}

class _ReservationConfirmationScreenState extends State<ReservationConfirmationScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => context.read<InventoryProvider>().fetchReservations());
  }

  Future<void> _confirm(ReservedLine line, double counted, String? note) async {
    final error = await context.read<InventoryProvider>().confirmReservation(line.id, counted, note: note);
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(error ?? (counted < line.quantity ? 'Shortfall reported to the coordinator.' : 'Stock confirmed.')),
      backgroundColor: error == null ? null : FloodLinkColors.stateError,
    ));
  }

  Future<void> _reportCount(ReservedLine line) async {
    final count = TextEditingController(text: _qty(line.quantity));
    final note = TextEditingController();
    final formKey = GlobalKey<FormState>();
    final result = await showDialog<(double, String)>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('Count ${line.itemName}'),
        content: Form(
          key: formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('${_qty(line.quantity)} are reserved for ${line.shelterName}.'),
              const SizedBox(height: 12),
              TextFormField(
                controller: count,
                autofocus: true,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                decoration: const InputDecoration(labelText: 'Counted on the shelf'),
                validator: (v) {
                  final n = double.tryParse(v?.trim() ?? '');
                  return n == null || n < 0 ? 'Enter the number you counted (0 or more).' : null;
                },
              ),
              TextFormField(
                controller: note,
                maxLength: 500,
                decoration: const InputDecoration(labelText: 'Note (optional)', hintText: 'e.g. 5 bottles damaged'),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancel')),
          FilledButton(
            onPressed: () {
              if (formKey.currentState!.validate()) {
                Navigator.pop(context, (double.parse(count.text.trim()), note.text));
              }
            },
            child: const Text('Save count'),
          ),
        ],
      ),
    );
    if (result != null) await _confirm(line, result.$1, result.$2);
  }

  static String _qty(double v) => v == v.roundToDouble() ? v.toStringAsFixed(0) : v.toString();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<InventoryProvider>();
    final lines = provider.reservations;

    Widget body;
    if (provider.isLoadingReservations && lines.isEmpty) {
      body = const Center(child: CircularProgressIndicator());
    } else if (provider.reservationsError != null) {
      body = ListView(padding: const EdgeInsets.all(32), children: [
        Text(provider.reservationsError!, textAlign: TextAlign.center, style: const TextStyle(color: FloodLinkColors.stateError)),
      ]);
    } else if (lines.isEmpty) {
      body = ListView(padding: const EdgeInsets.all(32), children: const [
        Icon(Icons.inventory_2_outlined, size: 48, color: FloodLinkColors.textMuted),
        SizedBox(height: 12),
        Text('No reserved stock to check at your depots right now.', textAlign: TextAlign.center),
      ]);
    } else {
      body = ListView.separated(
        padding: const EdgeInsets.all(16),
        itemCount: lines.length,
        separatorBuilder: (_, __) => const SizedBox(height: 8),
        itemBuilder: (_, i) {
          final line = lines[i];
          return Card(
            child: Padding(
              padding: const EdgeInsets.all(14),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('${line.itemName} × ${_qty(line.quantity)}',
                      style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15, color: FloodLinkColors.textPrimary)),
                  Text('From ${line.depotName} to ${line.shelterName}', style: const TextStyle(color: FloodLinkColors.textMuted)),
                  const SizedBox(height: 8),
                  if (line.isChecked)
                    Text(
                      line.isShort
                          ? 'Shortfall reported: ${_qty(line.countedQuantity!)} of ${_qty(line.quantity)} on the shelf'
                          : 'Confirmed on the shelf${line.confirmedBy != null ? ' by ${line.confirmedBy}' : ''}',
                      style: TextStyle(color: line.isShort ? FloodLinkColors.stateError : FloodLinkColors.stateSuccess),
                    ),
                  Wrap(
                    spacing: 8,
                    children: [
                      FilledButton.icon(
                        icon: const Icon(Icons.check),
                        label: const Text('All on the shelf'),
                        onPressed: () => _confirm(line, line.quantity, null),
                      ),
                      OutlinedButton(onPressed: () => _reportCount(line), child: const Text('Report a different count')),
                    ],
                  ),
                ],
              ),
            ),
          );
        },
      );
    }

    return Scaffold(
      appBar: AppBar(
        title: const Text('Check reserved stock'),
        actions: [
          IconButton(icon: const Icon(Icons.refresh), tooltip: 'Refresh', onPressed: provider.fetchReservations),
        ],
      ),
      body: RefreshIndicator(onRefresh: provider.fetchReservations, child: body),
    );
  }
}
