import { authFetch } from '../utils/authFetch';

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

// ── Types ─────────────────────────────────────────────────────────────

export type DriverAllowedActions = {
  canView: boolean;
  canStart: boolean;
  canComplete: boolean;
};

export type DriverShipmentAllowedActions = {
  canView: boolean;
  canConfirmPickup: boolean;
  canStartTransport: boolean;
  canConfirmDelivery: boolean;
  canConfirmCod: boolean;
};

export type DriverTripListItem = {
  id: string;
  tripCode: string;
  originName?: string;
  destinationName?: string;
  departureTime?: string;
  vehiclePlate?: string;
  status: string;
  totalShipments: number;
  currentWeight: number;
  remainingWeight: number;
  currentVolume: number;
  remainingVolume: number;
  allowedActions: DriverAllowedActions;
};

export type DriverShipmentListItem = {
  id: string;
  shipmentCode: string;
  commodity?: string;
  weight: number;
  volume: number;
  status: string;
  senderName?: string;
  senderPhone?: string;
  pickupAddress?: string;
  receiverName?: string;
  receiverPhone?: string;
  deliveryAddress?: string;
  pendingCodPaymentId?: string;
  pendingCodAmount?: number;
  pendingCodCurrency?: string;
  pendingCodPaymentCode?: string;
  allowedActions: DriverShipmentAllowedActions;
};

export type TripTimelineEntry = {
  label: string;
  timestamp?: string;
  isCompleted: boolean;
  isCurrent: boolean;
};

export type DriverTripDetail = {
  id: string;
  tripCode: string;
  status: string;
  departureTime?: string;
  vehiclePlate?: string;
  originName?: string;
  destinationName?: string;
  routeLineString?: string;
  currentWeight: number;
  remainingWeight: number;
  maxWeight: number;
  currentVolume: number;
  remainingVolume: number;
  maxVolume: number;
  totalShipments: number;
  shipments: DriverShipmentListItem[];
  timeline: TripTimelineEntry[];
  allowedActions: DriverAllowedActions;
};

export type DriverShipmentDetail = {
  id: string;
  shipmentCode: string;
  status: string;
  commodity?: string;
  weight: number;
  volume: number;
  specialInstructions?: string;
  senderName?: string;
  senderPhone?: string;
  pickupAddress?: string;
  pickupNote?: string;
  receiverName?: string;
  receiverPhone?: string;
  deliveryAddress?: string;
  timeline: { label: string; timestamp?: string; isCompleted: boolean; isCurrent: boolean }[];
  pendingCodPaymentId?: string;
  pendingCodAmount?: number;
  pendingCodCurrency?: string;
  pendingCodPaymentCode?: string;
  allowedActions: DriverShipmentAllowedActions;
};

// ── API Functions ─────────────────────────────────────────────────────

export async function getDriverTrips(
  page = 1,
  pageSize = 50
): Promise<{ items: DriverTripListItem[] }> {
  const res = await authFetch(
    `${API_BASE_URL}/api/driver/trips?page=${page}&pageSize=${pageSize}`,
    {},
    { includeJson: true }
  );

  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || 'Không thể tải danh sách chuyến đi.');
  }

  return res.json();
}

export async function getDriverTripDetail(
  tripId: string
): Promise<DriverTripDetail> {
  const res = await authFetch(
    `${API_BASE_URL}/api/driver/trips/${tripId}`,
    {},
    { includeJson: true }
  );

  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || 'Không thể tải chi tiết chuyến đi.');
  }

  return res.json();
}

export async function getDriverShipmentDetail(
  tripId: string,
  shipmentId: string
): Promise<DriverShipmentDetail> {
  const res = await authFetch(
    `${API_BASE_URL}/api/driver/trips/${tripId}/shipments/${shipmentId}`,
    {},
    { includeJson: true }
  );

  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || 'Không thể tải chi tiết kiện hàng.');
  }

  return res.json();
}

export async function startTrip(
  tripId: string
): Promise<{ message: string }> {
  const res = await authFetch(
    `${API_BASE_URL}/api/driver/trips/${tripId}/start`,
    { method: 'PUT' },
    { includeJson: true }
  );

  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể bắt đầu chuyến.' }));
    throw new Error(data.message || 'Không thể bắt đầu chuyến.');
  }

  return res.json();
}

export async function completeTrip(
  tripId: string
): Promise<{ message: string }> {
  const res = await authFetch(
    `${API_BASE_URL}/api/driver/trips/${tripId}/complete`,
    { method: 'PUT' },
    { includeJson: true }
  );

  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể hoàn thành chuyến.' }));
    throw new Error(data.message || 'Không thể hoàn thành chuyến.');
  }

  return res.json();
}

export async function confirmPickup(
  shipmentId: string,
  pickupNote?: string
): Promise<{ message: string; status: string }> {
  const res = await authFetch(
    `${API_BASE_URL}/api/driver/shipments/${shipmentId}/confirm-pickup`,
    {
      method: 'PUT',
      body: JSON.stringify({ pickupNote }),
    },
    { includeJson: true }
  );

  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể xác nhận nhận hàng.' }));
    throw new Error(data.message || 'Không thể xác nhận nhận hàng.');
  }

  return res.json();
}

export async function startTransport(
  shipmentId: string,
  note?: string
): Promise<{ message: string; status: string }> {
  const res = await authFetch(
    `${API_BASE_URL}/api/driver/shipments/${shipmentId}/start-transport`,
    {
      method: 'PUT',
      body: JSON.stringify({ note }),
    },
    { includeJson: true }
  );

  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể bắt đầu vận chuyển.' }));
    throw new Error(data.message || 'Không thể bắt đầu vận chuyển.');
  }

  return res.json();
}

export async function confirmDelivery(
  shipmentId: string,
  deliveryNote?: string,
  proofImageUrl?: string
): Promise<{ message: string; status: string }> {
  const res = await authFetch(
    `${API_BASE_URL}/api/driver/shipments/${shipmentId}/confirm-delivery`,
    {
      method: 'PUT',
      body: JSON.stringify({ deliveryNote, proofImageUrl }),
    },
    { includeJson: true }
  );

  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể xác nhận giao hàng.' }));
    throw new Error(data.message || 'Không thể xác nhận giao hàng.');
  }

  return res.json();
}

export async function reportIncident(
  tripId: string,
  payload: {
    shipmentId?: string;
    incidentType: string;
    description: string;
    files?: File[];
  }
): Promise<{ id: string; incidentCode: string; status: string; evidence: { id: string; fileName: string }[]; message: string }> {
  const formData = new FormData();
  formData.append('incidentType', payload.incidentType);
  formData.append('description', payload.description);
  if (payload.shipmentId) formData.append('shipmentId', payload.shipmentId);
  if (payload.files) {
    for (const file of payload.files) {
      formData.append('files', file);
    }
  }

  const res = await authFetch(
    `${API_BASE_URL}/api/driver/trips/${tripId}/incidents`,
    {
      method: 'POST',
      body: formData,
      // Don't set Content-Type – browser sets it with boundary for FormData
    }
  );

  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể báo cáo sự cố.' }));
    throw new Error(data.message || 'Không thể báo cáo sự cố.');
  }

  return res.json();
}

// ── Driver Incident List ───────────────────────────────────────────

export type DriverIncidentListItem = {
  id: string;
  incidentCode?: string;
  incidentType: string;
  description: string;
  status: string;
  createdAt: string;
  updatedAt: string;
  evidenceCount: number;
};

export type DriverIncidentDetail = {
  id: string;
  incidentCode: string;
  tripId: string;
  tripCode: string;
  incidentType: string;
  description: string;
  status: string;
  reportedAt: string;
  route: string;
  vehiclePlate: string;
  resolutionNote?: string;
  resolvedAt?: string;
  evidence: DriverIncidentEvidence[];
};

export type DriverIncidentEvidence = {
  id: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  uploadedAt: string;
};

export async function getTripIncidents(
  tripId: string
): Promise<DriverIncidentListItem[]> {
  const res = await authFetch(
    `${API_BASE_URL}/api/driver/trips/${tripId}/incidents`
  );
  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể tải danh sách sự cố.' }));
    throw new Error(data.message || 'Không thể tải danh sách sự cố.');
  }
  return res.json();
}

export async function getIncidentDetail(
  incidentId: string
): Promise<DriverIncidentDetail> {
  const res = await authFetch(
    `${API_BASE_URL}/api/driver/incidents/${incidentId}`
  );
  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể tải chi tiết sự cố.' }));
    throw new Error(data.message || 'Không thể tải chi tiết sự cố.');
  }
  return res.json();
}

export function getEvidenceDownloadUrl(incidentId: string, evidenceId: string): string {
  return `${API_BASE_URL}/api/driver/incidents/${incidentId}/evidence/${evidenceId}`;
}
