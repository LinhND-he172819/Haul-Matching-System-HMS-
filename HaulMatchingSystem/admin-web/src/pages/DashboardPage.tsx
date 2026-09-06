import { useEffect, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import {
  LineChart, Line, BarChart, Bar, PieChart, Pie, Cell,
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
} from 'recharts';
import {
  getAdminDashboardStats,
  type AdminDashboardStats,
} from '../api/adminDashboardApi';

const apiBaseUrl =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

interface RealtimeStats {
  activeTripCount: number;
  inTransitShipments: number;
  avgVehicleUtilisation: number;
  hubItemsWaitingOver3Days: number;
  lastUpdated: string;
}

interface DashboardPageProps {
  sidebar?: React.ReactNode;
}

type DatePreset = 'month' | 'quarter' | 'year' | 'all' | 'custom';

const PRESET_LABELS: Record<DatePreset, string> = {
  month: 'Tháng này',
  quarter: 'Quý này',
  year: 'Năm nay',
  all: 'Tất cả',
  custom: 'Tùy chọn',
};

function getPresetRange(preset: DatePreset): { from: string; to: string } {
  const now = new Date();
  const to = now.toISOString().slice(0, 10);
  let from: string;
  switch (preset) {
    case 'month': {
      const d = new Date(now.getFullYear(), now.getMonth(), 1);
      from = d.toISOString().slice(0, 10);
      break;
    }
    case 'quarter': {
      const q = Math.floor(now.getMonth() / 3) * 3;
      const d = new Date(now.getFullYear(), q, 1);
      from = d.toISOString().slice(0, 10);
      break;
    }
    case 'year':
      from = `${now.getFullYear()}-01-01`;
      break;
    case 'all':
      from = '2020-01-01';
      break;
    default:
      from = to;
  }
  return { from, to };
}

const fmtVND = (n: number) =>
  new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(n);
const fmtNumber = (n: number) => new Intl.NumberFormat('vi-VN').format(n);

const STATUS_COLORS: Record<string, string> = {
  Completed: '#00897b', In_Transit: '#3f51b5', Matched: '#7c4dff', Delivered: '#00897b',
  In_Warehouse: '#1976d2', PendingReview: '#ffa726', PendingDeposit: '#ff7043', Draft: '#bdbdbd', Cancelled: '#e53935',
};
const STATUS_LABELS: Record<string, string> = {
  Completed: 'Hoàn tất', In_Transit: 'Đang vận chuyển', Matched: 'Đã ghép chuyến',
  Delivered: 'Đã giao', In_Warehouse: 'Tại kho', PendingReview: 'Chờ duyệt',
  PendingDeposit: 'Chờ đặt cọc', Draft: 'Bản nháp', Cancelled: 'Đã hủy',
};
const PAYMENT_TYPE_COLORS: Record<string, string> = {
  Deposit: '#00897b', FinalPayment: '#3f51b5', AdditionalCharge: '#ff7043', Refund: '#e53935',
};
const PAYMENT_TYPE_LABELS: Record<string, string> = {
  Deposit: 'Đặt cọc', FinalPayment: 'Thanh toán cuối', AdditionalCharge: 'Phụ phí', Refund: 'Hoàn tiền',
};

export default function DashboardPage({ sidebar }: DashboardPageProps) {
  const [rtStats, setRtStats] = useState<RealtimeStats | null>(null);
  const [stats, setStats] = useState<AdminDashboardStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [preset, setPreset] = useState<DatePreset>('year');
  const [customFrom, setCustomFrom] = useState('');
  const [customTo, setCustomTo] = useState('');
  const [connectionStatus, setConnectionStatus] = useState('Đang kết nối...');

  const loadStats = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const { from, to } = preset === 'custom'
        ? { from: customFrom || '2020-01-01', to: customTo || new Date().toISOString().slice(0, 10) }
        : getPresetRange(preset);
      setStats(await getAdminDashboardStats(from, to));
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Lỗi tải dữ liệu');
    } finally {
      setLoading(false);
    }
  }, [preset, customFrom, customTo]);

  useEffect(() => { loadStats(); }, [loadStats]);

  useEffect(() => {
    const startConnection = async () => {
      let token = localStorage.getItem('accessToken');
      if (!token) return;
      const isExpiringSoon = (t: string) => {
        try {
          const p = JSON.parse(atob(t.split('.')[1]));
          return Date.now() > p.exp * 1000 - 60000;
        } catch {
          return false;
        }
      };
      if (isExpiringSoon(token)) {
        try {
          const res = await fetch(`${apiBaseUrl}/api/auth/refresh-token`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              accessToken: token,
              refreshToken: localStorage.getItem('refreshToken'),
            }),
          });
          if (res.ok) {
            const d = await res.json();
            localStorage.setItem('accessToken', d.accessToken);
            localStorage.setItem('refreshToken', d.refreshToken);
            localStorage.setItem('userId', d.userId);
            token = d.accessToken;
          }
        } catch {
          /* ignore */
        }
      }
      const connection = new signalR.HubConnectionBuilder()
        .withUrl(`${apiBaseUrl}/hub/fleet`, {
          accessTokenFactory: () => localStorage.getItem('accessToken') || '',
        })
        .withAutomaticReconnect()
        .build();
      connection.on('ReceiveAdminStats', (data: RealtimeStats) => setRtStats(data));
      try {
        await connection.start();
        setConnectionStatus('Connected');
      } catch {
        setConnectionStatus('Lỗi kết nối');
      }
    };
    startConnection();
  }, []);

  const formattedTime = stats?.lastUpdated
    ? new Date(stats.lastUpdated).toLocaleTimeString('vi-VN')
    : rtStats?.lastUpdated
      ? new Date(rtStats.lastUpdated).toLocaleTimeString('vi-VN')
      : '--:--:--';

  const revenueChartData = (stats?.revenueByMonth ?? []).map((m) => ({
    label: m.label,
    DoanhThu: Number(m.revenue),
    'Số GD': m.payment_count,
  }));
  const revenueByTypeData = (stats?.revenueByType ?? []).map((t) => ({
    name: PAYMENT_TYPE_LABELS[t.payment_type] || t.payment_type,
    value: Number(t.total),
    fill: PAYMENT_TYPE_COLORS[t.payment_type] || '#999',
  }));
  const shipmentStatusData = (stats?.shipmentsByStatus ?? []).map((s) => ({
    name: STATUS_LABELS[s.status] || s.status,
    value: s.count,
    fill: STATUS_COLORS[s.status] || '#999',
  }));

  return (
    <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden">
      {sidebar}
      <div className="flex-1 flex flex-col xl:ml-64 w-full">
        {/* Header */}
        <header className="bg-surface-container-lowest border-b border-outline-variant h-16 w-full flex justify-between items-center px-8 sticky top-0 z-10">
          <div className="hidden md:flex items-center bg-surface-container-low rounded-lg px-3 py-1.5 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary w-64 transition-all">
            <span className="material-symbols-outlined text-on-surface-variant text-[20px] mr-2">search</span>
            <input
              className="bg-transparent border-none outline-none text-body-md font-body-md w-full placeholder-on-surface-variant/70 p-0 text-on-surface"
              placeholder="Tìm kiếm..."
              type="text"
            />
          </div>
          <div className="flex items-center gap-6">
            <div className="hidden sm:flex items-center gap-2 text-label-md font-label-md text-on-surface-variant bg-surface px-3 py-1 rounded-full border border-outline-variant/30">
              <span className={`w-2 h-2 rounded-full ${connectionStatus === 'Connected' ? 'bg-secondary live-pulse' : 'bg-error'}`} />
              {connectionStatus}
            </div>
            <div className="w-8 h-8 rounded-full bg-primary-fixed text-primary overflow-hidden border border-outline-variant/50 flex items-center justify-center">
              <span className="material-symbols-outlined text-[20px]">person</span>
            </div>
          </div>
        </header>

        <main className="flex-1 p-container-margin overflow-y-auto">
          {/* Title + Date Filter */}
          <div className="flex flex-col sm:flex-row sm:justify-between sm:items-end mb-6 gap-4">
            <div>
              <div className="flex items-center text-label-md font-label-md text-on-surface-variant mb-1">
                <span>Analytics</span>
                <span className="material-symbols-outlined text-[14px] mx-1">chevron_right</span>
                <span className="text-primary font-bold">Tổng quan &amp; Thống kê</span>
              </div>
              <h2 className="text-headline-lg font-headline-lg text-on-surface">Bảng điều khiển quản trị</h2>
            </div>
            <div className="flex items-center gap-3 flex-wrap">
              <div className="flex items-center bg-surface-container-low rounded-lg border border-outline-variant/30 overflow-hidden">
                {(['month', 'quarter', 'year', 'all'] as DatePreset[]).map((p) => (
                  <button
                    key={p}
                    onClick={() => setPreset(p)}
                    className={`px-3 py-1.5 text-label-md font-bold transition-colors ${
                      preset === p ? 'bg-primary text-on-primary' : 'text-on-surface-variant hover:bg-surface-container-low'
                    }`}
                  >
                    {PRESET_LABELS[p]}
                  </button>
                ))}
                <button
                  onClick={() => setPreset('custom')}
                  className={`px-3 py-1.5 text-label-md font-bold transition-colors ${
                    preset === 'custom' ? 'bg-primary text-on-primary' : 'text-on-surface-variant hover:bg-surface-container-low'
                  }`}
                >
                  Tùy chọn
                </button>
              </div>
              {preset === 'custom' && (
                <div className="flex items-center gap-2">
                  <input
                    type="date"
                    value={customFrom}
                    onChange={(e) => setCustomFrom(e.target.value)}
                    className="px-3 py-1.5 rounded-lg border border-outline-variant bg-surface-container-lowest text-body-sm text-on-surface focus:ring-2 focus:ring-primary/40 focus:border-primary"
                  />
                  <span className="text-on-surface-variant text-body-sm">đến</span>
                  <input
                    type="date"
                    value={customTo}
                    onChange={(e) => setCustomTo(e.target.value)}
                    className="px-3 py-1.5 rounded-lg border border-outline-variant bg-surface-container-lowest text-body-sm text-on-surface focus:ring-2 focus:ring-primary/40 focus:border-primary"
                  />
                </div>
              )}
              <div className="text-label-md font-label-md text-on-surface-variant bg-surface px-3 py-1.5 rounded border border-outline-variant/30 card-shadow inline-flex items-center gap-2">
                <span className="material-symbols-outlined text-[16px]">schedule</span>
                Cập nhật: <strong className="text-primary">{formattedTime}</strong>
              </div>
            </div>
          </div>

          {loading && !stats ? (
            <div className="flex flex-col items-center justify-center py-20 gap-3">
              <span className="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
              <p className="text-body-md text-on-surface-variant">Đang tải dữ liệu...</p>
            </div>
          ) : error ? (
            <div className="flex flex-col items-center justify-center py-20 gap-3">
              <span className="material-symbols-outlined text-[36px] text-error">error</span>
              <p className="text-body-md text-error">{error}</p>
              <button
                onClick={loadStats}
                className="px-4 py-2 rounded-lg bg-primary text-on-primary text-label-md font-bold hover:opacity-90"
              >
                Thử lại
              </button>
            </div>
          ) : (
            stats && (
              <>
                {/* KPI: Revenue & Customers */}
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-gutter mb-gutter">
                  <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col justify-between">
                    <div className="flex justify-between items-start mb-4">
                      <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Tổng doanh thu</span>
                      <div className="w-8 h-8 rounded-full bg-secondary-container flex items-center justify-center text-secondary">
                        <span className="material-symbols-outlined text-[20px]">payments</span>
                      </div>
                    </div>
                    <span className="text-display-lg font-display-lg text-on-surface">{fmtVND(stats.totalRevenue)}</span>
                  </div>
                  <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col justify-between">
                    <div className="flex justify-between items-start mb-4">
                      <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Doanh thu tháng này</span>
                      <div className="w-8 h-8 rounded-full bg-primary-fixed flex items-center justify-center text-primary">
                        <span className="material-symbols-outlined text-[20px]">trending_up</span>
                      </div>
                    </div>
                    <span className="text-display-lg font-display-lg text-on-surface">{fmtVND(stats.revenueThisMonth)}</span>
                  </div>
                  <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col justify-between">
                    <div className="flex justify-between items-start mb-4">
                      <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Tổng khách hàng</span>
                      <div className="w-8 h-8 rounded-full bg-tertiary-container flex items-center justify-center text-tertiary">
                        <span className="material-symbols-outlined text-[20px]">group</span>
                      </div>
                    </div>
                    <span className="text-display-lg font-display-lg text-on-surface">{fmtNumber(stats.totalCustomers)}</span>
                    <span className="text-label-md font-label-md text-secondary mt-1">+{stats.newCustomersThisMonth} tháng này</span>
                  </div>
                  <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col justify-between">
                    <div className="flex justify-between items-start mb-4">
                      <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Tổng tài xế</span>
                      <div className="w-8 h-8 rounded-full bg-surface-container-high flex items-center justify-center text-primary relative">
                        <span className="material-symbols-outlined text-[20px] relative z-10">local_shipping</span>
                        <span className="absolute inset-0 bg-primary/20 rounded-full live-pulse" />
                      </div>
                    </div>
                    <span className="text-display-lg font-display-lg text-on-surface">{fmtNumber(stats.totalDrivers)}</span>
                  </div>
                </div>

                {/* KPI: Operational (SignalR realtime) */}
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-gutter mb-gutter">
                  <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col justify-between">
                    <div className="flex justify-between items-start mb-4">
                      <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Chuyến đang chạy</span>
                      <div className="w-8 h-8 rounded-full bg-primary-fixed flex items-center justify-center text-primary">
                        <span className="material-symbols-outlined text-[20px]">route</span>
                      </div>
                    </div>
                    <span className="text-display-lg font-display-lg text-on-surface">
                      {rtStats?.activeTripCount ?? stats.activeTrips}
                    </span>
                  </div>
                  <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col justify-between">
                    <div className="flex justify-between items-start mb-4">
                      <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Đơn đang vận chuyển</span>
                      <div className="w-8 h-8 rounded-full bg-surface-container-high flex items-center justify-center text-primary relative">
                        <span className="material-symbols-outlined text-[20px] relative z-10">package_2</span>
                        <span className="absolute inset-0 bg-primary/20 rounded-full live-pulse" />
                      </div>
                    </div>
                    <span className="text-display-lg font-display-lg text-on-surface">
                      {rtStats?.inTransitShipments ?? stats.inTransitShipments}
                    </span>
                  </div>
                  <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col justify-between">
                    <div className="flex justify-between items-start mb-2">
                      <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Sử dụng xe</span>
                      <span className="text-headline-md font-headline-md text-on-tertiary-container">
                        {rtStats?.avgVehicleUtilisation ?? 0}%
                      </span>
                    </div>
                    <div className="w-full bg-surface-container-high rounded-full h-2 mb-3 overflow-hidden">
                      <div
                        className="bg-on-tertiary-container h-2 rounded-full transition-all duration-500"
                        style={{ width: `${rtStats?.avgVehicleUtilisation ?? 0}%` }}
                      />
                    </div>
                  </div>
                  <div className="bg-error-container/20 rounded-xl p-card-padding card-shadow border border-error/20 flex flex-col justify-between relative overflow-hidden">
                    <div className="absolute top-0 right-0 w-24 h-24 bg-error/5 rounded-full -mr-8 -mt-8" />
                    <div className="flex justify-between items-start mb-4 relative z-10">
                      <span className="text-label-md font-label-md text-error uppercase tracking-wider font-bold">
                        Hàng tồn kho (&gt;3d)
                      </span>
                      <div className="w-8 h-8 rounded-full bg-error text-on-error flex items-center justify-center">
                        <span className="material-symbols-outlined text-[20px]">warning</span>
                      </div>
                    </div>
                    <div className="relative z-10">
                      <span className="text-display-lg font-display-lg text-error">
                        {rtStats?.hubItemsWaitingOver3Days ?? 0}
                      </span>
                      <span className="text-label-md font-label-md text-error ml-2">shipments</span>
                    </div>
                  </div>
                </div>

                {/* Charts Row 1: Revenue Trend + Revenue by Type */}
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-gutter mb-gutter">
                  <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col h-[380px]">
                    <div className="flex justify-between items-center mb-4">
                      <h3 className="text-headline-md font-headline-md text-on-surface flex items-center gap-2">
                        <span className="material-symbols-outlined text-primary">timeline</span> Xu hướng doanh thu
                      </h3>
                      <span className="text-xs text-on-surface-variant bg-surface-container px-2.5 py-1 rounded-full font-bold">
                        Theo tháng
                      </span>
                    </div>
                    <div className="flex-1">
                      {revenueChartData.length > 0 ? (
                        <ResponsiveContainer width="100%" height="100%">
                          <LineChart data={revenueChartData} margin={{ top: 5, right: 20, left: 10, bottom: 5 }}>
                            <CartesianGrid strokeDasharray="3 3" stroke="#e0e0e0" />
                            <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                            <YAxis tick={{ fontSize: 11 }} tickFormatter={(v) => `${(v / 1_000_000).toFixed(0)}tr`} />
                            <Tooltip formatter={(v) => fmtVND(Number(v))} />
                            <Line
                              type="monotone"
                              dataKey="DoanhThu"
                              stroke="#00288e"
                              strokeWidth={2.5}
                              dot={{ r: 4 }}
                              activeDot={{ r: 6 }}
                            />
                          </LineChart>
                        </ResponsiveContainer>
                      ) : (
                        <div className="flex items-center justify-center h-full text-on-surface-variant text-body-md">
                          Chưa có dữ liệu doanh thu
                        </div>
                      )}
                    </div>
                  </div>
                  <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col h-[380px]">
                    <div className="flex justify-between items-center mb-4">
                      <h3 className="text-headline-md font-headline-md text-on-surface flex items-center gap-2">
                        <span className="material-symbols-outlined text-primary">bar_chart</span> Doanh thu theo loại
                      </h3>
                    </div>
                    <div className="flex-1">
                      {revenueByTypeData.length > 0 ? (
                        <ResponsiveContainer width="100%" height="100%">
                          <BarChart data={revenueByTypeData} margin={{ top: 5, right: 20, left: 10, bottom: 5 }}>
                            <CartesianGrid strokeDasharray="3 3" stroke="#e0e0e0" />
                            <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                            <YAxis tick={{ fontSize: 11 }} tickFormatter={(v) => `${(v / 1_000_000).toFixed(0)}tr`} />
                            <Tooltip formatter={(v) => fmtVND(Number(v))} />
                            <Bar dataKey="value" radius={[6, 6, 0, 0]}>
                              {revenueByTypeData.map((entry, idx) => (
                                <Cell key={idx} fill={entry.fill} />
                              ))}
                            </Bar>
                          </BarChart>
                        </ResponsiveContainer>
                      ) : (
                        <div className="flex items-center justify-center h-full text-on-surface-variant text-body-md">
                          Chưa có dữ liệu
                        </div>
                      )}
                    </div>
                  </div>
                </div>

                {/* Charts Row 2: Shipment Status + Operational Summary */}
                <div className="grid grid-cols-1 lg:grid-cols-12 gap-gutter mb-gutter">
                  <div className="lg:col-span-5 bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col h-[350px]">
                    <div className="flex justify-between items-center mb-4">
                      <h3 className="text-headline-md font-headline-md text-on-surface flex items-center gap-2">
                        <span className="material-symbols-outlined text-primary">pie_chart</span> Trạng thái đơn hàng
                      </h3>
                      <span className="text-xs text-on-surface-variant bg-surface-container px-2.5 py-1 rounded-full font-bold">
                        Tổng: {fmtNumber(stats.totalShipments)}
                      </span>
                    </div>
                    <div className="flex-1 flex items-center justify-center">
                      {shipmentStatusData.length > 0 ? (
                        <ResponsiveContainer width="100%" height="100%">
                          <PieChart>
                            <Pie
                              data={shipmentStatusData}
                              cx="50%"
                              cy="50%"
                              innerRadius={50}
                              outerRadius={90}
                              paddingAngle={2}
                              dataKey="value"
                              label={({ name, percent }: any) => `${name} ${((percent || 0) * 100).toFixed(0)}%`}
                            >
                              {shipmentStatusData.map((entry, idx) => (
                                <Cell key={idx} fill={entry.fill} />
                              ))}
                            </Pie>
                            <Tooltip />
                          </PieChart>
                        </ResponsiveContainer>
                      ) : (
                        <div className="text-on-surface-variant text-body-md">Chưa có dữ liệu</div>
                      )}
                    </div>
                  </div>
                  <div className="lg:col-span-7 bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col h-[350px]">
                    <div className="flex justify-between items-center mb-4 pb-2 border-b border-outline-variant/30">
                      <h3 className="text-headline-md font-headline-md text-on-surface flex items-center gap-2">
                        <span className="material-symbols-outlined text-primary">analytics</span> Tổng quan vận hành
                      </h3>
                    </div>
                    <div className="flex-1 grid grid-cols-2 gap-4 auto-rows-min">
                      <div className="bg-primary/5 rounded-lg p-4 flex flex-col items-center justify-center text-center border border-primary/10">
                        <span className="material-symbols-outlined text-[28px] text-primary mb-1">route</span>
                        <span className="text-headline-md font-bold text-on-surface">{stats.activeTrips}</span>
                        <span className="text-label-md text-on-surface-variant">Chuyến hoạt động</span>
                      </div>
                      <div className="bg-secondary/5 rounded-lg p-4 flex flex-col items-center justify-center text-center border border-secondary/10">
                        <span className="material-symbols-outlined text-[28px] text-secondary mb-1">package_2</span>
                        <span className="text-headline-md font-bold text-on-surface">{stats.inTransitShipments}</span>
                        <span className="text-label-md text-on-surface-variant">Đơn vận chuyển</span>
                      </div>
                      <div className="bg-tertiary/5 rounded-lg p-4 flex flex-col items-center justify-center text-center border border-tertiary/10">
                        <span className="material-symbols-outlined text-[28px] text-tertiary mb-1">local_shipping</span>
                        <span className="text-headline-md font-bold text-on-surface">{stats.totalDrivers}</span>
                        <span className="text-label-md text-on-surface-variant">Tổng tài xế</span>
                      </div>
                      <div className="bg-surface-container rounded-lg p-4 flex flex-col items-center justify-center text-center border border-outline-variant/20">
                        <span className="material-symbols-outlined text-[28px] text-on-surface-variant mb-1">inventory_2</span>
                        <span className="text-headline-md font-bold text-on-surface">{fmtNumber(stats.totalShipments)}</span>
                        <span className="text-label-md text-on-surface-variant">Tổng đơn hàng</span>
                      </div>
                      <div className="bg-error/5 rounded-lg p-4 flex flex-col items-center justify-center text-center border border-error/10 col-span-2">
                        <span className="material-symbols-outlined text-[28px] text-error mb-1">warning</span>
                        <span className="text-headline-md font-bold text-error">{rtStats?.hubItemsWaitingOver3Days ?? 0}</span>
                        <span className="text-label-md text-error">Hàng tồn kho quá 3 ngày</span>
                      </div>
                    </div>
                  </div>
                </div>
              </>
            )
          )}
        </main>
      </div>
    </div>
  );
}
