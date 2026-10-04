// ── FloodLink AI — Inventory Dashboard ───────────────────────────────────────
//
// Carbon DataTable-based inventory view.
// Uses WorkflowStateBadge for status and fl-badge for stock level indicators.
// Mono font for Item IDs and coordinates per spec.
//
// Backend contract (when integrated):
//   GET /api/inventory  →  InventoryItem[]
// ─────────────────────────────────────────────────────────────────────────────

import { useState } from 'react';
import {
  DataTable,
  Table,
  TableHead,
  TableRow,
  TableHeader,
  TableBody,
  TableCell,
  TableContainer,
  TableToolbarSearch,
  Button,
  Tag,
} from '@carbon/react';
import { Add, Download } from '@carbon/icons-react';

// ── Types ─────────────────────────────────────────────────────────────────────

interface InventoryItem {
  id: string;
  name: string;
  category: 'Food' | 'Medical' | 'Shelter' | 'Water' | 'Clothing' | 'Other';
  depot: string;
  quantity: number;
  unit: string;
  minThreshold: number;
  lastUpdated: string;
}

// ── Mock data (replace with API call when backend is ready) ───────────────────

const MOCK_INVENTORY: InventoryItem[] = [
  { id: 'INV-001', name: 'Rice (25kg bags)',     category: 'Food',    depot: 'Colombo Central',  quantity: 340,  unit: 'bags',  minThreshold: 100, lastUpdated: '2026-10-04' },
  { id: 'INV-002', name: 'Drinking Water (5L)',  category: 'Water',   depot: 'Galle South',      quantity: 18,   unit: 'units', minThreshold: 50,  lastUpdated: '2026-10-03' },
  { id: 'INV-003', name: 'First Aid Kit',        category: 'Medical', depot: 'Kandy North',      quantity: 95,   unit: 'kits',  minThreshold: 30,  lastUpdated: '2026-10-04' },
  { id: 'INV-004', name: 'Emergency Blankets',   category: 'Shelter', depot: 'Colombo Central',  quantity: 210,  unit: 'units', minThreshold: 75,  lastUpdated: '2026-10-02' },
  { id: 'INV-005', name: 'ORS Sachets',          category: 'Medical', depot: 'Galle South',      quantity: 12,   unit: 'boxes', minThreshold: 40,  lastUpdated: '2026-10-04' },
  { id: 'INV-006', name: 'Tarpaulin Sheets',     category: 'Shelter', depot: 'Kandy North',      quantity: 58,   unit: 'rolls', minThreshold: 20,  lastUpdated: '2026-10-01' },
  { id: 'INV-007', name: 'Children\'s Clothing', category: 'Clothing',depot: 'Colombo Central',  quantity: 3,    unit: 'bales', minThreshold: 10,  lastUpdated: '2026-10-04' },
  { id: 'INV-008', name: 'Cooking Oil (1L)',     category: 'Food',    depot: 'Galle South',      quantity: 190,  unit: 'bottles',minThreshold: 80, lastUpdated: '2026-10-03' },
];

const HEADERS = [
  { key: 'id',          header: 'Item ID' },
  { key: 'name',        header: 'Item' },
  { key: 'category',    header: 'Category' },
  { key: 'depot',       header: 'Depot' },
  { key: 'stock',       header: 'Stock' },
  { key: 'lastUpdated', header: 'Last Updated' },
];

const CATEGORY_COLORS: Record<InventoryItem['category'], 'blue' | 'teal' | 'red' | 'cyan' | 'purple' | 'gray'> = {
  Food:     'teal',
  Medical:  'red',
  Shelter:  'blue',
  Water:    'cyan',
  Clothing: 'purple',
  Other:    'gray',
};

function StockCell({ item }: { item: InventoryItem }) {
  const isLow = item.quantity < item.minThreshold;
  const variant = isLow ? 'error' : 'success';

  return (
    <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
      <span style={{ color: isLow ? 'var(--state-error)' : 'var(--state-success)', fontWeight: 600 }}>
        {item.quantity.toLocaleString()} {item.unit}
      </span>
      {isLow && (
        <span className={`fl-badge fl-badge--${variant}`} aria-label="Low stock">
          <span className="fl-badge__dot" aria-hidden="true" />
          Low
        </span>
      )}
    </span>
  );
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function InventoryDashboard() {
  const [filterText, setFilterText] = useState('');
  const [categoryFilter, setCategoryFilter] = useState<string>('All');

  const categories = ['All', 'Food', 'Medical', 'Shelter', 'Water', 'Clothing', 'Other'];

  const filtered = MOCK_INVENTORY.filter(item => {
    const matchesText = item.name.toLowerCase().includes(filterText.toLowerCase()) ||
      item.id.toLowerCase().includes(filterText.toLowerCase()) ||
      item.depot.toLowerCase().includes(filterText.toLowerCase());
    const matchesCat = categoryFilter === 'All' || item.category === categoryFilter;
    return matchesText && matchesCat;
  });

  // Stats
  const lowStockCount = MOCK_INVENTORY.filter(i => i.quantity < i.minThreshold).length;
  const totalItems    = MOCK_INVENTORY.length;
  const depotCount    = new Set(MOCK_INVENTORY.map(i => i.depot)).size;

  // Prepare rows for Carbon DataTable
  const rows = filtered.map(item => ({
    id:          item.id,
    _raw:        item,
    name:        item.name,
    category:    item.category,
    depot:       item.depot,
    stock:       item.quantity,     // numeric for sorting
    lastUpdated: item.lastUpdated,
  }));

  return (
    <div className="fl-page">
      {/* ── Page Header ─────────────────────────────────────────────────── */}
      <header className="fl-page-header">
        <div>
          <p className="fl-page-header__eyebrow">Resource Management</p>
          <h1 className="fl-page-header__title">Inventory</h1>
          <p className="fl-page-header__subtitle">
            Relief supply stock levels across all depots
          </p>
        </div>
        <div className="fl-page-header__actions">
          <Button
            kind="ghost"
            renderIcon={Download}
            iconDescription="Export CSV"
            hasIconOnly
            tooltipPosition="bottom"
            aria-label="Export inventory as CSV"
          />
          <Button renderIcon={Add} kind="primary">
            Add Item
          </Button>
        </div>
      </header>

      {/* ── Stat Tiles ──────────────────────────────────────────────────── */}
      <div className="fl-stat-row" aria-label="Inventory summary">
        <div className="fl-stat-tile">
          <span className="fl-stat-tile__label">Total Items</span>
          <span className="fl-stat-tile__value">{totalItems}</span>
          <span className="fl-stat-tile__delta">Across {depotCount} depots</span>
        </div>
        <div className={`fl-stat-tile${lowStockCount > 0 ? ' fl-stat-tile--error' : ' fl-stat-tile--success'}`}>
          <span className="fl-stat-tile__label">Low Stock Alerts</span>
          <span className="fl-stat-tile__value">{lowStockCount}</span>
          <span className="fl-stat-tile__delta">Below minimum threshold</span>
        </div>
        <div className="fl-stat-tile fl-stat-tile--info">
          <span className="fl-stat-tile__label">Active Depots</span>
          <span className="fl-stat-tile__value">{depotCount}</span>
          <span className="fl-stat-tile__delta">Reporting stock</span>
        </div>
        <div className="fl-stat-tile">
          <span className="fl-stat-tile__label">Categories</span>
          <span className="fl-stat-tile__value">{categories.length - 1}</span>
          <span className="fl-stat-tile__delta">Tracked supply types</span>
        </div>
      </div>

      {/* ── Toolbar ─────────────────────────────────────────────────────── */}
      <div className="fl-toolbar" role="search" aria-label="Filter inventory">
        <div className="fl-toolbar__search">
          <TableToolbarSearch
            id="inventory-search"
            value={filterText}
            onChange={(_event, value) => setFilterText(value ?? '')}
            placeholder="Search items, depots, IDs…"
            persistent
          />
        </div>
        <div className="fl-toolbar__filters" role="group" aria-label="Category filter">
          {categories.map(cat => (
            <button
              key={cat}
              onClick={() => setCategoryFilter(cat)}
              aria-pressed={categoryFilter === cat}
              style={{
                padding: '4px 12px',
                border: '1px solid',
                borderColor: categoryFilter === cat ? 'var(--accent-primary)' : 'var(--border-default)',
                borderRadius: 'var(--radius-sm)',
                background: categoryFilter === cat ? 'var(--accent-primary)' : 'transparent',
                color: categoryFilter === cat ? '#fff' : 'var(--text-primary)',
                font: 'inherit',
                fontSize: 12,
                cursor: 'pointer',
                transition: 'background 70ms, border-color 70ms, color 70ms',
              }}
            >
              {cat}
            </button>
          ))}
        </div>
        <span className="fl-toolbar__count" aria-live="polite">
          {filtered.length} / {totalItems} items
        </span>
      </div>

      {/* ── Data Table ──────────────────────────────────────────────────── */}
      <DataTable rows={rows} headers={HEADERS} isSortable>
        {({
          rows: tableRows,
          headers,
          getHeaderProps,
          getRowProps,
          getTableProps,
          getTableContainerProps,
        }) => (
          <TableContainer {...getTableContainerProps()}>
            <Table {...getTableProps()} aria-label="Inventory table">
              <TableHead>
                <TableRow>
                  {headers.map(header => (
                    <TableHeader {...getHeaderProps({ header })}>
                      {header.header}
                    </TableHeader>
                  ))}
                </TableRow>
              </TableHead>
              <TableBody>
                {tableRows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={HEADERS.length}>
                      <div className="fl-state-message">No items match your filters.</div>
                    </TableCell>
                  </TableRow>
                ) : (
                  tableRows.map(row => {
                    const raw = filtered.find(i => i.id === row.id);
                    const isLow = raw ? raw.quantity < raw.minThreshold : false;

                    return (
                      <TableRow
                        {...getRowProps({ row })}
                        className={isLow ? 'fl-row--low-stock' : ''}
                      >
                        {row.cells.map(cell => {
                          // Item ID — mono font per spec
                          if (cell.info.header === 'id') {
                            return (
                              <TableCell key={cell.id}>
                                <span className="fl-cell--mono">{cell.value}</span>
                              </TableCell>
                            );
                          }

                          // Category — Carbon Tag
                          if (cell.info.header === 'category' && raw) {
                            return (
                              <TableCell key={cell.id}>
                                <Tag type={CATEGORY_COLORS[raw.category]} size="sm">
                                  {raw.category}
                                </Tag>
                              </TableCell>
                            );
                          }

                          // Stock — custom cell with badge
                          if (cell.info.header === 'stock' && raw) {
                            return (
                              <TableCell key={cell.id}>
                                <StockCell item={raw} />
                              </TableCell>
                            );
                          }

                          return <TableCell key={cell.id}>{cell.value}</TableCell>;
                        })}
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </DataTable>
    </div>
  );
}
