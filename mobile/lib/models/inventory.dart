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

/// A reserved allocation line at one of the manager's depots, to check on the shelf before dispatch (8.05).
class ReservedLine {
  const ReservedLine({
    required this.id,
    required this.workflowRunId,
    required this.depotName,
    required this.shelterName,
    required this.itemName,
    required this.quantity,
    this.countedQuantity,
    this.confirmedBy,
    this.confirmationNote,
  });

  final int id;
  final String workflowRunId;
  final String depotName;
  final String shelterName;
  final String itemName;
  final double quantity;
  final double? countedQuantity;
  final String? confirmedBy;
  final String? confirmationNote;

  bool get isChecked => countedQuantity != null;
  bool get isShort => countedQuantity != null && countedQuantity! < quantity;

  factory ReservedLine.fromJson(Map<String, dynamic> json) => ReservedLine(
        id: json['id'] as int,
        workflowRunId: json['workflowRunId'] as String,
        depotName: json['depotName'] as String? ?? 'Depot #${json['depotId']}',
        shelterName: json['shelterName'] as String? ?? 'Shelter #${json['shelterId']}',
        itemName: json['itemName'] as String? ?? 'Item',
        quantity: (json['quantity'] as num).toDouble(),
        countedQuantity: (json['countedQuantity'] as num?)?.toDouble(),
        confirmedBy: json['confirmedBy'] as String?,
        confirmationNote: json['confirmationNote'] as String?,
      );
}
