class Depot {
  final int id;
  final String name;

  const Depot({required this.id, required this.name});

  factory Depot.fromJson(Map<String, dynamic> json) => Depot(
        id: json['id'] as int,
        name: json['name'] as String? ?? 'Unnamed depot',
      );
}

class InventoryItem {
  final int id;
  final int depotId;
  final String itemName;
  final String unit;
  final double quantityAvailable;

  const InventoryItem({
    required this.id,
    required this.depotId,
    required this.itemName,
    required this.unit,
    required this.quantityAvailable,
    this.quantityReserved = 0,
    this.reorderThreshold = 0,
    this.depotName,
  });

  /// Held for plans awaiting approval.
  final double quantityReserved;

  /// Reserve floor: free stock at or below this is low.
  final double reorderThreshold;

  final String? depotName;

  /// Stock that plans can still use (on hand minus held).
  double get freeStock => quantityAvailable - quantityReserved;

  /// The same rule as the API's lowStock filter.
  bool get isLowStock => freeStock <= reorderThreshold;

  factory InventoryItem.fromJson(Map<String, dynamic> json) => InventoryItem(
        id: json['id'] as int,
        depotId: json['depotId'] as int,
        itemName: json['itemName'] as String? ?? 'Unnamed item',
        unit: json['unit'] as String? ?? 'units',
        quantityAvailable: (json['quantityAvailable'] as num?)?.toDouble() ?? 0,
        quantityReserved: (json['quantityReserved'] as num?)?.toDouble() ?? 0,
        reorderThreshold: (json['reorderThreshold'] as num?)?.toDouble() ?? 0,
        depotName: (json['depot'] as Map<String, dynamic>?)?['name'] as String?,
      );
}
