import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import '../providers/report_provider.dart';
import '../models/report.dart';

class ReportsListScreen extends StatefulWidget {
  const ReportsListScreen({super.key});

  @override
  State<ReportsListScreen> createState() => _ReportsListScreenState();
}

class _ReportsListScreenState extends State<ReportsListScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<ReportProvider>().fetchReports();
    });
  }

  Color _getUrgencyColor(int urgency) {
    if (urgency >= 80) return const Color(0xFFDA1E28); // --state-error
    if (urgency >= 50) return const Color(0xFFF1C21B); // --state-warning
    return const Color(0xFF198038); // --state-success
  }

  @override
  Widget build(BuildContext context) {
    final reportProvider = context.watch<ReportProvider>();

    return Scaffold(
      backgroundColor: const Color(0xFFF4F4F4), // --bg-base
      appBar: AppBar(
        title: const Text(
          'FloodLink Field Reports',
          style: TextStyle(fontWeight: FontWeight.bold, color: Colors.white),
        ),
        backgroundColor: const Color(0xFF0F62FE), // --accent-primary
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.assignment_ind_outlined, color: Colors.white),
            tooltip: 'My Submitted Reports',
            onPressed: () => context.push('/my-submitted-reports'),
          ),
          IconButton(
            icon: const Icon(Icons.refresh, color: Colors.white),
            onPressed: () => reportProvider.fetchReports(),
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
      body: RefreshIndicator(
        onRefresh: () => reportProvider.fetchReports(),
        color: const Color(0xFF0F62FE),
        child: Builder(
          builder: (context) {
            // ERROR STATE
            if (reportProvider.reportsError != null) {
              return Center(
                child: Padding(
                  padding: const EdgeInsets.all(24.0),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.cloud_off, size: 64, color: Color(0xFFDA1E28)), // --state-error
                      const SizedBox(height: 16),
                      const Text(
                        'Failed to Load Reports',
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
                        onPressed: () => reportProvider.fetchReports(),
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
            if (reportProvider.isLoadingReports) {
              return const Center(
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    CircularProgressIndicator(color: Color(0xFF0F62FE)),
                    SizedBox(height: 16),
                    Text(
                      'Loading field reports...',
                      style: TextStyle(color: Color(0xFF6F6F6F)),
                    ),
                  ],
                ),
              );
            }

            // EMPTY STATE
            if (reportProvider.reports.isEmpty) {
              return ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                children: [
                  SizedBox(height: MediaQuery.of(context).size.height * 0.2),
                  const Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.assignment_outlined, size: 64, color: Color(0xFF6F6F6F)),
                        SizedBox(height: 16),
                        Text(
                          'No Field Reports Found',
                          style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Color(0xFF161616)),
                        ),
                        SizedBox(height: 8),
                        Text(
                          'Tap "+ New Report" to submit your first report.',
                          style: TextStyle(color: Color(0xFF6F6F6F)),
                        ),
                      ],
                    ),
                  ),
                ],
              );
            }

            // REPORT LIST
            return ListView.builder(
              padding: const EdgeInsets.all(12),
              itemCount: reportProvider.reports.length,
              itemBuilder: (context, index) {
                final FieldReport report = reportProvider.reports[index];
                final urgencyColor = _getUrgencyColor(report.urgencyLevel);

                return Container(
                  margin: const EdgeInsets.only(bottom: 12),
                  decoration: BoxDecoration(
                    color: const Color(0xFFFFFFFF), // --bg-surface
                    borderRadius: BorderRadius.circular(6), // rounded-md
                    border: Border.all(color: const Color(0xFFC6C6C6), width: 1), // --border-default
                  ),
                  child: Padding(
                    padding: const EdgeInsets.all(14.0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            // Need Type Tag
                            Container(
                              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                              decoration: BoxDecoration(
                                color: const Color(0xFFF4F4F4),
                                borderRadius: BorderRadius.circular(16), // rounded-full (Tag style)
                                border: Border.all(color: const Color(0xFFC6C6C6)),
                              ),
                              child: Text(
                                report.needType,
                                style: const TextStyle(
                                  fontWeight: FontWeight.bold,
                                  fontSize: 12,
                                  color: Color(0xFF161616),
                                ),
                              ),
                            ),

                            // Urgency Badge
                            Container(
                              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                              decoration: BoxDecoration(
                                color: urgencyColor.withValues(alpha: 0.15),
                                borderRadius: BorderRadius.circular(16), // rounded-full
                                border: Border.all(color: urgencyColor),
                              ),
                              child: Text(
                                'Urgency: ${report.urgencyLevel}/100',
                                style: TextStyle(
                                  fontWeight: FontWeight.bold,
                                  fontSize: 11,
                                  color: urgencyColor == const Color(0xFFF1C21B)
                                      ? const Color(0xFF161616) // Warning contrast rule: dark text on yellow
                                      : urgencyColor,
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 10),
                        Text(
                          'Quantity Required: ${report.quantityNeeded} units',
                          style: const TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                            color: Color(0xFF161616), // --text-primary
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          'Shelter #${report.shelterId} • Status: ${report.status}',
                          style: const TextStyle(fontSize: 12, color: Color(0xFF6F6F6F)), // --text-muted
                        ),
                        if (report.gpsLat != null) ...[
                          const SizedBox(height: 4),
                          Text(
                            'GPS: Lat ${report.gpsLat!.toStringAsFixed(4)}, Lng ${report.gpsLng!.toStringAsFixed(4)}',
                            style: const TextStyle(
                              fontSize: 11,
                              fontFamily: 'IBM Plex Mono', // --font-mono
                              color: Color(0xFF6F6F6F),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                );
              },
            );
          },
        ),
      ),
    );
  }
}

