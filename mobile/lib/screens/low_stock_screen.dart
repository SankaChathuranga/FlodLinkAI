import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../main.dart' show FloodLinkColors;
import '../models/inventory.dart';
import '../providers/inventory_provider.dart';

/// Depot manager's low-stock alerts (8.04): stock lines of the depots they manage whose free stock
/// (on hand minus held) is at or below the reorder threshold, least free first.
class LowStockScreen extends StatefulWidget {
  const LowStockScreen({super.key});

  @override
  State<LowStockScreen> createState() => _LowStockScreenState();
}

class _LowStockScreenState extends State<LowStockScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => context.read<InventoryProvider>().fetchLowStock());
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<InventoryProvider>();
    return Scaffold(
      appBar: AppBar(
        title: const Text('Low stock'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'Refresh',
            onPressed: provider.isLoadingLowStock ? null : provider.fetchLowStock,
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: provider.fetchLowStock,
        child: _body(provider),
      ),
    );
  }

  Widget _body(InventoryProvider provider) {
    if (provider.isLoadingLowStock && provider.lowStock.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (provider.lowStockError != null) {
      return _Message(icon: Icons.error_outline, color: FloodLinkColors.stateError, text: provider.lowStockError!);
    }
    if (provider.lowStock.isEmpty) {
      return const _Message(
        icon: Icons.check_circle_outline,
        color: FloodLinkColors.stateSuccess,
        text: 'All stock at your depots is above its reorder level.',
      );
    }
    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: provider.lowStock.length + 1,
      separatorBuilder: (_, __) => const SizedBox(height: 8),
      itemBuilder: (context, index) {
        if (index == 0) {
          return Text(
            '${provider.lowStock.length} line${provider.lowStock.length == 1 ? '' : 's'} at or below the reorder level',
            style: const TextStyle(fontWeight: FontWeight.w600, color: FloodLinkColors.textPrimary),
          );
        }
        return _LowStockCard(item: provider.lowStock[index - 1]);
      },
    );
  }
}

class _LowStockCard extends StatelessWidget {
  const _LowStockCard({required this.item});
  final InventoryItem item;

  String _qty(double value) => value == value.roundToDouble() ? value.toStringAsFixed(0) : value.toStringAsFixed(1);

  @override
  Widget build(BuildContext context) {
    final empty = item.freeStock <= 0;
    return Card(
      child: ListTile(
        leading: Icon(empty ? Icons.remove_shopping_cart_outlined : Icons.warning_amber_rounded,
            color: empty ? FloodLinkColors.stateError : const Color(0xFFB28600)),
        title: Text('${item.itemName} · ${item.depotName ?? 'Depot #${item.depotId}'}'),
        subtitle: Text(
          '${_qty(item.freeStock)} ${item.unit} free (reorder level ${_qty(item.reorderThreshold)})'
          '${item.quantityReserved > 0 ? ' · ${_qty(item.quantityReserved)} held' : ''}',
        ),
      ),
    );
  }
}

class _Message extends StatelessWidget {
  const _Message({required this.icon, required this.color, required this.text});
  final IconData icon;
  final Color color;
  final String text;

  @override
  Widget build(BuildContext context) {
    // A scrollable child so pull-to-refresh also works on the message.
    return ListView(
      padding: const EdgeInsets.all(32),
      children: [
        Icon(icon, color: color, size: 48),
        const SizedBox(height: 12),
        Text(text, textAlign: TextAlign.center, style: const TextStyle(color: FloodLinkColors.textPrimary)),
      ],
    );
  }
}
