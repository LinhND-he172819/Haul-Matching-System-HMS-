import { useState, useEffect, useCallback } from 'react';
import AppHeader from '../../components/AppHeader';
import Toast from '../../components/matching/Toast';
import {
  getStaffQuotations,
  type QuotationListItem,
  type PagedResult,
} from '../../api/staff/staffQuotationApi';

/* ─── Constants ─────────────────────────────────────────────────── */

const STATUS_TABS = [
  { key: '', label: 'Tất cả' },
  { key: 'Draft', label: 'Bản nháp' },
  { key: 'Sent', label: 'Đã gửi' },
  { key: 'Accepted', label: 'Đã chấp nhận' },
  { key: 'Expired', label: 'Đã hết hạn' },
  { key: 'Cancelled', label: 'Đã hủy' },
];

const STATUS_BADGE: Record<string, string> = {
  Draft: 'bg-gray-100 text-gray-600 border border-gray-200',
  Sent: 'bg-amber-50 text-amber-700 border border-amber-200',
  Accepted: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
  Expired: 'bg-gray-100 text-gray-500 border border-gray-200',
  Cancelled: 'bg-rose-50 text-rose-700 border border-rose-200',
};

const STATUS_LABELS: Record<string, string> = {
  Draft: 'Bản nháp',
  Sent: 'Đã gửi',
  Accepted: 'Đã chấp nhận',
  Expired: 'Đã hết hạn',
  Cancelled: 'Đã hủy',
};

/* ─── Props ─────────────────────────────────────────────────────── */

type Props = {
  onLogout: () => void;
  onSelectQuotation: (quotationId: string) => void;
};

/* ─── Component ─────────────────────────────────────────────────── */

export default function StaffQuotationManagementPage({
  onLogout,
  onSelectQuotation,
}: Props) {
  const [data, setData] = useState<PagedResult<QuotationListItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState('');
  const [page, setPage] = useState(1);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const result = await getStaffQuotations({
        status: activeTab || undefined,
        page,
        pageSize: 10,
      });
      setData(result);
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi tải dữ liệu', type: 'error' });
    } finally {
      setLoading(false);
    }
  }, [activeTab, page]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const formatCurrency = (n: number) =>
    n.toLocaleString('vi-VN', { style: 'currency', currency: 'VND' });

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

  const navPages = [
    { label: 'Đề xuất', onClick: () => {} },
    { label: 'Báo giá', onClick: () => {}, active: true },
    { label: 'Thanh toán', onClick: () => {} },
  ];

  return (
    <div className="min-h-screen bg-surface">
      <AppHeader onLogout={onLogout} pages={navPages} />

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
        {/* Page Header */}
        <div className="mb-6">
          <h1 className="text-headline-lg font-bold text-on-surface flex items-center gap-3">
            <span className="material-symbols-outlined text-primary">request_quote</span>
            Quản lý báo giá
          </h1>
          <p className="text-body-md text-on-surface-variant mt-1">
            Quản lý báo giá cho khách hàng
          </p>
        </div>

        {/* Status Tabs */}
        <div className="flex gap-2 mb-6 overflow-x-auto pb-2">
          {STATUS_TABS.map((tab) => (
            <button
              key={tab.key}
              onClick={() => {
                setActiveTab(tab.key);
                setPage(1);
              }}
              className={`px-4 py-2 rounded-xl text-label-md font-semibold whitespace-nowrap transition-all ${
                activeTab === tab.key
                  ? 'bg-primary text-on-primary shadow-sm'
                  : 'bg-white text-on-surface-variant border border-outline-variant hover:bg-surface-container-low'
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {/* Loading */}
        {loading && (
          <div className="space-y-4">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-36 bg-white rounded-2xl animate-pulse border border-outline-variant" />
            ))}
          </div>
        )}

        {/* Empty */}
        {!loading && data && data.items.length === 0 && (
          <div className="text-center py-16 bg-white rounded-2xl border border-outline-variant">
            <span className="material-symbols-outlined text-[48px] text-gray-300">receipt_long</span>
            <p className="text-title-lg font-semibold text-on-surface-variant mt-3">
              Không có báo giá nào
            </p>
            <p className="text-body-md text-on-surface-variant mt-1">
              {activeTab ? 'Thử chuyển tab khác' : 'Chưa có báo giá nào được tạo'}
            </p>
          </div>
        )}

        {/* Quotation Cards */}
        {!loading && data && data.items.length > 0 && (
          <>
            <div className="space-y-4">
              {data.items.map((q) => (
                <div
                  key={q.id}
                  className="bg-white rounded-2xl border border-outline-variant p-5 hover:shadow-md transition-shadow cursor-pointer"
                  onClick={() => onSelectQuotation(q.id)}
                >
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-full bg-primary/10 flex items-center justify-center">
                        <span className="material-symbols-outlined text-primary text-xl">receipt_long</span>
                      </div>
                      <div>
                        <p className="text-title-lg font-bold text-on-surface">{q.quotationCode}</p>
                        <p className="text-label-sm text-on-surface-variant">
                          {q.shipmentCode} • {q.customerName}
                        </p>
                      </div>
                    </div>
                    <span
                      className={`inline-flex items-center gap-1 px-3 py-1.5 rounded-full text-label-sm font-semibold ${
                        STATUS_BADGE[q.status] || 'bg-gray-100 text-gray-600'
                      }`}
                    >
                      {STATUS_LABELS[q.status] || q.status}
                    </span>
                  </div>

                  <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                    <div>
                      <p className="text-label-sm text-on-surface-variant">Phí vận chuyển</p>
                      <p className="text-body-md font-bold text-primary">{formatCurrency(q.shippingFee)}</p>
                    </div>
                    <div>
                      <p className="text-label-sm text-on-surface-variant">Tiền đặt cọc</p>
                      <p className="text-body-md font-semibold">{formatCurrency(q.depositAmount)}</p>
                    </div>
                    <div>
                      <p className="text-label-sm text-on-surface-variant">Hạn thanh toán</p>
                      <p className="text-body-md">{formatDate(q.expiresAt)}</p>
                    </div>
                    <div>
                      <p className="text-label-sm text-on-surface-variant">Ngày tạo</p>
                      <p className="text-body-md">{formatDate(q.createdAt)}</p>
                    </div>
                  </div>
                </div>
              ))}
            </div>

            {/* Pagination */}
            {data.totalPages > 1 && (
              <div className="flex items-center justify-center gap-3 mt-6">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="btn-ghost disabled:opacity-50"
                >
                  ← Trước
                </button>
                <span className="text-body-md text-on-surface-variant">
                  Trang {page} / {data.totalPages}
                </span>
                <button
                  onClick={() => setPage((p) => Math.min(data.totalPages, p + 1))}
                  disabled={page === data.totalPages}
                  className="btn-ghost disabled:opacity-50"
                >
                  Sau →
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
