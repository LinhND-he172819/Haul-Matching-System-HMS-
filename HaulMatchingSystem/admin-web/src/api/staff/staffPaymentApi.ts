import { authFetch } from '../../utils/authFetch';

const API_BASE =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

/* ─── Types ─────────────────────────────────────────────────────── */

export interface PaymentListItem {
  id: string;
  paymentCode: string;
  quotationId: string;
  quotationCode: string;
  shipmentId: string;
  shipmentCode: string;
  customerName: string;
  paymentType: string;
  amount: number;
  currency: string;
  status: string;
  transactionReference?: string;
  paidAt?: string;
  createdAt: string;
}

export interface PaymentDetailDto {
  id: string;
  paymentCode: string;
  paymentGateway?: string;
  paymentMethod?: string;
  status: string;
  paymentType: string;
  amount: number;
  currency: string;
  createdAt: string;
  paidAt?: string;
  cancelledAt?: string;
  failedAt?: string;
  transactionReference?: string;
  failureReason?: string;
  quotationId?: string;
  quotationCode?: string;
  shippingFee?: number;
  depositAmount?: number;
  shipmentId?: string;
  shipmentCode?: string;
  shipmentStatus?: string;
  customerId?: string;
  customerName?: string;
}

export interface PaymentTimelineEntry {
  status: string;
  action: string;
  details?: string;
  occurredAt: string;
}

export interface RequestRefundResponse {
  id: string;
  paymentCode: string;
  status: string;
  amount: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/* ─── API Functions ─────────────────────────────────────────────── */

export async function getStaffPayments(params: {
  status?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedResult<PaymentListItem>> {
  const url = new URL(`${API_BASE}/api/staff/payments`);
  if (params.status) url.searchParams.set('status', params.status);
  if (params.page) url.searchParams.set('page', params.page.toString());
  if (params.pageSize) url.searchParams.set('pageSize', params.pageSize.toString());

  const res = await authFetch(url.toString());
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tải danh sách thanh toán (${res.status})`);
  }
  return res.json();
}

export async function getStaffPaymentDetail(paymentId: string): Promise<PaymentDetailDto> {
  const res = await authFetch(`${API_BASE}/api/staff/payments/${paymentId}`);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tải chi tiết thanh toán (${res.status})`);
  }
  return res.json();
}

export async function getStaffPaymentTimeline(paymentId: string): Promise<PaymentTimelineEntry[]> {
  const res = await authFetch(`${API_BASE}/api/staff/payments/${paymentId}/timeline`);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tải lịch sử trạng thái (${res.status})`);
  }
  return res.json();
}

export async function requestRefund(
  paymentId: string,
  reason: string
): Promise<RequestRefundResponse> {
  const res = await authFetch(`${API_BASE}/api/staff/payments/${paymentId}/refund`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason }),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi yêu cầu hoàn tiền (${res.status})`);
  }
  return res.json();
}

export async function approveRefund(paymentId: string): Promise<RequestRefundResponse> {
  const res = await authFetch(`${API_BASE}/api/staff/payments/${paymentId}/refund/approve`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi duyệt hoàn tiền (${res.status})`);
  }
  return res.json();
}
