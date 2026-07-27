import { useEffect, useState } from 'react';
import {
  getDriverTrips,
  type DriverTripListItem,
} from '../api/driverTripApi';
import Toast from '../components/matching/Toast';
import AppHeader from '../components/AppHeader';

/* ─── Status Badge Mapping ────────────────────────────────────────── */

const STATUS_BADGE: Record<string, string> = {
  Scheduled: 'bg-gray-100 text-gray-600 border border-gray-200',
  Ready: 'bg-blue-50 text-blue-700 border border-blue-200',
  InProgress: 'bg-indigo-50 text-indigo-700 border border-indigo-200',
  Active: 'bg-indigo-50 text-indigo-700 border border-indigo-200',
  Completed: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
  Cancelled: 'bg-red-50 text-red-700 border border-red-200',
  Breakdown: 'bg-orange-50 text-orange-700 border border-orange-200',
};

const STATUS_LABELS: Record<string, string> = {
  Scheduled: 'Đã lên lịch',
  Ready: 'Sẵn sàng',
  InProgress: 'Đang thực hiện',
  Active: 'Đang hoạt động',
  Completed: 'Hoàn tất',
  Cancelled: 'Đã hủy',
  Breakdown: 'Hỏng xe',
};

/* ─── Props ───────────────────────────────────────────────────────── */

type Props = {
  onSelectTrip: (tripId: string) => void;
  onLogout: () => void;
  onNavigate?: (page: string) => void;
};

/* ─── Component ───────────────────────────────────────────────────── */

export default function DriverTripsPageV2({ onSelectTrip, onLogout, onNavigate }: Props) {
  const [trips, setTrips] = useState<DriverTripListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');

  const loadTrips = async () => {
    setLoading(true);
    try {
      const result = await getDriverTrips();
      setTrips(result.items);
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi tải dữ liệu', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadTrips();
  }, []);

  // Client-side filter
  const filteredTrips = trips.filter((trip) => {
    const kw = search.toLowerCase();
    const matchSearch = !kw
      || trip.tripCode.toLowerCase().includes(kw)
      || (trip.vehiclePlate ?? '').toLowerCase().includes(kw)
      || (trip.originName ?? '').toLowerCase().includes(kw)
      || (trip.destinationName ?? '').toLowerCase().includes(kw);
    const matchStatus = statusFilter === 'all' || trip.status === statusFilter;
    return matchSearch && matchStatus;
  });

  const formatDate = (s?: string) =>
    s ? new Date(s).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '-';

  const formatCapacity = (current: number, max: number, unit: string) =>
    `${current.toFixed(1)} / ${max.toFixed(1)} ${unit}`;

  const capacityPercent = (current: number, max: number) =>
    max > 0 ? Math.round((current / max) * 100) : 0;

  return (
    <div className="min-h-screen bg-surface text-on-surface">
      {/* Shared App Header */}
      <AppHeader
        onLogout={onLogout}
        pages={[
          { label: 'Chuyến đi của tôi', onClick: () => {}, active: true },
        ]}
      />

      {/* Content */}
      <div className="max-w-4xl mx-auto px-4 py-6">
        {/* Search & Filter Bar */}
        <div className="flex items-center gap-3 mb-5">
          {/* Search Input */}
          <div className="relative flex-1">
            <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant/50 text-[20px]">search</span>
            <input
              type="text"
              placeholder="Tìm theo mã chuyến, biển số, tuyến đường..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full pl-10 pr-10 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md placeholder:text-on-surface-variant/50 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all"
            />
            {search && (
              <button
                onClick={() => setSearch('')}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-on-surface-variant/50 hover:text-on-surface transition-colors"
              >
                <span className="material-symbols-outlined text-[18px]">close</span>
              </button>
            )}
          </div>

          {/* Status Filter Dropdown */}
          <div className="relative shrink-0 w-52">
            <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant/50 text-[20px] pointer-events-none">filter_list</span>
            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="w-full appearance-none pl-10 pr-10 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all cursor-pointer"
            >
              <option value="all">Tất cả trạng thái</option>
              {Object.entries(STATUS_LABELS).map(([key, label]) => (
                <option key={key} value={key}>{label}</option>
              ))}
            </select>
            <span className="material-symbols-outlined absolute right-3 top-1/2 -translate-y-1/2 text-on-surface-variant/50 text-[18px] pointer-events-none">expand_more</span>
          </div>
          {statusFilter !== 'all' && (
            <button
              onClick={() => setStatusFilter('all')}
              className="shrink-0 px-3 py-2 rounded-lg text-label-sm font-medium text-primary hover:bg-primary/8 transition-colors"
            >
              Xóa lọc
            </button>
          )}
        </div>

        {loading ? (
          <div className="flex flex-col items-center justify-center py-20 gap-3">
            <span className="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
            <p className="text-body-md text-on-surface-variant">Đang tải danh sách chuyến...</p>
          </div>
        ) : filteredTrips.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 gap-4">
            <div className="w-20 h-20 rounded-full bg-surface-container-high flex items-center justify-center">
              <span className="material-symbols-outlined text-[40px] text-on-surface-variant/40">
                {trips.length === 0 ? 'route' : 'search_off'}
              </span>
            </div>
            <div className="text-center">
              <p className="text-title-md text-on-surface mb-1">
                {trips.length === 0 ? 'Chưa có chuyến đi nào' : 'Không tìm thấy chuyến đi phù hợp'}
              </p>
              <p className="text-body-md text-on-surface-variant">
                {trips.length === 0 ? 'Bạn chưa được giao chuyến đi nào' : 'Thử thay đổi từ khóa hoặc bộ lọc'}
              </p>
            </div>
          </div>
        ) : (
          <div className="space-y-3">
            {filteredTrips.map((trip) => (
              <button
                key={trip.id}
                onClick={() => onSelectTrip(trip.id)}
                className="w-full bg-surface-container-lowest rounded-2xl border border-outline-variant p-5 text-left hover:shadow-md transition-all group"
              >
                {/* Top row: code + status */}
                <div className="flex items-start justify-between mb-3">
                  <div>
                    <span className="text-label-lg font-bold text-on-surface group-hover:text-primary transition-colors">
                      {trip.tripCode}
                    </span>
                    {trip.vehiclePlate && (
                      <span className="ml-2 text-label-sm text-on-surface-variant">
                        • {trip.vehiclePlate}
                      </span>
                    )}
                  </div>
                  <span className={`text-label-sm font-medium px-2.5 py-1 rounded-lg ${STATUS_BADGE[trip.status] || 'bg-gray-100 text-gray-600'}`}>
                    {STATUS_LABELS[trip.status] || trip.status}
                  </span>
                </div>

                {/* Route */}
                <div className="flex items-center gap-2 mb-3">
                  <span className="material-symbols-outlined text-[16px] text-primary">route</span>
                  <span className="text-body-md text-on-surface">
                    {trip.originName || '?'} → {trip.destinationName || '?'}
                  </span>
                </div>

                {/* Capacity bars */}
                <div className="grid grid-cols-2 gap-4 mb-3">
                  <div>
                    <div className="flex justify-between text-body-sm mb-1">
                      <span className="text-on-surface-variant">Trọng lượng</span>
                      <span className="text-on-surface font-medium">{capacityPercent(trip.currentWeight, trip.currentWeight + trip.remainingWeight)}%</span>
                    </div>
                    <div className="w-full h-2 bg-surface-container-high rounded-full overflow-hidden">
                      <div
                        className="h-full bg-primary rounded-full transition-all"
                        style={{ width: `${capacityPercent(trip.currentWeight, trip.currentWeight + trip.remainingWeight)}%` }}
                      />
                    </div>
                    <p className="text-body-sm text-on-surface-variant mt-1">{formatCapacity(trip.currentWeight, trip.currentWeight + trip.remainingWeight, 'kg')}</p>
                  </div>
                  <div>
                    <div className="flex justify-between text-body-sm mb-1">
                      <span className="text-on-surface-variant">Thể tích</span>
                      <span className="text-on-surface font-medium">{capacityPercent(trip.currentVolume, trip.currentVolume + trip.remainingVolume)}%</span>
                    </div>
                    <div className="w-full h-2 bg-surface-container-high rounded-full overflow-hidden">
                      <div
                        className="h-full bg-tertiary rounded-full transition-all"
                        style={{ width: `${capacityPercent(trip.currentVolume, trip.currentVolume + trip.remainingVolume)}%` }}
                      />
                    </div>
                    <p className="text-body-sm text-on-surface-variant mt-1">{formatCapacity(trip.currentVolume, trip.currentVolume + trip.remainingVolume, 'm³')}</p>
                  </div>
                </div>

                {/* Bottom row */}
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2 text-body-sm text-on-surface-variant">
                    <span className="material-symbols-outlined text-[14px]">inventory_2</span>
                    {trip.totalShipments} kiện hàng
                  </div>
                  {trip.departureTime && (
                    <span className="text-body-sm text-on-surface-variant">
                      {formatDate(trip.departureTime)}
                    </span>
                  )}
                </div>
              </button>
            ))}
          </div>
        )}
      </div>

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
