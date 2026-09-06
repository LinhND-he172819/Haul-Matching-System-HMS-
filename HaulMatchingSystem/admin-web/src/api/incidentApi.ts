/**
 * Admin/Staff Incident Management API client.
 * Base: /api/staff/incidents
 */
import { authFetch } from '../utils/authFetch';

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

// ── Types ─────────────────────────────────────────────────────────────

export type AdminIncidentListItem = {
  id: string;
  incidentCode: string;
  incidentType: string;
  description: string;
  status: string;
  tripId: string;
  tripCode: string;
  driverName: string;
  driverId: string;
  vehiclePlate?: string;
  route?: string;
  reportedAt: string;
  evidenceCount: number;
  assignedToName?: string;
  assignedToId?: string;
};

export type AdminIncidentPagedResult = {
  items: AdminIncidentListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

export type AdminIncidentDetail = {
  id: string;
  incidentCode: string;
  incidentType: string;
  description: string;
  status: string;
  tripId: string;
  tripCode: string;
  driverId: string;
  driverName: string;
  vehiclePlate?: string;
  route?: string;
  reportedAt: string;
  updatedAt: string;
  assignedToUserId?: string;
  assignedToName?: string;
  assignedAt?: string;
  resolutionNote?: string;
  resolvedByUserId?: string;
  resolvedByName?: string;
  resolvedAt?: string;
  evidence: AdminIncidentEvidence[];
  allowedActions: AdminIncidentAllowedActions[];
};

export type AdminIncidentEvidence = {
  id: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  uploadedAt: string;
};

export type AdminIncidentAllowedActions = {
  canTake: boolean;
  canResolve: boolean;
  canReject: boolean;
};

// ── API Functions ─────────────────────────────────────────────────────

export async function listIncidents(params: {
  page?: number;
  pageSize?: number;
  status?: string;
  incidentType?: string;
  search?: string;
}): Promise<AdminIncidentPagedResult> {
  const searchParams = new URLSearchParams();
  if (params.page) searchParams.set('page', params.page.toString());
  if (params.pageSize) searchParams.set('pageSize', params.pageSize.toString());
  if (params.status) searchParams.set('status', params.status);
  if (params.incidentType) searchParams.set('incidentType', params.incidentType);
  if (params.search) searchParams.set('search', params.search);

  const res = await authFetch(
    `${API_BASE_URL}/api/staff/incidents?${searchParams.toString()}`
  );
  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể tải danh sách sự cố.' }));
    throw new Error(data.message || 'Không thể tải danh sách sự cố.');
  }
  return res.json();
}

export async function getIncidentDetail(
  incidentId: string
): Promise<AdminIncidentDetail> {
  const res = await authFetch(
    `${API_BASE_URL}/api/staff/incidents/${incidentId}`
  );
  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể tải chi tiết sự cố.' }));
    throw new Error(data.message || 'Không thể tải chi tiết sựố.');
  }
  return res.json();
}

export async function takeIncident(
  incidentId: string,
  note?: string
): Promise<{ message: string; status: string }> {
  const res = await authFetch(
    `${API_BASE_URL}/api/staff/incidents/${incidentId}/take`,
    {
      method: 'POST',
      body: JSON.stringify({ note: note || '' }),
    },
    { includeJson: true }
  );
  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể tiếp nhận sự cố.' }));
    throw new Error(data.message || 'Không thể tiếp nhận sự cố.');
  }
  return res.json();
}

export async function resolveIncident(
  incidentId: string,
  note: string
): Promise<{ message: string; status: string }> {
  const res = await authFetch(
    `${API_BASE_URL}/api/staff/incidents/${incidentId}/resolve`,
    {
      method: 'POST',
      body: JSON.stringify({ note }),
    },
    { includeJson: true }
  );
  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể xử lý sự cố.' }));
    throw new Error(data.message || 'Không thể xử lý sự cố.');
  }
  return res.json();
}

export async function rejectIncident(
  incidentId: string,
  reason: string
): Promise<{ message: string; status: string }> {
  const res = await authFetch(
    `${API_BASE_URL}/api/staff/incidents/${incidentId}/reject`,
    {
      method: 'POST',
      body: JSON.stringify({ note: reason }),
    },
    { includeJson: true }
  );
  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể từ chối sự cố.' }));
    throw new Error(data.message || 'Không thể từ chối sự cố.');
  }
  return res.json();
}

export function getEvidenceDownloadUrl(incidentId: string, evidenceId: string): string {
  return `${API_BASE_URL}/api/staff/incidents/${incidentId}/evidence/${evidenceId}`;
}
