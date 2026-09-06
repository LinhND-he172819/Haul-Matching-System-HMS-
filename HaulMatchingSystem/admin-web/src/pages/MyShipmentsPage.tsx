import { useEffect, useState } from 'react';
import {
  getMyShipments,
  type CustomerShipmentListItem,
  type PagedResult,
} from '../api/customerShipmentApi';
import {
  getCustomerDashboardStats,
  type CustomerDashboardStats,
} from '../api/customerDashboardApi';
import Toast from '../components/matching/Toast';
import AppHeader from '../components/AppHeader';

/* ─── Status Badge Mapping ────────────────────────────────────────── */

const STATUS_BADGE: Record<string, string> = {
  Draft: 'bg-gray-100 text-gray-600 border border-gray-200',
  PendingReview: 'bg-amber-50 text-amber-700 border border-amber-200',
  PendingDeposit: 'bg-orange-50 text-orange-700 border border-orange-200',
  In_Warehouse: 'bg-blue-50 text-blue-700 border border-blue-200',
  Matched: 'bg-purple-50 text-purple-700 border border-purple-200',
  In_Transit: 'bg-indigo-50 text-indigo-700 border border-indigo-200',
  Delivered: 'bg-teal-50 text-teal-700 border border-teal-200',
  Completed: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
  Cancelled: 'bg-red-50 text-red-700 border border-red-200',
};

const STATUS_LABELS: Record<string, string> = {
  Draft: 'Bản nháp',
  PendingReview: 'Chờ duyệt',
  PendingDeposit: 'Chờ đặt cọc',
  In_Warehouse: 'Đang lưu kho',
  Matched: 'Đã ghép chuyến',
  In_Transit: 'Đang vận chuyển',
  Delivered: 'Đã giao hàng',
  Completed: 'Hoàn tất',
  Cancelled: 'Đã hủy',
};

/* ─── Helpers ─────────────────────────────────────────────────────── */

const fmtVND = (n: number) =>
  new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(n);

/* ─── Component ───────────────────────────────────────────────────── */

type Props = {
  onSelectShipment: (shipmentId: string) => void;
  onLogout: () => void;
  onNavigate?: (page: string) => void;
};

export default function MyShipmentsPage({ onSelectShipment, onLogout, onNavigate }: Props) {
  const [data, setData] = useState<PagedResult<CustomerShipmentListItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [custStats, setCustStats] = useState<CustomerDashboardStats | null>(null);

  const loadData = async () => {
    setLoading(true);
    try {
      const result = await getMyShipments(page);
      setData(result);
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi tải dữ liệu', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [page]);

  useEffect(() => {
    getCustomerDashboardStats()
      .then(setCustStats)
      .catch(() => {});
  }, []);

  // Client-side filter
  const filteredItems = (data?.items ?? []).filter((item) => {
    const kw = search.toLowerCase();
    const matchSearch = !kw
      || item.shipmentCode.toLowerCase().includes(kw)
      || (item.commodity ?? '').toLowerCase().includes(kw)
      || (item.receiverName ?? '').toLowerCase().includes(kw)
      || (item.deliveryAddress ?? '').toLowerCase().includes(kw)
      || (item.tripCode ?? '').toLowerCase().includes(kw)
      || (item.originName ?? '').toLowerCase().includes(kw)
      || (item.destinationName ?? '').toLowerCase().includes(kw);
    const matchStatus = statusFilter === 'all' || item.status === statusFilter;
    return matchSearch && matchStatus;
  });

  const formatDate = (s?: string) =>
    s ? new Date(s).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' }) : '-';

  const formatWeight = (w: number) => `${w.toFixed(1)} kg`;

  return (
    <div className="min-h-screen bg-surface text-on-surface">
      {/* Shared App Header */}
      <AppHeader
        onLogout={onLogout}
        pages={[
          { label: 'Trang chủ', onClick: () => onNavigate?.('home') },        
          { label: 'Tạo đơn gửi hàng', onClick: () => onNavigate?.('create-shipment') },
          { label: 'Đơn hàng của tôi', onClick: () => {}, active: true },
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
              placeholder="Tìm theo mã đơn, hàng hóa, người nhận, địa chỉ..."
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
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
              onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
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
              onClick={() => { setStatusFilter('all'); setPage(1); }}
              className="shrink-0 px-3 py-2 rounded-lg text-label-sm font-medium text-primary hover:bg-primary/8 transition-colors"
            >
              Xóa lọc
            </button>
          )}
        </div>

        {/* Customer Cost Summary Cards */}
        {custStats && (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 mb-5">
            <div className="bg-surface-container-lowest rounded-xl p-4 card-shadow border border-outline-variant/20 flex flex-col justify-between">
              <div className="flex justify-between items-center mb-2">
                <span className="text-label-md text-on-surface-variant uppercase tracking-wider">Tổng chi phí ước tính</span>
                <span className="material-symbols-outlined text-[20px] text-primary">payments</span>
              </div>
              <span className="text-headline-md font-bold text-on-surface">{fmtVND(custStats.totalEstimatedCost)}</span>
            </div>
            <div className="bg-surface-container-lowest rounded-xl p-4 card-shadow border border-outline-variant/20 flex flex-col justify-between">
              <div className="flex justify-between items-center mb-2">
                <span className="text-label-md text-on-surface-variant uppercase tracking-wider">Tổng đặt cọc yêu cầu</span>
                <span className="material-symbols-outlined text-[20px] text-amber-600">account_balance_wallet</span>
              </div>
              <span className="text-headline-md font-bold text-on-surface">{fmtVND(custStats.totalDepositRequired)}</span>
            </div>
            <div className="bg-surface-container-lowest rounded-xl p-4 card-shadow border border-outline-variant/20 flex flex-col justify-between">
              <div className="flex justify-between items-center mb-2">
                <span className="text-label-md text-on-surface-variant uppercase tracking-wider">Đã thanh toán</span>
                <span className="material-symbols-outlined text-[20px] text-emerald-600">check_circle</span>
              </div>
              <span className="text-headline-md font-bold text-emerald-600">{fmtVND(custStats.totalPaid)}</span>
              <span className="text-body-sm text-on-surface-variant mt-1">
                Cọc: {fmtVND(custStats.depositPaid)} · Final: {fmtVND(custStats.finalPaid)}
              </span>
            </div>
            <div className="bg-surface-container-lowest rounded-xl p-4 card-shadow border border-outline-variant/20 flex flex-col justify-between">
              <div className="flex justify-between items-center mb-2">
                <span className="text-label-md text-on-surface-variant uppercase tracking-wider">Còn nợ</span>
                <span className="material-symbols-outlined text-[20px] text-red-600">money_off</span>
              </div>
              <span className={`text-headline-md font-bold ${custStats.outstandingAmount > 0 ? 'text-red-600' : 'text-emerald-600'}`}>
                {fmtVND(custStats.outstandingAmount)}
              </span>
              {custStats.outstandingAmount > 0 && (
                <span className="text-body-sm text-red-500 mt-1">Cần thanh toán để hoàn tất</span>
              )}
            </div>
          </div>
        )}

        {loading ? (
          <div className="flex flex-col items-center justify-center py-20 gap-3">
            <span className="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
            <p className="text-body-md text-on-surface-variant">Đang tải danh sách...</p>
          </div>
        ) : !data || data.items.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 gap-4">
            <div className="w-20 h-20 rounded-full bg-surface-container-high flex items-center justify-center">
              <span className="material-symbols-outlined text-[40px] text-on-surface-variant/40">inventory_2</span>
            </div>
            <div className="text-center">
              <p className="text-title-md text-on-surface mb-1">Chưa có đơn hàng nào</p>
              <p className="text-body-md text-on-surface-variant">Hãy tạo đơn hàng mới để bắt đầu</p>
            </div>
          </div>
        ) : (
          <>
            {/* Summary bar */}
            <div className="flex items-center justify-between mb-4">
              <p className="text-body-sm text-on-surface-variant">
                {filteredItems.length === data.items.length
                  ? `${data.totalItems} đơn hàng`
                  : `${filteredItems.length} / ${data.totalItems} đơn hàng`}
              </p>
              <p className="text-body-sm text-on-surface-variant">
                Trang {data.page}/{data.totalPages}
              </p>
            </div>

            {/* Shipment cards */}
            <div className="space-y-3">
              {filteredItems.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-12 gap-3">
                  <span className="material-symbols-outlined text-[36px] text-on-surface-variant/30">search_off</span>
                  <p className="text-body-md text-on-surface-variant">Không tìm thấy đơn hàng phù hợp</p>
                </div>
              ) : filteredItems.map((item) => (
                <button
                  key={item.id}
                  onClick={() => onSelectShipment(item.id)}
                  className="w-full bg-surface-container-lowest rounded-2xl border border-outline-variant p-4 text-left hover:shadow-md transition-all group"
                >
                  {/* Top row: code + status */}
                  <div className="flex items-start justify-between mb-3">
                    <div>
                      <span className="text-label-lg font-bold text-on-surface group-hover:text-primary transition-colors">
                        {item.shipmentCode}
                      </span>
                      {item.tripCode && (
                        <span className="ml-2 text-label-sm text-on-surface-variant">
                          • {item.tripCode}
                        </span>
                      )}
                    </div>
                    <span className={`text-label-sm font-medium px-2.5 py-1 rounded-lg ${STATUS_BADGE[item.status] || 'bg-gray-100 text-gray-600'}`}>
                      {STATUS_LABELS[item.status] || item.status}
                    </span>
                  </div>

                  {/* Route */}
                  {(item.originName || item.destinationName) && (
                    <div className="flex items-center gap-2 mb-3">
                      <span className="material-symbols-outlined text-[16px] text-primary">route</span>
                      <span className="text-body-sm text-on-surface">
                        {item.originName || '?'} → {item.destinationName || '?'}
                      </span>
                    </div>
                  )}

                  {/* Bottom row: cargo + date */}
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-4 text-body-sm text-on-surface-variant">
                      {item.commodity && (
                        <span className="flex items-center gap-1">
                          <span className="material-symbols-outlined text-[14px]">inventory_2</span>
                          {item.commodity}
                        </span>
                      )}
                      <span className="flex items-center gap-1">
                        <span className="material-symbols-outlined text-[14px]">scale</span>
                        {formatWeight(item.weight)}
                      </span>
                      {item.receiverName && (
                        <span className="flex items-center gap-1">
                          <span className="material-symbols-outlined text-[14px]">person</span>
                          {item.receiverName}
                        </span>
                      )}
                    </div>
                    <span className="text-body-sm text-on-surface-variant">
                      {formatDate(item.createdAt)}
                    </span>
                  </div>

                  {/* Delivery address */}
                  {item.deliveryAddress && (
                    <div className="mt-2 flex items-center gap-2 text-body-sm text-on-surface-variant">
                      <span className="material-symbols-outlined text-[14px]">location_on</span>
                      <span className="truncate">{item.deliveryAddress}</span>
                    </div>
                  )}
                </button>
              ))}
            </div>

            {/* Pagination */}
            {data.totalPages > 1 && filteredItems.length > 0 && (
              <div className="flex items-center justify-center gap-2 mt-6">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page <= 1}
                  className="px-4 py-2 rounded-xl border border-outline-variant text-label-md disabled:opacity-40 hover:bg-surface-container-low transition-colors"
                >
                  Trước
                </button>
                <span className="text-body-sm text-on-surface-variant px-3">
                  {page} / {data.totalPages}
                </span>
                <button
                  onClick={() => setPage((p) => Math.min(data.totalPages, p + 1))}
                  disabled={page >= data.totalPages}
                  className="px-4 py-2 rounded-xl border border-outline-variant text-label-md disabled:opacity-40 hover:bg-surface-container-low transition-colors"
                >
                  Sau
                </button>
              </div>
            )}
          </>
        )}
      </div>

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
