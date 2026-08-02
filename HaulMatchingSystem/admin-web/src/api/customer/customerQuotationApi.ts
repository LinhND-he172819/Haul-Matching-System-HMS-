import { authFetch } from '../../utils/authFetch';

const API_BASE =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

/* ─── Types ─────────────────────────────────────────────────────── */

export interface CustomerQuotationDetail {
  id: string;
  quotationCode: string;
  proposalId: string;
  shipmentId: string;
  shippingFee: number;
  depositAmount: number;
  remainingAmount: number;
  currency: string;
  status: string;
  sentAt?: string;
  expiresAt?: string;
  acceptedAt?: string;
  createdAt: string;
}

export interface PaymentResponseDto {
  id: string;
  paymentCode: string;
  quotationId: string;
  shipmentId: string;
  paymentType: string;
  amount: number;
  currency: string;
  status: string;
  paidAt?: string;
  createdAt: string;
}

export interface PaymentSummaryDto {
  depositPaid: number;
  finalPaid: number;
  totalPaid: number;
  outstandingAmount: number;
  shippingFee: number;
  depositAmount: number;
  transactionRef?: string;
  paymentStatus: string;
}

export interface PaymentHistoryEntry {
  id: string;
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

/* ─── API Functions ─────────────────────────────────────────────── */

export async function getCustomerQuotation(quotationId: string): Promise<CustomerQuotationDetail> {
  const res = await authFetch(`${API_BASE}/api/customer/quotations/${quotationId}`);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tải báo giá (${res.status})`);
  }
  return res.json();
}

export async function createDepositPayment(
  quotationId: string,
  paymentMethod?: string
): Promise<PaymentResponseDto> {
  const res = await authFetch(`${API_BASE}/api/customer/quotations/${quotationId}/deposit-payment`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ paymentMethod: paymentMethod || 'Online' }),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tạo thanh toán đặt cọc (${res.status})`);
  }
  return res.json();
}

export async function createFinalPayment(
  quotationId: string,
  paymentMethod?: string
): Promise<PaymentResponseDto> {
  const res = await authFetch(`${API_BASE}/api/customer/quotations/${quotationId}/final-payment`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ paymentMethod: paymentMethod || 'Online' }),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tạo thanh toán cuối (${res.status})`);
  }
  return res.json();
}

export async function getPaymentSummary(shipmentId: string): Promise<PaymentSummaryDto> {
  const res = await authFetch(`${API_BASE}/api/customer/shipments/${shipmentId}/payments`);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tải tổng quan thanh toán (${res.status})`);
  }
  return res.json();
}

export async function getPaymentHistory(quotationId: string): Promise<PaymentHistoryEntry[]> {
  const res = await authFetch(`${API_BASE}/api/customer/quotations/${quotationId}/payment-history`);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tải lịch sử thanh toán (${res.status})`);
  }
  return res.json();
}

/* ─── Part 13: New Payment APIs ──────────────────────────────────── */

export async function retryPayment(paymentId: string): Promise<PaymentResponseDto> {
  const res = await authFetch(`${API_BASE}/api/customer/payments/${paymentId}/retry`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi thử lại thanh toán (${res.status})`);
  }
  return res.json();
}

export async function cancelPayment(paymentId: string, reason?: string): Promise<PaymentResponseDto> {
  const res = await authFetch(`${API_BASE}/api/customer/payments/${paymentId}/cancel`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason: reason || '' }),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi hủy thanh toán (${res.status})`);
  }
  return res.json();
}

export async function getPaymentDetail(paymentId: string): Promise<PaymentDetailDto> {
  const res = await authFetch(`${API_BASE}/api/customer/payments/${paymentId}`);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tải chi tiết thanh toán (${res.status})`);
  }
  return res.json();
}

export async function getPaymentTimeline(paymentId: string): Promise<PaymentTimelineEntry[]> {
  const res = await authFetch(`${API_BASE}/api/customer/payments/${paymentId}/timeline`);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tải lịch sử trạng thái (${res.status})`);
  }
  return res.json();
}
