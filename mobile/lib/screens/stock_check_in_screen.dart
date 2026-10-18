import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../auth/logout_button.dart';

import '../models/inventory.dart';
import '../providers/inventory_provider.dart';
import 'scan_label_screen.dart';

/// Opens the label scanner and returns the scanned stock line's id, or null if cancelled.
typedef LabelScanner = Future<int?> Function(BuildContext context);

class StockCheckInScreen extends StatefulWidget {
  const StockCheckInScreen({super.key, this.scanLabel});

  /// Injected in tests; otherwise the camera scanner (8.06).
  final LabelScanner? scanLabel;

  @override
  State<StockCheckInScreen> createState() => _StockCheckInScreenState();
}

class _StockCheckInScreenState extends State<StockCheckInScreen> {
  final _formKey = GlobalKey<FormState>();
  final _quantityController = TextEditingController();
  int? _selectedDepotId;
  int? _selectedItemId;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<InventoryProvider>().fetchDepots();
    });
  }

  @override
  void dispose() {
    _quantityController.dispose();
    super.dispose();
  }

  // Scanning a shelf label picks its depot and stock line, ready for the quantity.
  Future<void> _scan() async {
    final scanner = widget.scanLabel ??
        (context) => Navigator.of(context).push<int>(MaterialPageRoute(builder: (_) => const ScanLabelScreen()));
    final id = await scanner(context);
    if (id == null || !mounted) return;
    final provider = context.read<InventoryProvider>();
    final item = await provider.fetchItem(id);
    if (item == null || !mounted) return;
    setState(() {
      _selectedDepotId = item.depotId;
      _selectedItemId = null;
    });
    await provider.fetchItems(item.depotId);
    if (!mounted) return;
    setState(() => _selectedItemId = item.id);
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text('Scanned ${item.itemName}${item.depotName != null ? ' at ${item.depotName}' : ''}.')),
    );
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate() || _selectedItemId == null) return;
    final provider = context.read<InventoryProvider>();
    final success = await provider.checkIn(
      itemId: _selectedItemId!,
      quantityReceived: double.parse(_quantityController.text.trim()),
    );
    if (!mounted) return;

    if (success) {
      _quantityController.clear();
      await provider.fetchItems(_selectedDepotId!);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Stock check-in recorded.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<InventoryProvider>();
    InventoryItem? selectedItem;
    for (final item in provider.items) {
      if (item.id == _selectedItemId) {
        selectedItem = item;
        break;
      }
    }

    return Scaffold(
      appBar: AppBar(
        title: const Text('Stock Check-in'),
        actions: [
          IconButton(
            icon: const Icon(Icons.fact_check_outlined),
            tooltip: 'Check reserved stock',
            onPressed: () => context.push('/reservations'),
          ),
          IconButton(
            icon: const Icon(Icons.warning_amber_rounded),
            tooltip: 'Low stock',
            onPressed: () => context.push('/low-stock'),
          ),
          const LogoutButton(),
        ],
      ),
      body: provider.isLoadingDepots
          ? const Center(child: CircularProgressIndicator())
          : Padding(
              padding: const EdgeInsets.all(16),
              child: Form(
                key: _formKey,
                child: ListView(
                  children: [
                    const Text(
                      'Record received relief supplies',
                      style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 8),
                    const Text('Select the depot and stock item, then enter the received quantity.'),
                    const SizedBox(height: 12),
                    OutlinedButton.icon(
                      onPressed: provider.isLoadingItems ? null : _scan,
                      icon: const Icon(Icons.qr_code_scanner),
                      label: const Text('Scan shelf label'),
                    ),
                    const SizedBox(height: 24),
                    DropdownButtonFormField<int>(
                      key: ValueKey('depot-$_selectedDepotId'),
                      initialValue: _selectedDepotId,
                      decoration: const InputDecoration(labelText: 'Depot', border: OutlineInputBorder()),
                      items: provider.depots
                          .map((depot) => DropdownMenuItem(value: depot.id, child: Text(depot.name)))
                          .toList(),
                      onChanged: (depotId) {
                        setState(() {
                          _selectedDepotId = depotId;
                          _selectedItemId = null;
                        });
                        if (depotId != null) provider.fetchItems(depotId);
                      },
                      validator: (value) => value == null ? 'Select a depot.' : null,
                    ),
                    const SizedBox(height: 16),
                    if (provider.isLoadingItems) const LinearProgressIndicator(),
                    DropdownButtonFormField<int>(
                      key: ValueKey('item-$_selectedDepotId-$_selectedItemId'),
                      initialValue: _selectedItemId,
                      decoration: const InputDecoration(labelText: 'Stock item', border: OutlineInputBorder()),
                      items: provider.items
                          .map((item) => DropdownMenuItem(
                                value: item.id,
                                child: Text('${item.itemName} (${item.quantityAvailable} ${item.unit})'),
                              ))
                          .toList(),
                      onChanged: provider.isLoadingItems
                          ? null
                          : (itemId) => setState(() => _selectedItemId = itemId),
                      validator: (value) => value == null ? 'Select an item.' : null,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _quantityController,
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                      decoration: InputDecoration(
                        labelText: 'Quantity received${selectedItem == null ? '' : ' (${selectedItem.unit})'}',
                        border: const OutlineInputBorder(),
                      ),
                      validator: (value) {
                        final quantity = double.tryParse(value?.trim() ?? '');
                        return quantity == null || quantity <= 0 ? 'Enter a quantity greater than zero.' : null;
                      },
                    ),
                    if (provider.error != null) ...[
                      const SizedBox(height: 16),
                      Text(provider.error!, style: const TextStyle(color: Color(0xFFDA1E28))),
                    ],
                    const SizedBox(height: 24),
                    ElevatedButton.icon(
                      onPressed: provider.isSubmitting ? null : _submit,
                      icon: const Icon(Icons.inventory_2_outlined),
                      label: Text(provider.isSubmitting ? 'Saving…' : 'Record check-in'),
                    ),
                  ],
                ),
              ),
            ),
    );
  }
}
