import { authFetch } from '../../utils/authFetch';

const API_BASE =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

/* ─── Types ─────────────────────────────────────────────────────── */

export interface CreateQuotationRequest {
  shippingFee: number;
  depositAmount: number;
  currency: string;
  expiresAt: string;
}

export interface UpdateQuotationRequest {
  shippingFee: number;
  depositAmount: number;
  currency: string;
  expiresAt: string;
}

export interface QuotationResponseDto {
  id: string;
  proposalId: string;
  quotationCode: string;
  shippingFee: number;
  depositAmount: number;
  currency: string;
  status: string;
  quotedBy: string;
  quotedByName?: string;
  quotedAt: string;
  sentAt?: string;
  expiresAt?: string;
  acceptedAt?: string;
  createdAt: string;
}

export interface QuotationListParams {
  status?: string;
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface QuotationListItem {
  id: string;
  quotationCode: string;
  proposalId: string;
  proposalCode?: string;
  shipmentCode: string;
  customerName: string;
  shippingFee: number;
  depositAmount: number;
  currency: string;
  status: string;
  sentAt?: string;
  expiresAt?: string;
  createdAt: string;
}

/* ─── API Functions ─────────────────────────────────────────────── */

export async function getStaffQuotations(params: QuotationListParams): Promise<PagedResult<QuotationListItem>> {
  const url = new URL(`${API_BASE}/api/staff/quotations`);
  if (params.status) url.searchParams.set('status', params.status);
  if (params.page) url.searchParams.set('page', params.page.toString());
  if (params.pageSize) url.searchParams.set('pageSize', params.pageSize.toString());

  const res = await authFetch(url.toString());
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tải danh sách báo giá (${res.status})`);
  }
  return res.json();
}

export async function getStaffQuotationDetail(quotationId: string): Promise<QuotationResponseDto> {
  const res = await authFetch(`${API_BASE}/api/staff/quotations/${quotationId}`);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tải chi tiết báo giá (${res.status})`);
  }
  return res.json();
}

export async function createQuotation(proposalId: string, request: CreateQuotationRequest): Promise<QuotationResponseDto> {
  const res = await authFetch(`${API_BASE}/api/staff/proposals/${proposalId}/quotations`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tạo báo giá (${res.status})`);
  }
  return res.json();
}

export async function updateQuotation(quotationId: string, request: UpdateQuotationRequest): Promise<QuotationResponseDto> {
  const res = await authFetch(`${API_BASE}/api/staff/quotations/${quotationId}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi cập nhật báo giá (${res.status})`);
  }
  return res.json();
}

export async function sendQuotation(quotationId: string): Promise<void> {
  const res = await authFetch(`${API_BASE}/api/staff/quotations/${quotationId}/send`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({}),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi gửi báo giá (${res.status})`);
  }
}

export async function cancelQuotation(quotationId: string, reason?: string): Promise<void> {
  const res = await authFetch(`${API_BASE}/api/staff/quotations/${quotationId}/cancel`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason: reason || '' }),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi hủy báo giá (${res.status})`);
  }
}
