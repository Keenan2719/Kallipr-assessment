const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '';   // The base URL for the API, configurable via environment variable

export interface ReadingStatus {                              
  batteryLow: boolean;
}

export interface TelemetryReading {
  id: number;
  tenantId: string;
  deviceId: string;
  type: string;
  value: number;
  unit: string;
  battery: number;
  signal: number;
  recordedAt: string;
  externalId: string;
  ingestedAt: string;
  status: ReadingStatus;
}

export interface PagedReadingsResponse {
  items: TelemetryReading[];
  page: number;
  pageSize: number;
}

export interface IngestRequest {
  tenantId: string;
  deviceId: string;
  type: string;
  value: number;
  unit: string;
  battery: number;
  signal: number;
  recordedAt: string;
  externalId: string;
}

export interface ReadingsFilter {
  deviceId?: string;
  type?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export async function fetchReadings(                                                                      // Fetch telemetry readings for a given tenant, with optional filtering and pagination
  tenantId: string,
  filter: ReadingsFilter = {}
): Promise<PagedReadingsResponse> {
  const params = new URLSearchParams();
  if (filter.deviceId) params.set('deviceId', filter.deviceId);
  if (filter.type) params.set('type', filter.type);
  if (filter.from) params.set('from', filter.from);
  if (filter.to) params.set('to', filter.to);
  if (filter.page != null) params.set('page', String(filter.page));
  if (filter.pageSize != null) params.set('pageSize', String(filter.pageSize));

  const qs = params.size > 0 ? `?${params}` : '';
  const res = await fetch(`${BASE_URL}/api/telemetry/${encodeURIComponent(tenantId)}${qs}`);
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json();
}

export async function ingestReading(reading: IngestRequest): Promise<TelemetryReading> {      // Ingest a new telemetry reading into the system, returning the created reading with its assigned ID and timestamps
  const res = await fetch(`${BASE_URL}/api/telemetry`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(reading),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new Error(body?.detail ?? body?.title ?? `HTTP ${res.status}`);
  }
  return res.json();
}
