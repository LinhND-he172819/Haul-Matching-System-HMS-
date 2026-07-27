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
