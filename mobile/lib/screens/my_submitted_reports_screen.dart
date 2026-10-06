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
      case 'triaged':
      case 'inplan':
      case 'in-plan':
        return const Color(0xFF0043CE); // --state-info
      case 'resolved':
      case 'approved':
        return const Color(0xFF198038); // --state-success
      case 'pendingapproval':
      case 'revisionrequested':
        return const Color(0xFFF1C21B); // --state-warning
      case 'rejected':
      case 'failed':
        return const Color(0xFFDA1E28); // --state-error
      default:
        return const Color(0xFF6F6F6F); // --text-muted
    }
  }

  Color _getUrgencyColor(int urgency) {
    if (urgency >= 80) return const Color(0xFFDA1E28); // --state-error
    if (urgency >= 50) return const Color(0xFFF1C21B); // --state-warning
    return const Color(0xFF198038); // --state-success
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
      backgroundColor: const Color(0xFFF4F4F4), // --bg-base
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
        backgroundColor: const Color(0xFF0F62FE), // --accent-primary
        elevation: 0,
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
        backgroundColor: const Color(0xFF0F62FE), // --accent-primary
        foregroundColor: Colors.white,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(4), // rounded-sm
        ),
      ),
      body: Column(
        children: [
          // Status Filter Tabs
          Container(
            color: const Color(0xFFFFFFFF), // --bg-surface
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
          const Divider(height: 1, thickness: 1, color: Color(0xFFC6C6C6)), // --border-default

          // Main Content
          Expanded(
            child: RefreshIndicator(
              onRefresh: () async => _loadReports(),
              color: const Color(0xFF0F62FE),
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
                            const Icon(Icons.cloud_off_rounded, size: 64, color: Color(0xFFDA1E28)), // --state-error
                            const SizedBox(height: 16),
                            const Text(
                              'Connection Error',
                              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Color(0xFF161616)),
                            ),
                            const SizedBox(height: 8),
                            Text(
                              reportProvider.reportsError!,
                              textAlign: TextAlign.center,
                              style: const TextStyle(color: Color(0xFF6F6F6F)), // --text-muted
                            ),
                            const SizedBox(height: 24),
                            ElevatedButton.icon(
                              onPressed: _loadReports,
                              icon: const Icon(Icons.refresh),
                              label: const Text('Retry Connection'),
                              style: ElevatedButton.styleFrom(
                                backgroundColor: const Color(0xFF0F62FE),
                                foregroundColor: Colors.white,
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(4),
                                ),
                              ),
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
                          CircularProgressIndicator(color: Color(0xFF0F62FE)),
                          SizedBox(height: 16),
                          Text(
                            'Fetching your field report updates...',
                            style: TextStyle(color: Color(0xFF6F6F6F)),
                          ),
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
                              const Icon(Icons.assignment_turned_in_outlined, size: 64, color: Color(0xFF6F6F6F)),
                              const SizedBox(height: 16),
                              Text(
                                _selectedStatusFilter == 'ALL'
                                    ? 'No Submitted Reports Yet'
                                    : 'No Reports with Status "$_selectedStatusFilter"',
                                style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Color(0xFF161616)),
                              ),
                              const SizedBox(height: 8),
                              const Text(
                                'Tap "+ New Report" to record emergency needs.',
                                style: TextStyle(color: Color(0xFF6F6F6F)),
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

                      return Container(
                        margin: const EdgeInsets.only(bottom: 12),
                        decoration: BoxDecoration(
                          color: const Color(0xFFFFFFFF), // --bg-surface
                          borderRadius: BorderRadius.circular(6), // rounded-md
                          border: Border.all(color: const Color(0xFFC6C6C6), width: 1), // --border-default
                        ),
                        child: Padding(
                          padding: const EdgeInsets.all(16.0),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              // Top Row: Need Type Badge & Status Badge
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                    decoration: BoxDecoration(
                                      color: const Color(0xFFF4F4F4),
                                      borderRadius: BorderRadius.circular(16), // rounded-full (Tag)
                                      border: Border.all(color: const Color(0xFFC6C6C6)),
                                    ),
                                    child: Text(
                                      report.needType,
                                      style: const TextStyle(
                                        fontWeight: FontWeight.bold,
                                        fontSize: 13,
                                        color: Color(0xFF161616),
                                      ),
                                    ),
                                  ),

                                  // Status Badge (New, Triaged, InPlan, Resolved)
                                  Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                    decoration: BoxDecoration(
                                      color: statusColor.withValues(alpha: 0.12),
                                      borderRadius: BorderRadius.circular(16), // rounded-full
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
                                            color: statusColor == const Color(0xFFF1C21B)
                                                ? const Color(0xFF161616) // Warning contrast rule
                                                : statusColor,
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
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        'Quantity: ${report.quantityNeeded} units',
                                        style: const TextStyle(
                                          fontSize: 16,
                                          fontWeight: FontWeight.bold,
                                          color: Color(0xFF161616),
                                        ),
                                      ),
                                      const SizedBox(height: 4),
                                      Text(
                                        'Shelter #${report.shelterId}',
                                        style: const TextStyle(fontSize: 13, color: Color(0xFF6F6F6F)),
                                      ),
                                    ],
                                  ),

                                  // Urgency Pill
                                  Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                                    decoration: BoxDecoration(
                                      color: urgencyColor.withValues(alpha: 0.1),
                                      borderRadius: BorderRadius.circular(16), // rounded-full
                                      border: Border.all(color: urgencyColor),
                                    ),
                                    child: Text(
                                      'Score: ${report.urgencyLevel}/100',
                                      style: TextStyle(
                                        fontWeight: FontWeight.bold,
                                        fontSize: 12,
                                        color: urgencyColor == const Color(0xFFF1C21B)
                                            ? const Color(0xFF161616) // Warning contrast rule
                                            : urgencyColor,
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
                                  color: const Color(0xFFF4F4F4),
                                  borderRadius: BorderRadius.circular(4),
                                  border: Border.all(color: const Color(0xFFC6C6C6)),
                                ),
                                child: Row(
                                  children: [
                                    const Icon(Icons.location_on_outlined, size: 16, color: Color(0xFF6F6F6F)),
                                    const SizedBox(width: 4),
                                    Expanded(
                                      child: Text(
                                        report.gpsLat != null
                                            ? 'Lat: ${report.gpsLat!.toStringAsFixed(4)}, Lng: ${report.gpsLng!.toStringAsFixed(4)}'
                                            : 'GPS Location Unavailable',
                                        style: const TextStyle(
                                          fontSize: 11,
                                          fontFamily: 'IBM Plex Mono', // --font-mono
                                          color: Color(0xFF6F6F6F),
                                        ),
                                      ),
                                    ),
                                    if (report.photoUrl != null) ...[
                                      const Icon(Icons.photo_camera_outlined, size: 16, color: Color(0xFF0F62FE)),
                                      const SizedBox(width: 4),
                                      const Text(
                                        'Photo Attached',
                                        style: TextStyle(
                                          fontSize: 11,
                                          color: Color(0xFF0F62FE), // --accent-primary
                                          fontWeight: FontWeight.w600,
                                        ),
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
      child: ChoiceChip(
        selected: isSelected,
        label: Text(label),
        labelStyle: TextStyle(
          color: isSelected ? Colors.white : const Color(0xFF161616),
          fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
          fontSize: 12,
        ),
        selectedColor: const Color(0xFF0F62FE), // --accent-primary
        backgroundColor: const Color(0xFFF4F4F4), // --bg-base
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(16), // rounded-full (Tag style)
          side: BorderSide(
            color: isSelected ? const Color(0xFF0F62FE) : const Color(0xFFC6C6C6),
          ),
        ),
        onSelected: (selected) {
          setState(() {
            _selectedStatusFilter = value;
          });
        },
      ),
    );
  }
}

