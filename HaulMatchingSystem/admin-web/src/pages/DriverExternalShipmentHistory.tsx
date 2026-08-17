import { useEffect, useState } from 'react';
import {
  getExternalShipments,
  type ExternalShipmentListItem,
  type PagedResult,
} from '../api/driverExternalShipmentApi';
import Toast from '../components/matching/Toast';
import AppHeader from '../components/AppHeader';

/* ─── Status Badge Mapping ────────────────────────────────────────── */

const PROPOSAL_STATUS_BADGE: Record<string, string> = {
  PendingReview: 'bg-amber-50 text-amber-700 border border-amber-200',
  Approved: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
  Rejected: 'bg-red-50 text-red-700 border border-red-200',
  Cancelled: 'bg-gray-100 text-gray-600 border border-gray-200',
};

const PROPOSAL_STATUS_LABELS: Record<string, string> = {
  PendingReview: 'Chờ xét duyệt',
  Approved: 'Đã chấp nhận',
  Rejected: 'Bị từ chối',
  Cancelled: 'Đã hủy',
};

/* ─── Props ───────────────────────────────────────────────────────── */

type Props = {
  onSelectDetail?: (proposalId: string) => void;
  onLogout: () => void;
  onNavigate?: (page: string) => void;
};

/* ─── Component ───────────────────────────────────────────────────── */

export default function DriverExternalShipmentHistory({
  onSelectDetail,
  onLogout,
  onNavigate,
}: Props) {
  const [data, setData] = useState<PagedResult<ExternalShipmentListItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [page, setPage] = useState(1);
  const pageSize = 10;

  const loadData = async () => {
    setLoading(true);
    try {
      const result = await getExternalShipments({
        status: statusFilter === 'all' ? undefined : statusFilter,
        page,
        pageSize,
      });
      setData(result);
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi tải dữ liệu', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    setPage(1);
  }, [statusFilter]);

  useEffect(() => {
    loadData();
  }, [page, statusFilter]);

  // Client-side search filter
  const filteredItems = (data?.items ?? []).filter((item) => {
    if (!search.trim()) return true;
    const kw = search.toLowerCase();
    return (
      (item.shipmentCode ?? '').toLowerCase().includes(kw) ||
      (item.proposalCode ?? '').toLowerCase().includes(kw) ||
      item.receiverName.toLowerCase().includes(kw) ||
      (item.destAddress ?? '').toLowerCase().includes(kw) ||
      item.category.toLowerCase().includes(kw)
    );
  });

  const totalPages = data?.totalPages ?? 1;

  const formatDate = (s?: string) =>
    s
      ? new Date(s).toLocaleDateString('vi-VN', {
          day: '2-digit',
          month: '2-digit',
          year: 'numeric',
          hour: '2-digit',
          minute: '2-digit',
        })
      : '-';

  return (
    <div className="min-h-screen bg-surface text-on-surface">
      <AppHeader
        onLogout={onLogout}
        pages={[
          { label: 'Chuyến đi của tôi', onClick: () => onNavigate?.('driver-trips-v2') },
          { label: 'Đơn ngoài hệ thống', onClick: () => {}, active: true },
        ]}
      />

      <div className="max-w-4xl mx-auto px-4 py-6">
        {/* Header */}
        <div className="flex items-center justify-between mb-5">
          <div>
            <h1 className="text-title-xl font-bold text-on-surface">Đơn hàng ngoài hệ thống</h1>
            <p className="text-body-sm text-on-surface-variant mt-1">
              Danh sách đơn hàng do tài xế khai báo
            </p>
          </div>
          <button
            onClick={() => onNavigate?.('driver-external-create')}
            className="px-4 py-2.5 rounded-xl text-label-lg font-medium text-white bg-primary hover:bg-primary-dark transition-colors flex items-center gap-2"
          >
            <span className="material-symbols-outlined text-[18px]">add</span>
            Tạo đơn mới
          </button>
        </div>

        {/* Search & Filter */}
        <div className="flex items-center gap-3 mb-5">
          <div className="relative flex-1">
            <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant/50 text-[20px]">search</span>
            <input
              type="text"
              placeholder="Tìm theo mã đơn, người nhận, địa chỉ..."
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

          <div className="relative shrink-0 w-52">
            <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant/50 text-[20px] pointer-events-none">filter_list</span>
            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="w-full appearance-none pl-10 pr-10 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all cursor-pointer"
            >
              <option value="all">Tất cả trạng thái</option>
              <option value="PendingReview">Chờ xét duyệt</option>
              <option value="Approved">Đã chấp nhận</option>
              <option value="Rejected">Bị từ chối</option>
            </select>
            <span className="material-symbols-outlined absolute right-3 top-1/2 -translate-y-1/2 text-on-surface-variant/50 text-[18px] pointer-events-none">expand_more</span>
          </div>
        </div>

        {/* List */}
        {loading ? (
          <div className="flex flex-col items-center justify-center py-20 gap-3">
            <span className="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
            <p className="text-body-md text-on-surface-variant">Đang tải danh sách...</p>
          </div>
        ) : filteredItems.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 gap-4">
            <span className="material-symbols-outlined text-[56px] text-on-surface-variant/30">inventory_2</span>
            <p className="text-body-lg text-on-surface-variant">Không có đơn hàng nào</p>
            <button
              onClick={() => onNavigate?.('driver-external-create')}
              className="mt-2 px-5 py-2.5 rounded-xl text-label-lg font-medium text-primary border border-primary hover:bg-primary/8 transition-colors"
            >
              Tạo đơn mới
            </button>
          </div>
        ) : (
          <>
            <div className="space-y-3">
              {filteredItems.map((item) => (
                <button
                  key={item.proposalId}
                  onClick={() => onSelectDetail?.(item.proposalId)}
                  className="w-full text-left p-5 rounded-2xl border border-outline-variant bg-surface-container-lowest hover:bg-surface-container-low hover:shadow-sm transition-all group"
                >
                  <div className="flex items-start justify-between mb-3">
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="text-title-sm font-semibold text-on-surface group-hover:text-primary transition-colors">
                          {item.shipmentCode ?? item.proposalCode ?? `#${item.proposalId.slice(0, 8)}`}
                        </span>
                        <span className={`inline-flex px-2 py-0.5 rounded-full text-label-xs font-medium ${PROPOSAL_STATUS_BADGE[item.proposalStatus] ?? 'bg-gray-100 text-gray-600'}`}>
                          {PROPOSAL_STATUS_LABELS[item.proposalStatus] ?? item.proposalStatus}
                        </span>
                      </div>
                      <p className="text-body-sm text-on-surface-variant mt-1">{item.category}</p>
                    </div>
                    <span className="material-symbols-outlined text-on-surface-variant/40 group-hover:text-primary transition-colors">chevron_right</span>
                  </div>

                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 text-body-sm">
                    <div>
                      <span className="text-on-surface-variant">Khối lượng</span>
                      <p className="font-medium text-on-surface">{item.weightKg.toFixed(1)} kg</p>
                    </div>
                    <div>
                      <span className="text-on-surface-variant">Thể tích</span>
                      <p className="font-medium text-on-surface">{item.volumeCbm.toFixed(2)} m³</p>
                    </div>
                    <div>
                      <span className="text-on-surface-variant">Người nhận</span>
                      <p className="font-medium text-on-surface truncate">{item.receiverName}</p>
                    </div>
                    <div>
                      <span className="text-on-surface-variant">Ngày tạo</span>
                      <p className="font-medium text-on-surface">{formatDate(item.createdAt)}</p>
                    </div>
                  </div>

                  {item.quotationStatus && (
                    <div className="mt-3 pt-3 border-t border-outline-variant/50 flex items-center gap-2 text-body-sm">
                      <span className="material-symbols-outlined text-[16px] text-on-surface-variant">receipt_long</span>
                      <span className="text-on-surface-variant">Báo giá:</span>
                      <span className="font-medium text-on-surface">{item.quotationStatus}</span>
                    </div>
                  )}
                </button>
              ))}
            </div>

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="flex items-center justify-center gap-3 mt-6">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="px-4 py-2 rounded-lg text-label-md font-medium text-on-surface-variant hover:bg-surface-container-high disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  Trước
                </button>
                <span className="text-body-sm text-on-surface-variant">
                  {page} / {totalPages}
                </span>
                <button
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  disabled={page === totalPages}
                  className="px-4 py-2 rounded-lg text-label-md font-medium text-on-surface-variant hover:bg-surface-container-high disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  Sau
                </button>
              </div>
            )}
          </>
        )}
      </div>

      {toast && (
        <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />
      )}
    </div>
  );
}
