class Shelter {
  final int id;
  final String name;
  final double latitude;
  final double longitude;
  final int capacity;
  final int currentOccupancy;
  final String status;

  Shelter({
    required this.id,
    required this.name,
    required this.latitude,
    required this.longitude,
    required this.capacity,
    required this.currentOccupancy,
    required this.status,
  });

  factory Shelter.fromJson(Map<String, dynamic> json) {
    return Shelter(
      id: json['id'] as int,
      name: json['name'] as String? ?? 'Unnamed Shelter',
      latitude: (json['latitude'] as num?)?.toDouble() ?? 0.0,
      longitude: (json['longitude'] as num?)?.toDouble() ?? 0.0,
      capacity: json['capacity'] as int? ?? 0,
      currentOccupancy: json['currentOccupancy'] as int? ?? 0,
      status: json['status'] as String? ?? 'Active',
    );
  }
}
