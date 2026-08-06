import { authFetch } from '../utils/authFetch';

const API_BASE =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

// ── Types ─────────────────────────────────────────────────────────────

export interface CreateExternalShipmentRequest {
  senderName: string;
  senderPhone: string;
  pickupAddress: string;
  receiverName: string;
  receiverPhone: string;
  destAddress: string;
  category: string;
  description?: string;
  weightKg: number;
  volumeCbm: number;
  quantity: number;
  codRequired: boolean;
  note?: string;
}

export interface CreateExternalShipmentResponse {
  shipmentId: string;
  proposalId: string;
  requestedTripId: string;
  shipmentCode: string;
  proposalStatus: string;
  createdAt: string;
}

export interface ExternalShipmentListItem {
  shipmentId: string;
  proposalId: string;
  shipmentCode?: string;
  proposalCode?: string;
  category: string;
  weightKg: number;
  volumeCbm: number;
  quantity: number;
  receiverName: string;
  destAddress?: string;
  proposalStatus: string;
  shipmentStatus: string;
  quotationStatus?: string;
  paymentStatus?: string;
  createdAt: string;
}

export interface ExternalShipmentTimelineEntry {
  status: string;
  label: string;
  timestamp?: string;
  note?: string;
}

export interface ExternalShipmentDetail {
  shipmentId: string;
  proposalId: string;
  shipmentCode?: string;
  proposalCode?: string;
  senderName: string;
  senderPhone: string;
  pickupAddress: string;
  receiverName: string;
  receiverPhone: string;
  destAddress: string;
  category: string;
  description?: string;
  weightKg: number;
  volumeCbm: number;
  quantity: number;
  codRequired: boolean;
  note?: string;
  codAmount: number;
  proposalStatus: string;
  shipmentStatus: string;
  quotationStatus?: string;
  paymentStatus?: string;
  shippingFee?: number;
  depositAmount?: number;
  tripId: string;
  tripCode?: string;
  origin?: string;
  destination?: string;
  createdAt: string;
  timeline: ExternalShipmentTimelineEntry[];
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ── API Functions ─────────────────────────────────────────────────────

/**
 * Create a new external shipment declaration.
 */
export async function createExternalShipment(
  payload: CreateExternalShipmentRequest
): Promise<CreateExternalShipmentResponse> {
  const res = await authFetch(
    `${API_BASE}/api/driver/external-shipments`,
    {
      method: 'POST',
      body: JSON.stringify(payload),
    },
    { includeJson: true }
  );

  if (!res.ok) {
    const message = await res.text();
    throw new Error(message || 'Không thể tạo đơn hàng.');
  }
  return res.json();
}

/**
 * Get a paginated list of the driver's external shipment declarations.
 */
export async function getExternalShipments(params: {
  status?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedResult<ExternalShipmentListItem>> {
  const query = new URLSearchParams();
  if (params.status) query.set('status', params.status);
  if (params.page) query.set('page', params.page.toString());
  if (params.pageSize) query.set('pageSize', params.pageSize.toString());

  const qs = query.toString();
  const url = `${API_BASE}/api/driver/external-shipments${qs ? '?' + qs : ''}`;

  const res = await authFetch(url, {}, { includeJson: true });

  if (!res.ok) {
    const message = await res.text();
    throw new Error(message || 'Không thể tải danh sách đơn hàng.');
  }
  return res.json();
}

/**
 * Get detail of a specific external shipment declaration.
 */
export async function getExternalShipmentDetail(
  id: string
): Promise<ExternalShipmentDetail> {
  const res = await authFetch(
    `${API_BASE}/api/driver/external-shipments/${id}`,
    {},
    { includeJson: true }
  );

  if (!res.ok) {
    const message = await res.text();
    throw new Error(message || 'Không thể tải chi tiết đơn hàng.');
  }
  return res.json();
}
