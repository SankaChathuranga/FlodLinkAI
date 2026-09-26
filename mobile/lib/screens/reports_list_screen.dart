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
    if (urgency >= 80) return Colors.red.shade700;
    if (urgency >= 50) return Colors.orange.shade700;
    return Colors.green.shade700;
  }

  @override
  Widget build(BuildContext context) {
    final reportProvider = context.watch<ReportProvider>();

    return Scaffold(
      appBar: AppBar(
        title: const Text(
          'FloodLink Field Reports',
          style: TextStyle(fontWeight: FontWeight.bold, color: Colors.white),
        ),
        backgroundColor: Theme.of(context).colorScheme.primary,
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
        backgroundColor: Theme.of(context).colorScheme.primary,
        foregroundColor: Colors.white,
      ),
      body: RefreshIndicator(
        onRefresh: () => reportProvider.fetchReports(),
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
                      const Icon(Icons.cloud_off, size: 64, color: Colors.redAccent),
                      const SizedBox(height: 16),
                      const Text(
                        'Failed to Load Reports',
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
                        onPressed: () => reportProvider.fetchReports(),
                        icon: const Icon(Icons.refresh),
                        label: const Text('Retry Connection'),
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
                    CircularProgressIndicator(),
                    SizedBox(height: 16),
                    Text('Loading field reports...'),
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
                        Icon(Icons.assignment_outlined, size: 64, color: Colors.grey),
                        SizedBox(height: 16),
                        Text(
                          'No Field Reports Found',
                          style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                        ),
                        SizedBox(height: 8),
                        Text(
                          'Tap "+ New Report" to submit your first report.',
                          style: TextStyle(color: Colors.grey),
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

                return Card(
                  margin: const EdgeInsets.only(bottom: 12),
                  elevation: 2,
                  child: Padding(
                    padding: const EdgeInsets.all(14.0),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
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
                                style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 12),
                              ),
                            ),
                            Container(
                              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                              decoration: BoxDecoration(
                                color: _getUrgencyColor(report.urgencyLevel).withOpacity(0.15),
                                borderRadius: BorderRadius.circular(6),
                                border: Border.all(color: _getUrgencyColor(report.urgencyLevel)),
                              ),
                              child: Text(
                                'Urgency: ${report.urgencyLevel}/100',
                                style: TextStyle(
                                  fontWeight: FontWeight.bold,
                                  fontSize: 11,
                                  color: _getUrgencyColor(report.urgencyLevel),
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 10),
                        Text(
                          'Quantity Required: ${report.quantityNeeded} units',
                          style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          'Shelter #${report.shelterId} • Status: ${report.status}',
                          style: const TextStyle(fontSize: 12, color: Colors.grey),
                        ),
                        if (report.gpsLat != null) ...[
                          const SizedBox(height: 4),
                          Text(
                            'GPS: Lat ${report.gpsLat!.toStringAsFixed(4)}, Lng ${report.gpsLng!.toStringAsFixed(4)}',
                            style: const TextStyle(fontSize: 11, fontFamily: 'monospace', color: Colors.blueGrey),
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
