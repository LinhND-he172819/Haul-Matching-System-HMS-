/**
 * IncidentListPage — Shared admin/staff incident management list.
 * Admin sees all incidents; Warehouse_Staff sees only their hub's incidents (enforced server-side).
 */
import { useCallback, useEffect, useState } from 'react';
import {
  listIncidents,
  type AdminIncidentPagedResult,
} from '../api/incidentApi';

/* ─── Constants ─────────────────────────────────────────────────── */

const STATUS_BADGE: Record<string, string> = {
  Open: 'bg-amber-50 text-amber-700 border border-amber-200',
  InProgress: 'bg-blue-50 text-blue-700 border border-blue-200',
  Resolved: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
  Rejected: 'bg-red-50 text-red-700 border border-red-200',
};

const STATUS_LABEL: Record<string, string> = {
  Open: 'Mới',
  InProgress: 'Đang xử lý',
  Resolved: 'Đã giải quyết',
  Rejected: 'Đã từ chối',
};

const INCIDENT_TYPE_BADGE: Record<string, string> = {
  Delay: 'bg-orange-50 text-orange-700',
  VehicleBreakdown: 'bg-red-50 text-red-700',
  Accident: 'bg-red-100 text-red-800',
  CargoDamage: 'bg-yellow-50 text-yellow-700',
  CargoLost: 'bg-red-50 text-red-800',
  DeliveryProblem: 'bg-purple-50 text-purple-700',
  RouteProblem: 'bg-indigo-50 text-indigo-700',
  Weather: 'bg-sky-50 text-sky-700',
  Other: 'bg-gray-50 text-gray-700',
};

const INCIDENT_TYPE_LABEL: Record<string, string> = {
  Delay: 'Trễ hạn',
  VehicleBreakdown: 'Hỏng xe',
  Accident: 'Tai nạn',
  CargoDamage: 'Hư hỏng hàng',
  CargoLost: 'Mất hàng',
  DeliveryProblem: 'Sự cố giao',
  RouteProblem: 'Sự cố tuyến',
  Weather: 'Thời tiết',
  Other: 'Khác',
};

/* ─── Props ─────────────────────────────────────────────────────── */

type Props = {
  onLogout: () => void;
  onSelectIncident: (incidentId: string) => void;
};

/* ─── Component ─────────────────────────────────────────────────── */

export default function IncidentListPage({ onLogout: _onLogout, onSelectIncident }: Props) {
  const [data, setData] = useState<AdminIncidentPagedResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState('');
  const [typeFilter, setTypeFilter] = useState('');
  const [searchText, setSearchText] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');

  // Debounce search
  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(searchText), 400);
    return () => clearTimeout(t);
  }, [searchText]);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const result = await listIncidents({
        page,
        pageSize: 10,
        status: statusFilter || undefined,
        incidentType: typeFilter || undefined,
        search: debouncedSearch || undefined,
      });
      setData(result);
    } catch {
      // errors silently shown as empty list
    } finally {
      setLoading(false);
    }
  }, [page, statusFilter, typeFilter, debouncedSearch]);

  useEffect(() => { fetchData(); }, [fetchData]);

  // Reset page when filters change
  useEffect(() => { setPage(1); }, [statusFilter, typeFilter, debouncedSearch]);

  const totalPages = data?.totalPages ?? 1;

  /* ─── Loading Skeleton ─────────────────────────────────────────── */

  if (loading && !data) {
    return (
      <div className="p-6 xl:p-8 space-y-4 max-w-7xl mx-auto">
        <div className="h-8 w-48 bg-surface-container-highest rounded-lg animate-pulse" />
        {[1, 2, 3].map(i => (
          <div key={i} className="bg-surface-container-low rounded-2xl border border-outline-variant p-5 animate-pulse">
            <div className="flex gap-4">
              <div className="w-12 h-12 bg-surface-container-highest rounded-xl" />
              <div className="flex-1 space-y-2">
                <div className="h-4 bg-surface-container-highest rounded w-1/3" />
                <div className="h-3 bg-surface-container-highest rounded w-1/2" />
              </div>
            </div>
          </div>
        ))}
      </div>
    );
  }

  /* ─── Render ───────────────────────────────────────────────────── */

  return (
    <div className="p-6 xl:p-8 space-y-6 max-w-7xl mx-auto">
      {/* Header */}
      <div>
        <h1 className="text-headline-lg font-headline-lg text-on-surface flex items-center gap-3">
          <span className="material-symbols-outlined text-[28px] text-error">warning</span>
          Quản lý sự cố
        </h1>
        <p className="text-body-md text-on-surface-variant mt-1">
          {data?.totalCount ?? 0} sự cố — Trang {page}/{totalPages}
        </p>
      </div>

      {/* Filters */}
      <div className="bg-surface-container-low rounded-2xl border border-outline-variant p-4">
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          {/* Search */}
          <div className="relative">
            <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant/50 text-[20px]">search</span>
            <input
              type="text"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              placeholder="Tìm mã sự cố, mã chuyến, tài xế..."
              className="w-full pl-10 pr-4 py-2.5 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md placeholder:text-on-surface-variant/50 focus:outline-none focus:border-primary"
            />
          </div>

          {/* Status Filter */}
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="px-4 py-2.5 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md focus:outline-none focus:border-primary"
          >
            <option value="">Tất cả trạng thái</option>
            <option value="Open">Mới</option>
            <option value="InProgress">Đang xử lý</option>
            <option value="Resolved">Đã giải quyết</option>
            <option value="Rejected">Đã từ chối</option>
          </select>

          {/* Type Filter */}
          <select
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
            className="px-4 py-2.5 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md focus:outline-none focus:border-primary"
          >
            <option value="">Tất cả loại</option>
            {Object.entries(INCIDENT_TYPE_LABEL).map(([key, label]) => (
              <option key={key} value={key}>{label}</option>
            ))}
          </select>
        </div>
      </div>

      {/* Empty State */}
      {!loading && data && data.items.length === 0 && (
        <div className="bg-surface-container-low rounded-2xl border border-outline-variant p-12 text-center">
          <span className="material-symbols-outlined text-[48px] text-on-surface-variant/30 mb-3">check_circle</span>
          <p className="text-title-lg font-bold text-on-surface-variant">Không có sự cố nào</p>
          <p className="text-body-md text-on-surface-variant/70 mt-1">Không tìm thấy sự cố phù hợp với bộ lọc.</p>
        </div>
      )}

      {/* Incident Cards */}
      <div className="space-y-3">
        {data?.items.map((inc) => (
          <button
            key={inc.id}
            onClick={() => onSelectIncident(inc.id)}
            className="w-full text-left bg-surface-container-lowest rounded-2xl border border-outline-variant p-5 hover:shadow-md hover:border-primary/30 transition-all duration-200 group"
          >
            <div className="flex items-start gap-4">
              {/* Icon */}
              <div className={`w-12 h-12 rounded-xl flex items-center justify-center shrink-0 ${INCIDENT_TYPE_BADGE[inc.incidentType] || 'bg-gray-50 text-gray-600'}`}>
                <span className="material-symbols-outlined text-[22px]">
                  {inc.incidentType === 'VehicleBreakdown' ? 'car_repair' :
                   inc.incidentType === 'Accident' ? 'car_crash' :
                   inc.incidentType === 'CargoDamage' ? 'inventory_2' :
                   inc.incidentType === 'CargoLost' ? 'search_off' :
                   inc.incidentType === 'Weather' ? 'cloud' :
                   inc.incidentType === 'RouteProblem' ? 'route' :
                   inc.incidentType === 'DeliveryProblem' ? 'local_shipping' :
                   inc.incidentType === 'Delay' ? 'schedule' : 'warning'}
                </span>
              </div>

              {/* Content */}
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="text-label-lg font-bold text-on-surface">
                    {inc.incidentCode || 'N/A'}
                  </span>
                  <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-label-sm font-medium ${STATUS_BADGE[inc.status] || ''}`}>
                    {STATUS_LABEL[inc.status] || inc.status}
                  </span>
                  <span className={`inline-flex items-center px-2 py-0.5 rounded-lg text-label-sm font-medium ${INCIDENT_TYPE_BADGE[inc.incidentType] || ''}`}>
                    {INCIDENT_TYPE_LABEL[inc.incidentType] || inc.incidentType}
                  </span>
                </div>

                <p className="text-body-md text-on-surface-variant mt-1 line-clamp-1">
                  {inc.description}
                </p>

                <div className="flex items-center gap-4 mt-2 flex-wrap">
                  <span className="inline-flex items-center gap-1 text-label-sm text-on-surface-variant">
                    <span className="material-symbols-outlined text-[14px]">route</span>
                    {inc.tripCode}
                  </span>
                  <span className="inline-flex items-center gap-1 text-label-sm text-on-surface-variant">
                    <span className="material-symbols-outlined text-[14px]">person</span>
                    {inc.driverName}
                  </span>
                  {inc.vehiclePlate && (
                    <span className="inline-flex items-center gap-1 text-label-sm text-on-surface-variant">
                      <span className="material-symbols-outlined text-[14px]">garage</span>
                      {inc.vehiclePlate}
                    </span>
                  )}
                  {inc.evidenceCount > 0 && (
                    <span className="inline-flex items-center gap-1 text-label-sm text-on-surface-variant">
                      <span className="material-symbols-outlined text-[14px]">image</span>
                      {inc.evidenceCount} ảnh
                    </span>
                  )}
                  <span className="text-label-sm text-on-surface-variant/60">
                    {new Date(inc.reportedAt).toLocaleString('vi-VN')}
                  </span>
                </div>
              </div>

              {/* Chevron */}
              <span className="material-symbols-outlined text-on-surface-variant/30 group-hover:text-primary transition-colors">
                chevron_right
              </span>
            </div>
          </button>
        ))}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between">
          <button
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={page <= 1}
            className="flex items-center gap-1 px-4 py-2 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low disabled:opacity-40 disabled:cursor-not-allowed transition-colors text-label-md font-bold"
          >
            <span className="material-symbols-outlined text-[18px]">chevron_left</span>
            Trước
          </button>
          <span className="text-label-md text-on-surface-variant">
            {page} / {totalPages}
          </span>
          <button
            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
            disabled={page >= totalPages}
            className="flex items-center gap-1 px-4 py-2 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low disabled:opacity-40 disabled:cursor-not-allowed transition-colors text-label-md font-bold"
          >
            Sau
            <span className="material-symbols-outlined text-[18px]">chevron_right</span>
          </button>
        </div>
      )}
    </div>
  );
}
