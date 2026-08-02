import { authFetch } from '../utils/authFetch';

const API_BASE =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

/* ─── Types ─────────────────────────────────────────────────────── */

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

/* ─── API Functions ─────────────────────────────────────────────── */

export async function confirmCodPayment(paymentId: string): Promise<PaymentResponseDto> {
  const res = await authFetch(`${API_BASE}/api/driver/payments/${paymentId}/confirm-cod`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi xác nhận thanh toán COD (${res.status})`);
  }
  return res.json();
}
