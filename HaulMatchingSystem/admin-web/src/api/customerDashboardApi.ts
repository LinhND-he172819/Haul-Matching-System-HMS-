import { authFetch } from '../utils/authFetch';

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

// ── Types ─────────────────────────────────────────────────────────────

export type ShipmentsByStatus = {
  status: string;
  count: number;
};

export type CustomerDashboardStats = {
  totalShipments: number;
  shipmentsByStatus: ShipmentsByStatus[];
  totalEstimatedCost: number;
  totalDepositRequired: number;
  totalPaid: number;
  depositPaid: number;
  finalPaid: number;
  outstandingAmount: number;
};

// ── API ───────────────────────────────────────────────────────────────

export async function getCustomerDashboardStats(): Promise<CustomerDashboardStats> {
  const res = await authFetch(`${API_BASE_URL}/api/customer/dashboard/stats`);
  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: 'Lỗi tải dữ liệu' }));
    throw new Error(err.message || `HTTP ${res.status}`);
  }
  return res.json();
}
