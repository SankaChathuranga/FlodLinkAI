import 'dart:async';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import '../providers/report_provider.dart';
import '../models/report.dart';

/// Screen displaying the user's submitted field reports with live status updates.
class MySubmittedReportsScreen extends StatefulWidget {
  const MySubmittedReportsScreen({super.key});

  @override
  State<MySubmittedReportsScreen> createState() => _MySubmittedReportsScreenState();
}

class _MySubmittedReportsScreenState extends State<MySubmittedReportsScreen> {
  String _selectedStatusFilter = 'ALL';
  Timer? _autoRefreshTimer;
  bool _isAutoRefreshEnabled = true;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _loadReports();
    });

    // Setup periodic auto-refresh for live status polling (every 10s)
    _autoRefreshTimer = Timer.periodic(const Duration(seconds: 10), (_) {
      if (_isAutoRefreshEnabled && mounted) {
        context.read<ReportProvider>().fetchReports();
      }
    });
  }

  @override
  void dispose() {
    _autoRefreshTimer?.cancel();
    super.dispose();
  }

  void _loadReports() {
    context.read<ReportProvider>().fetchReports();
  }

  Color _getStatusColor(String status) {
    switch (status.trim().toLowerCase()) {
      case 'new':
        return Colors.blue.shade700;
      case 'triaged':
        return Colors.purple.shade700;
      case 'inplan':
      case 'in-plan':
        return Colors.amber.shade800;
      case 'resolved':
        return Colors.emerald.shade700;
      default:
        return Colors.grey.shade700;
    }
  }

  Color _getUrgencyColor(int urgency) {
    if (urgency >= 80) return Colors.red.shade700;
    if (urgency >= 50) return Colors.orange.shade800;
    return Colors.green.shade700;
  }

  List<FieldReport> _filterReports(List<FieldReport> reports) {
    if (_selectedStatusFilter == 'ALL') return reports;
    return reports.where((r) => r.status.trim().toLowerCase() == _selectedStatusFilter.toLowerCase()).toList();
  }

  @override
  Widget build(BuildContext context) {
    final reportProvider = context.watch<ReportProvider>();
    final filteredReports = _filterReports(reportProvider.reports);

    return Scaffold(
      appBar: AppBar(
        title: const Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'My Submitted Reports',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18, color: Colors.white),
            ),
            Text(
              'Live Status Tracker',
              style: TextStyle(fontSize: 12, color: Colors.white70),
            ),
          ],
        ),
        backgroundColor: Theme.of(context).colorScheme.primary,
        actions: [
          IconButton(
            icon: Icon(
              _isAutoRefreshEnabled ? Icons.sync : Icons.sync_disabled,
              color: Colors.white,
            ),
            tooltip: _isAutoRefreshEnabled ? 'Live Polling Active (10s)' : 'Live Polling Paused',
            onPressed: () {
              setState(() {
                _isAutoRefreshEnabled = !_isAutoRefreshEnabled;
              });
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(
                  content: Text(_isAutoRefreshEnabled ? 'Live status polling enabled' : 'Live polling paused'),
                  duration: const Duration(seconds: 2),
                ),
              );
            },
          ),
          IconButton(
            icon: const Icon(Icons.refresh, color: Colors.white),
            tooltip: 'Refresh Reports',
            onPressed: _loadReports,
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => context.push('/new-report'),
        icon: const Icon(Icons.add),
        label: const Text('New Report'),
        backgroundColor: Theme.of(context).colorScheme.primary,
        foregroundColor: Colors.white,
      ),
      body: Column(
        children: [
          // Status Filter Tabs
          Container(
            color: Colors.white,
            padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 12),
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  _buildFilterChip('ALL', 'All Reports'),
                  _buildFilterChip('New', 'New'),
                  _buildFilterChip('Triaged', 'Triaged'),
                  _buildFilterChip('InPlan', 'In Plan'),
                  _buildFilterChip('Resolved', 'Resolved'),
                ],
              ),
            ),
          ),
          const Divider(height: 1, thickness: 1, color: Color(0xFFE2E8F0)),

          // Main Content
          Expanded(
            child: RefreshIndicator(
              onRefresh: () async => _loadReports(),
              child: Builder(
                builder: (context) {
                  // ERROR STATE
                  if (reportProvider.reportsError != null) {
                    return Center(
                      child: SingleChildScrollView(
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.all(24.0),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            const Icon(Icons.cloud_off_rounded, size: 64, color: Colors.redAccent),
                            const SizedBox(height: 16),
                            const Text(
                              'Connection Error',
                              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                            ),
                            const SizedBox(height: 8),
                            Text(
                              reportProvider.reportsError!,
                              textAlign: TextAlign.center,
                              style: const TextStyle(color: Colors.grey),
                            ),
                            const SizedBox(height: 24),
                            ElevatedButton.icon(
                              onPressed: _loadReports,
                              icon: const Icon(Icons.refresh),
                              label: const Text('Retry Connection'),
                            ),
                          ],
                        ),
                      ),
                    );
                  }

                  // LOADING STATE
                  if (reportProvider.isLoadingReports && reportProvider.reports.isEmpty) {
                    return const Center(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          CircularProgressIndicator(),
                          SizedBox(height: 16),
                          Text('Fetching your field report updates...'),
                        ],
                      ),
                    );
                  }

                  // EMPTY STATE
                  if (filteredReports.isEmpty) {
                    return ListView(
                      physics: const AlwaysScrollableScrollPhysics(),
                      children: [
                        SizedBox(height: MediaQuery.of(context).size.height * 0.2),
                        Center(
                          child: Column(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              Icon(Icons.assignment_turned_in_outlined, size: 64, color: Colors.grey.shade400),
                              const SizedBox(height: 16),
                              Text(
                                _selectedStatusFilter == 'ALL'
                                    ? 'No Submitted Reports Yet'
                                    : 'No Reports with Status "$_selectedStatusFilter"',
                                style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                              ),
                              const SizedBox(height: 8),
                              const Text(
                                'Tap "+ New Report" to record emergency needs.',
                                style: TextStyle(color: Colors.grey),
                              ),
                            ],
                          ),
                        ),
                      ],
                    );
                  }

                  // LIVE STATUS REPORT LIST
                  return ListView.builder(
                    padding: const EdgeInsets.all(12),
                    itemCount: filteredReports.length,
                    itemBuilder: (context, index) {
                      final FieldReport report = filteredReports[index];
                      final statusColor = _getStatusColor(report.status);
                      final urgencyColor = _getUrgencyColor(report.urgencyLevel);

                      return Card(
                        margin: const EdgeInsets.only(bottom: 12),
                        elevation: 2,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12),
                          side: BorderSide(color: Colors.grey.shade200),
                        ),
                        child: Padding(
                          padding: const EdgeInsets.all(16.0),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              // Top Row: Need Type Badge & Status Badge
                              Row(
                                mainAxisAlignment: MainAxisAlignment.between,
                                children: [
                                  Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                    decoration: BoxDecoration(
                                      color: Theme.of(context).colorScheme.primaryContainer,
                                      borderRadius: BorderRadius.circular(6),
                                    ),
                                    child: Text(
                                      report.needType,
                                      style: TextStyle(
                                        fontWeight: FontWeight.bold,
                                        fontSize: 13,
                                        color: Theme.of(context).colorScheme.onPrimaryContainer,
                                      ),
                                    ),
                                  ),

                                  // Status Badge (New, Triaged, InPlan, Resolved)
                                  Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                    decoration: BoxDecoration(
                                      color: statusColor.withOpacity(0.12),
                                      borderRadius: BorderRadius.circular(20),
                                      border: Border.all(color: statusColor, width: 1.5),
                                    ),
                                    child: Row(
                                      mainAxisSize: MainAxisSize.min,
                                      children: [
                                        Container(
                                          width: 8,
                                          height: 8,
                                          decoration: BoxDecoration(
                                            color: statusColor,
                                            shape: BoxShape.circle,
                                          ),
                                        ),
                                        const SizedBox(width: 6),
                                        Text(
                                          report.status.toUpperCase(),
                                          style: TextStyle(
                                            fontWeight: FontWeight.bold,
                                            fontSize: 11,
                                            color: statusColor,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 12),

                              // Main Report Quantity & Shelter ID
                              Row(
                                mainAxisAlignment: MainAxisAlignment.between,
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        'Quantity: ${report.quantityNeeded} units',
                                        style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                                      ),
                                      const SizedBox(height: 4),
                                      Text(
                                        'Shelter #${report.shelterId}',
                                        style: TextStyle(fontSize: 13, color: Colors.grey.shade700),
                                      ),
                                    ],
                                  ),

                                  // Urgency Pill
                                  Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                                    decoration: BoxDecoration(
                                      color: urgencyColor.withOpacity(0.1),
                                      borderRadius: BorderRadius.circular(8),
                                    ),
                                    child: Text(
                                      'Score: ${report.urgencyLevel}/100',
                                      style: TextStyle(
                                        fontWeight: FontWeight.bold,
                                        fontSize: 12,
                                        color: urgencyColor,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 12),

                              // GPS & Media Metadata Footer
                              Container(
                                padding: const EdgeInsets.all(8),
                                decoration: BoxDecoration(
                                  color: Colors.grey.shade50,
                                  borderRadius: BorderRadius.circular(8),
                                ),
                                child: Row(
                                  children: [
                                    const Icon(Icons.location_on_outlined, size: 16, color: Colors.slate),
                                    const SizedBox(width: 4),
                                    Expanded(
                                      child: Text(
                                        report.gpsLat != null
                                            ? 'Lat: ${report.gpsLat!.toStringAsFixed(4)}, Lng: ${report.gpsLng!.toStringAsFixed(4)}'
                                            : 'GPS Location Unavailable',
                                        style: const TextStyle(fontSize: 11, fontFamily: 'monospace', color: Colors.slate),
                                      ),
                                    ),
                                    if (report.photoUrl != null) ...[
                                      const Icon(Icons.photo_camera_outlined, size: 16, color: Colors.blueAccent),
                                      const SizedBox(width: 4),
                                      const Text(
                                        'Photo Attached',
                                        style: TextStyle(fontSize: 11, color: Colors.blueAccent, fontWeight: FontWeight.w600),
                                      ),
                                    ],
                                  ],
                                ),
                              ),
                            ],
                          ),
                        ),
                      );
                    },
                  );
                },
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildFilterChip(String value, String label) {
    final bool isSelected = _selectedStatusFilter == value;
    return Padding(
      padding: const EdgeInsets.only(right: 8.0),
      child: FilterChip(
        selected: isSelected,
        label: Text(label),
        labelStyle: TextStyle(
          color: isSelected ? Colors.white : Colors.black87,
          fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
          fontSize: 12,
        ),
        selectedColor: Theme.of(context).colorScheme.primary,
        backgroundColor: Colors.grey.shade100,
        onSelected: (selected) {
          setState(() {
            _selectedStatusFilter = value;
          });
        },
      ),
    );
  }
}
