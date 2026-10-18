/// The text on a FloodLink stock label's QR code is `FLI:<inventory item id>` (printed from the web
/// Inventory page). Returns the id, or null when [code] is not a FloodLink stock label.
int? parseStockLabel(String? code) {
  final match = RegExp(r'^FLI:(\d{1,9})$').firstMatch(code?.trim() ?? '');
  return match == null ? null : int.parse(match.group(1)!);
}
