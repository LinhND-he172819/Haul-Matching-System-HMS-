import { authFetch } from '../utils/authFetch';

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

// ── Types ─────────────────────────────────────────────────────────────

export type AllowedActions = {
  canView: boolean;
  canEdit: boolean;
  canCancel: boolean;
  canPayDeposit: boolean;
  canPayRemaining: boolean;
  canGiveFeedback?: boolean;
};

export type CustomerShipmentListItem = {
  id: string;
  shipmentCode: string;
  commodity?: string;
  weight: number;
  volume: number;
  receiverName?: string;
  deliveryAddress?: string;
  status: string;
  tripCode?: string;
  originName?: string;
  destinationName?: string;
  createdAt: string;
  allowedActions: AllowedActions;
};

export type ProposalInfo = {
  id?: string;
  status?: string;
  submittedAt?: string;
  reviewedAt?: string;
  rejectReason?: string;
};

export type QuotationInfo = {
  id?: string;
  quotationCode?: string;
  shippingFee: number;
  depositAmount: number;
  remainingAmount: number;
  sentAt?: string;
  expiresAt?: string;
  status?: string;
};

export type PaymentSummary = {
  depositPaid: number;
  finalPaid: number;
  totalPaid: number;
  outstandingAmount: number;
  transactionRef?: string;
  paymentStatus?: string;
};

export type TimelineEntry = {
  label: string;
  timestamp?: string;
  isCompleted: boolean;
  isCurrent: boolean;
};

export type CustomerShipmentDetail = {
  id: string;
  shipmentCode: string;
  status: string;
  createdAt: string;
  updatedAt: string;
  senderName?: string;
  senderPhone?: string;
  pickupAddress?: string;
  pickupNote?: string;
  receiverName?: string;
  receiverPhone?: string;
  deliveryAddress?: string;
  commodity?: string;
  weight: number;
  volume: number;
  specialInstructions?: string;
  tripCode?: string;
  originName?: string;
  destinationName?: string;
  departureTime?: string;
  vehiclePlate?: string;
  proposal?: ProposalInfo;
  quotation?: QuotationInfo;
  payment?: PaymentSummary;
  timeline: TimelineEntry[];
  allowedActions: AllowedActions;
};

export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
};

export type UpdateDraftShipmentRequest = {
  receiverName?: string;
  receiverPhone?: string;
  deliveryAddress?: string;
  commodity?: string;
  weight?: number;
  volume?: number;
  specialInstructions?: string;
  senderName?: string;
  senderPhone?: string;
  pickupAddress?: string;
  pickupNote?: string;
};

// ── API Functions ─────────────────────────────────────────────────────

export async function getMyShipments(
  page = 1,
  pageSize = 20
): Promise<PagedResult<CustomerShipmentListItem>> {
  const res = await authFetch(
    `${API_BASE_URL}/api/customer/shipments?page=${page}&pageSize=${pageSize}`,
    {},
    { includeJson: true }
  );

  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || 'Không thể tải danh sách đơn hàng.');
  }

  return res.json();
}

export async function getShipmentDetail(
  shipmentId: string
): Promise<CustomerShipmentDetail> {
  const res = await authFetch(
    `${API_BASE_URL}/api/customer/shipments/${shipmentId}`,
    {},
    { includeJson: true }
  );

  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || 'Không thể tải chi tiết đơn hàng.');
  }

  return res.json();
}

export async function updateDraftShipment(
  shipmentId: string,
  payload: UpdateDraftShipmentRequest
): Promise<{ message: string }> {
  const res = await authFetch(
    `${API_BASE_URL}/api/customer/shipments/${shipmentId}`,
    {
      method: 'PUT',
      body: JSON.stringify(payload),
    },
    { includeJson: true }
  );

  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể cập nhật đơn hàng.' }));
    throw new Error(data.message || 'Không thể cập nhật đơn hàng.');
  }

  return res.json();
}

export async function cancelShipment(
  shipmentId: string,
  reason: string
): Promise<{ message: string }> {
  const res = await authFetch(
    `${API_BASE_URL}/api/customer/shipments/${shipmentId}/cancel`,
    {
      method: 'POST',
      body: JSON.stringify({ reason }),
    },
    { includeJson: true }
  );

  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể hủy đơn hàng.' }));
    throw new Error(data.message || 'Không thể hủy đơn hàng.');
  }

  return res.json();
}
