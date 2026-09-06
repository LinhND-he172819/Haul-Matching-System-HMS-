import { authFetch } from '../utils/authFetch';

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

// ── Types ─────────────────────────────────────────────────────────────

export type RevenueByMonth = {
  month_key: string;
  label: string;
  revenue: number;
  payment_count: number;
};

export type RevenueByType = {
  payment_type: string;
  total: number;
  count: number;
};

export type ShipmentsByStatus = {
  status: string;
  count: number;
};

export type AdminDashboardStats = {
  totalCustomers: number;
  newCustomersThisMonth: number;
  totalRevenue: number;
  revenueThisMonth: number;
  revenueByMonth: RevenueByMonth[];
  revenueByType: RevenueByType[];
  activeTrips: number;
  inTransitShipments: number;
  totalShipments: number;
  totalDrivers: number;
  shipmentsByStatus: ShipmentsByStatus[];
  fromDate: string;
  toDate: string;
  lastUpdated: string;
};

// ── API ───────────────────────────────────────────────────────────────

export async function getAdminDashboardStats(
  from?: string,
  to?: string,
): Promise<AdminDashboardStats> {
  const params = new URLSearchParams();
  if (from) params.set('from', from);
  if (to) params.set('to', to);
  const qs = params.toString();

  const res = await authFetch(
    `${API_BASE_URL}/api/admin/dashboard/stats${qs ? '?' + qs : ''}`,
  );
  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: 'Lỗi tải dữ liệu' }));
    throw new Error(err.message || `HTTP ${res.status}`);
  }
  return res.json();
}
