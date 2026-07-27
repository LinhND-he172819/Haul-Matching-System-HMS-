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
