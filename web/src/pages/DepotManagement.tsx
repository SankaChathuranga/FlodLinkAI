// ── FloodLink AI — Depot Management ─────────────────────────────────────────
//
// Carbon DataTable + Tile view of registered depots.
// Provides add/edit capability via a Carbon Modal form.
//
// Backend contract (when integrated):
//   GET    /api/depots         → Depot[]
//   POST   /api/depots         → Depot
//   PUT    /api/depots/{id}    → Depot
//   DELETE /api/depots/{id}    → 204
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
  Button,
  Modal,
  TextInput,
  Select,
  SelectItem,
  Tag,
  InlineNotification,
} from '@carbon/react';
import { Add, Edit, Location } from '@carbon/icons-react';

// ── Types ─────────────────────────────────────────────────────────────────────

type DepotStatus = 'Active' | 'Inactive' | 'Full';

interface Depot {
  id: string;
  name: string;
  district: string;
  province: string;
  /** Format: "lat, lng" — displayed in mono font */
  coordinates: string;
  status: DepotStatus;
  capacity: number;
  usedCapacity: number;
}

// ── Mock data ─────────────────────────────────────────────────────────────────

const MOCK_DEPOTS: Depot[] = [
  { id: 'DEP-001', name: 'Colombo Central Depot',  district: 'Colombo',   province: 'Western',   coordinates: '6.9271, 79.8612',  status: 'Active',   capacity: 1000, usedCapacity: 680 },
  { id: 'DEP-002', name: 'Galle Southern Hub',     district: 'Galle',     province: 'Southern',  coordinates: '6.0535, 80.2210',  status: 'Active',   capacity: 600,  usedCapacity: 590 },
  { id: 'DEP-003', name: 'Kandy Northern Store',   district: 'Kandy',     province: 'Central',   coordinates: '7.2906, 80.6337',  status: 'Active',   capacity: 800,  usedCapacity: 310 },
  { id: 'DEP-004', name: 'Jaffna Relief Point',    district: 'Jaffna',    province: 'Northern',  coordinates: '9.6615, 80.0255',  status: 'Inactive', capacity: 400,  usedCapacity: 0   },
  { id: 'DEP-005', name: 'Hambantota Overflow',    district: 'Hambantota',province: 'Southern',  coordinates: '6.1241, 81.1185',  status: 'Full',     capacity: 500,  usedCapacity: 500 },
];

const HEADERS = [
  { key: 'id',          header: 'Depot ID' },
  { key: 'name',        header: 'Name' },
  { key: 'district',    header: 'District' },
  { key: 'province',    header: 'Province' },
  { key: 'coordinates', header: 'Coordinates' },
  { key: 'capacity',    header: 'Capacity' },
  { key: 'status',      header: 'Status' },
  { key: 'actions',     header: '' },
];

const STATUS_TAG_TYPE: Record<DepotStatus, 'green' | 'gray' | 'red'> = {
  Active:   'green',
  Inactive: 'gray',
  Full:     'red',
};

// ── Capacity bar ──────────────────────────────────────────────────────────────

function CapacityBar({ depot }: { depot: Depot }) {
  const pct = Math.round((depot.usedCapacity / depot.capacity) * 100);
  const color = pct >= 95 ? 'var(--state-error)' : pct >= 80 ? 'var(--state-warning)' : 'var(--state-success)';

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 3, minWidth: 100 }}>
      <div
        role="progressbar"
        aria-valuenow={pct}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={`${pct}% capacity used`}
        style={{
          height: 4,
          background: 'var(--border-default)',
          borderRadius: 2,
          overflow: 'hidden',
        }}
      >
        <div style={{ width: `${pct}%`, height: '100%', background: color, transition: 'width 300ms' }} />
      </div>
      <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>
        {depot.usedCapacity.toLocaleString()} / {depot.capacity.toLocaleString()} units · {pct}%
      </span>
    </div>
  );
}

// ── Add/Edit Modal ────────────────────────────────────────────────────────────

interface DepotFormState {
  name: string;
  district: string;
  province: string;
  coordinates: string;
  capacity: string;
  status: DepotStatus;
}

const EMPTY_FORM: DepotFormState = {
  name: '', district: '', province: '', coordinates: '', capacity: '', status: 'Active',
};

function DepotModal({
  open,
  editing,
  onClose,
  onSave,
}: {
  open: boolean;
  editing: Depot | null;
  onClose: () => void;
  onSave: (data: DepotFormState) => void;
}) {
  const [form, setForm] = useState<DepotFormState>(
    editing
      ? { name: editing.name, district: editing.district, province: editing.province, coordinates: editing.coordinates, capacity: String(editing.capacity), status: editing.status }
      : EMPTY_FORM
  );
  const [error, setError] = useState('');

  function handleSubmit() {
    if (!form.name.trim() || !form.district.trim() || !form.capacity) {
      setError('Name, district, and capacity are required.');
      return;
    }
    if (isNaN(Number(form.capacity)) || Number(form.capacity) <= 0) {
      setError('Capacity must be a positive number.');
      return;
    }
    setError('');
    onSave(form);
  }

  function field(key: keyof DepotFormState) {
    return (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
      setForm(prev => ({ ...prev, [key]: e.target.value }));
  }

  return (
    <Modal
      open={open}
      modalHeading={editing ? `Edit ${editing.name}` : 'Add New Depot'}
      primaryButtonText={editing ? 'Save Changes' : 'Create Depot'}
      secondaryButtonText="Cancel"
      onRequestSubmit={handleSubmit}
      onRequestClose={onClose}
      onSecondarySubmit={onClose}
      size="md"
    >
      {error && (
        <div style={{ marginBottom: 16 }}>
          <InlineNotification
            kind="error"
            title="Validation error:"
            subtitle={error}
            lowContrast
            hideCloseButton
          />
        </div>
      )}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
        <div style={{ gridColumn: '1 / -1' }}>
          <TextInput
            id="depot-name"
            labelText="Depot name"
            value={form.name}
            onChange={field('name')}
            placeholder="e.g. Colombo Central Depot"
          />
        </div>
        <TextInput
          id="depot-district"
          labelText="District"
          value={form.district}
          onChange={field('district')}
          placeholder="e.g. Colombo"
        />
        <TextInput
          id="depot-province"
          labelText="Province"
          value={form.province}
          onChange={field('province')}
          placeholder="e.g. Western"
        />
        <TextInput
          id="depot-coords"
          labelText="Coordinates"
          helperText="Format: lat, lng — e.g. 6.9271, 79.8612"
          value={form.coordinates}
          onChange={field('coordinates')}
          placeholder="6.9271, 79.8612"
          style={{ fontFamily: 'var(--font-mono)' }}
        />
        <TextInput
          id="depot-capacity"
          labelText="Capacity (units)"
          value={form.capacity}
          onChange={field('capacity')}
          type="number"
          min="1"
          placeholder="e.g. 1000"
        />
        <div style={{ gridColumn: '1 / -1' }}>
          <Select
            id="depot-status"
            labelText="Status"
            value={form.status}
            onChange={field('status')}
          >
            <SelectItem value="Active"   text="Active" />
            <SelectItem value="Inactive" text="Inactive" />
            <SelectItem value="Full"     text="Full" />
          </Select>
        </div>
      </div>
    </Modal>
  );
}

// ── Main Component ────────────────────────────────────────────────────────────

export default function DepotManagement() {
  const [depots, setDepots] = useState<Depot[]>(MOCK_DEPOTS);
  const [modalOpen, setModalOpen] = useState(false);
  const [editingDepot, setEditingDepot] = useState<Depot | null>(null);

  function openAdd()          { setEditingDepot(null); setModalOpen(true); }
  function openEdit(d: Depot) { setEditingDepot(d); setModalOpen(true); }
  function closeModal()       { setModalOpen(false); setEditingDepot(null); }

  function handleSave(data: DepotFormState) {
    if (editingDepot) {
      setDepots(prev => prev.map(d => d.id === editingDepot.id
        ? { ...d, ...data, capacity: Number(data.capacity) }
        : d
      ));
    } else {
      const newDepot: Depot = {
        id: `DEP-${String(depots.length + 1).padStart(3, '0')}`,
        name:         data.name,
        district:     data.district,
        province:     data.province,
        coordinates:  data.coordinates,
        capacity:     Number(data.capacity),
        usedCapacity: 0,
        status:       data.status,
      };
      setDepots(prev => [...prev, newDepot]);
    }
    closeModal();
  }

  const rows = depots.map(d => ({
    id:          d.id,
    name:        d.name,
    district:    d.district,
    province:    d.province,
    coordinates: d.coordinates,
    capacity:    d.capacity,
    status:      d.status,
    actions:     '',
    _raw:        d,
  }));

  const activeCount   = depots.filter(d => d.status === 'Active').length;
  const fullCount     = depots.filter(d => d.status === 'Full').length;
  const inactiveCount = depots.filter(d => d.status === 'Inactive').length;

  return (
    <div className="fl-page">
      {/* ── Page Header ─────────────────────────────────────────────────── */}
      <header className="fl-page-header">
        <div>
          <p className="fl-page-header__eyebrow">Resource Management</p>
          <h1 className="fl-page-header__title">Depot Management</h1>
          <p className="fl-page-header__subtitle">
            Relief supply storage locations across Sri Lanka
          </p>
        </div>
        <div className="fl-page-header__actions">
          <Button renderIcon={Add} kind="primary" onClick={openAdd}>
            Add Depot
          </Button>
        </div>
      </header>

      {/* ── Stat Tiles ──────────────────────────────────────────────────── */}
      <div className="fl-stat-row" aria-label="Depot summary">
        <div className="fl-stat-tile fl-stat-tile--success">
          <span className="fl-stat-tile__label">Active</span>
          <span className="fl-stat-tile__value">{activeCount}</span>
          <span className="fl-stat-tile__delta">Accepting stock</span>
        </div>
        <div className={`fl-stat-tile${fullCount > 0 ? ' fl-stat-tile--error' : ''}`}>
          <span className="fl-stat-tile__label">Full</span>
          <span className="fl-stat-tile__value">{fullCount}</span>
          <span className="fl-stat-tile__delta">At capacity</span>
        </div>
        <div className="fl-stat-tile">
          <span className="fl-stat-tile__label">Inactive</span>
          <span className="fl-stat-tile__value">{inactiveCount}</span>
          <span className="fl-stat-tile__delta">Not in use</span>
        </div>
        <div className="fl-stat-tile">
          <span className="fl-stat-tile__label">Total</span>
          <span className="fl-stat-tile__value">{depots.length}</span>
          <span className="fl-stat-tile__delta">Registered depots</span>
        </div>
      </div>

      {/* ── Data Table ──────────────────────────────────────────────────── */}
      <DataTable rows={rows} headers={HEADERS} isSortable>
        {({ rows: tableRows, headers, getHeaderProps, getRowProps, getTableProps, getTableContainerProps }) => (
          <TableContainer {...getTableContainerProps()}>
            <Table {...getTableProps()} aria-label="Depots table">
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
                {tableRows.map(row => {
                  const raw = depots.find(d => d.id === row.id)!;
                  return (
                    <TableRow {...getRowProps({ row })}>
                      {row.cells.map(cell => {
                        if (cell.info.header === 'id') {
                          return (
                            <TableCell key={cell.id}>
                              <span className="fl-cell--mono">{cell.value}</span>
                            </TableCell>
                          );
                        }

                        if (cell.info.header === 'coordinates') {
                          return (
                            <TableCell key={cell.id}>
                              <span className="fl-cell--mono">
                                <Location size={12} style={{ marginRight: 4, verticalAlign: 'middle' }} />
                                {cell.value}
                              </span>
                            </TableCell>
                          );
                        }

                        if (cell.info.header === 'capacity') {
                          return (
                            <TableCell key={cell.id}>
                              <CapacityBar depot={raw} />
                            </TableCell>
                          );
                        }

                        if (cell.info.header === 'status') {
                          return (
                            <TableCell key={cell.id}>
                              <Tag type={STATUS_TAG_TYPE[raw.status]} size="sm">
                                {raw.status}
                              </Tag>
                            </TableCell>
                          );
                        }

                        if (cell.info.header === 'actions') {
                          return (
                            <TableCell key={cell.id}>
                              <Button
                                kind="ghost"
                                size="sm"
                                renderIcon={Edit}
                                iconDescription="Edit depot"
                                hasIconOnly
                                tooltipPosition="left"
                                onClick={() => openEdit(raw)}
                                aria-label={`Edit ${raw.name}`}
                              />
                            </TableCell>
                          );
                        }

                        return <TableCell key={cell.id}>{cell.value}</TableCell>;
                      })}
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </DataTable>

      {/* ── Add/Edit Modal ───────────────────────────────────────────────── */}
      <DepotModal
        open={modalOpen}
        editing={editingDepot}
        onClose={closeModal}
        onSave={handleSave}
      />
    </div>
  );
}
