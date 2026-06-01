import { useState, useEffect, useCallback } from 'react';
import { fetchReadings, ingestReading, type TelemetryReading, type ReadingsFilter, type IngestRequest } from './api';

const DEFAULT_TENANT = import.meta.env.VITE_DEFAULT_TENANT ?? 'acme'; //default tenant ID to use for API requests
function formatDate(iso: string) {
  return new Date(iso).toLocaleString();
}

export default function App() {                                       //main React component for the telemetry UI application
  const [tenantId] = useState(DEFAULT_TENANT);
  const [readings, setReadings] = useState<TelemetryReading[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [filterDevice, setFilterDevice] = useState('');
  const [filterType, setFilterType] = useState('');

  // Submit form state
  const [showForm, setShowForm] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [form, setForm] = useState<IngestRequest>({
    tenantId: DEFAULT_TENANT,
    deviceId: '',
    type: '',
    value: 0,
    unit: '',
    battery: 100,
    signal: -70,
    recordedAt: new Date().toISOString().slice(0, 19) + 'Z',
    externalId: '',
  });

  const loadReadings = useCallback(async (filter: ReadingsFilter = {}) => { //function to load telemetry readings from the API, with optional filtering parameters
    setLoading(true);
    setError(null);
    try {
      const data = await fetchReadings(tenantId, filter);
      setReadings(data.items);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load readings');
    } finally {
      setLoading(false);
    }
  }, [tenantId]);

  useEffect(() => {
    loadReadings();
  }, [loadReadings]);

  function applyFilters() { //function to apply filters to the telemetry readings
    loadReadings({
      deviceId: filterDevice || undefined,
      type: filterType || undefined,
    });
  }

  async function handleSubmit(e: React.FormEvent) { //function to handle form submission for ingesting a new telemetry reading
    e.preventDefault();                     //prevent default form submission behavior
    setSubmitting(true); 
    setFormError(null);
    try {
      await ingestReading(form);                   //call API to ingest the new reading using the form data
      setShowForm(false);
      await loadReadings();
    } catch (e) {
      setFormError(e instanceof Error ? e.message : 'Submit failed');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div style={{ fontFamily: 'system-ui, sans-serif', maxWidth: 1100, margin: '0 auto', padding: '1rem' }}>
      <h1 style={{ fontSize: '1.5rem', marginBottom: '0.25rem' }}>Kallipr Telemetry</h1>
      <p style={{ color: '#666', marginTop: 0 }}>Tenant: <strong>{tenantId}</strong></p>

      {/* Filters */}
      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1rem', flexWrap: 'wrap' }}>
        <input
          placeholder="Device ID"
          value={filterDevice}
          onChange={e => setFilterDevice(e.target.value)}
          style={inputStyle}
        />
        <input
          placeholder="Type"
          value={filterType}
          onChange={e => setFilterType(e.target.value)}
          style={inputStyle}
        />
        <button onClick={applyFilters} style={btnStyle}>Filter</button>
        <button onClick={() => { setFilterDevice(''); setFilterType(''); loadReadings(); }} style={{ ...btnStyle, background: '#6c757d' }}>
          Clear
        </button>
        <button onClick={() => setShowForm(s => !s)} style={{ ...btnStyle, background: '#198754', marginLeft: 'auto' }}>
          {showForm ? 'Cancel' : '+ New Reading'}
        </button>
      </div>

      {/* Submit form */}
      {showForm && (
        <form onSubmit={handleSubmit} style={{ background: '#f8f9fa', padding: '1rem', borderRadius: 6, marginBottom: '1rem' }}>
          <h3 style={{ marginTop: 0 }}>Submit Reading</h3>
          {formError && <p style={{ color: 'red' }}>{formError}</p>}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(160px, 1fr))', gap: '0.5rem' }}>
            {(['deviceId', 'type', 'unit', 'externalId'] as const).map(field => (
              <label key={field} style={{ display: 'flex', flexDirection: 'column', fontSize: '0.85rem' }}>
                {field}
                <input
                  required
                  value={String(form[field])}
                  onChange={e => setForm(f => ({ ...f, [field]: e.target.value }))}
                  style={inputStyle}
                />
              </label>
            ))}
            {(['value', 'battery', 'signal'] as const).map(field => (
              <label key={field} style={{ display: 'flex', flexDirection: 'column', fontSize: '0.85rem' }}>
                {field}
                <input
                  type="number"
                  required
                  value={form[field]}
                  onChange={e => setForm(f => ({ ...f, [field]: Number(e.target.value) }))}
                  style={inputStyle}
                />
              </label>
            ))}
            <label style={{ display: 'flex', flexDirection: 'column', fontSize: '0.85rem' }}>
              recordedAt
              <input
                value={form.recordedAt}
                onChange={e => setForm(f => ({ ...f, recordedAt: e.target.value }))}
                style={inputStyle}
              />
            </label>
          </div>
          <button type="submit" disabled={submitting} style={{ ...btnStyle, marginTop: '0.75rem' }}>
            {submitting ? 'Submitting…' : 'Submit'}
          </button>
        </form>
      )}

      {/* Table */}
      {loading && <p>Loading…</p>}
      {error && <p style={{ color: 'red' }}>Error: {error}</p>}
      {!loading && !error && (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
            <thead>
              <tr style={{ background: '#f1f3f5' }}>
                {['Device ID', 'Type', 'Value', 'Unit', 'Battery', 'Signal', 'Recorded At', 'External ID', 'Status'].map(h => (
                  <th key={h} style={thStyle}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {readings.length === 0 ? (
                <tr><td colSpan={9} style={{ textAlign: 'center', padding: '1rem', color: '#888' }}>No readings found</td></tr>
              ) : readings.map(r => (
                <tr key={r.id} style={{ borderBottom: '1px solid #e9ecef' }}>
                  <td style={tdStyle}>{r.deviceId}</td>
                  <td style={tdStyle}>{r.type}</td>
                  <td style={tdStyle}>{r.value}</td>
                  <td style={tdStyle}>{r.unit}</td>
                  <td style={tdStyle}>
                    <span style={{ color: r.status.batteryLow ? '#dc3545' : 'inherit' }}>
                      {r.battery}%{r.status.batteryLow ? ' ⚠' : ''}
                    </span>
                  </td>
                  <td style={tdStyle}>{r.signal} dBm</td>
                  <td style={tdStyle}>{formatDate(r.recordedAt)}</td>
                  <td style={tdStyle}>{r.externalId}</td>
                  <td style={tdStyle}>{r.status.batteryLow ? '🔋 Low' : '✓'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

const inputStyle: React.CSSProperties = {
  padding: '0.375rem 0.5rem',
  border: '1px solid #ced4da',
  borderRadius: 4,
  fontSize: '0.875rem',
};

const btnStyle: React.CSSProperties = {
  padding: '0.375rem 0.75rem',
  background: '#0d6efd',
  color: '#fff',
  border: 'none',
  borderRadius: 4,
  cursor: 'pointer',
  fontSize: '0.875rem',
};

const thStyle: React.CSSProperties = {
  textAlign: 'left',
  padding: '0.5rem 0.75rem',
  fontWeight: 600,
  borderBottom: '2px solid #dee2e6',
};

const tdStyle: React.CSSProperties = {
  padding: '0.5rem 0.75rem',
};
