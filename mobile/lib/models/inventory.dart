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
  });

  factory InventoryItem.fromJson(Map<String, dynamic> json) => InventoryItem(
        id: json['id'] as int,
        depotId: json['depotId'] as int,
        itemName: json['itemName'] as String? ?? 'Unnamed item',
        unit: json['unit'] as String? ?? 'units',
        quantityAvailable: (json['quantityAvailable'] as num?)?.toDouble() ?? 0,
      );
}
