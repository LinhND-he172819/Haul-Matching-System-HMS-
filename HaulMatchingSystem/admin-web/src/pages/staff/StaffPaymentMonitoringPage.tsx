import { useState, useEffect, useCallback } from 'react';
import AppHeader from '../../components/AppHeader';
import Toast from '../../components/matching/Toast';

const API_BASE =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

/* ─── Types ─────────────────────────────────────────────────────── */

import { authFetch } from '../../utils/authFetch';

interface PaymentListItem {
  id: string;
  paymentCode: string;
  quotationId: string;
  quotationCode: string;
  shipmentId: string;
  shipmentCode: string;
  customerName: string;
  paymentType: string;
  amount: number;
  currency: string;
  status: string;
  transactionReference?: string;
  paidAt?: string;
  createdAt: string;
}

interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/* ─── Constants ─────────────────────────────────────────────────── */

const STATUS_TABS = [
  { key: '', label: 'Tất cả' },
  { key: 'Pending', label: 'Đang chờ' },
  { key: 'Paid', label: 'Đã thanh toán' },
  { key: 'Failed', label: 'Thất bại' },
  { key: 'Refunded', label: 'Đã hoàn tiền' },
];

const STATUS_BADGE: Record<string, string> = {
  Pending: 'bg-amber-50 text-amber-700 border border-amber-200',
  Paid: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
  Failed: 'bg-rose-50 text-rose-700 border border-rose-200',
  Cancelled: 'bg-gray-100 text-gray-600 border border-gray-200',
  PendingRefund: 'bg-amber-50 text-amber-700 border border-amber-200',
  Refunded: 'bg-blue-50 text-blue-700 border border-blue-200',
  PartiallyRefunded: 'bg-blue-50 text-blue-700 border border-blue-200',
};

const STATUS_LABELS: Record<string, string> = {
  Pending: 'Đang chờ',
  Paid: 'Đã thanh toán',
  Failed: 'Thất bại',
  Cancelled: 'Đã hủy',
  PendingRefund: 'Đang chờ hoàn tiền',
  Refunded: 'Đã hoàn tiền',
  PartiallyRefunded: 'Hoàn tiền một phần',
};

const TYPE_LABELS: Record<string, string> = {
  Deposit: 'Đặt cọc',
  FinalPayment: 'Thanh toán cuối',
  AdditionalCharge: 'Phụ phí',
  Refund: 'Hoàn tiền',
};

/* ─── Props ─────────────────────────────────────────────────────── */

type Props = {
  onLogout: () => void;
};

/* ─── Component ─────────────────────────────────────────────────── */

export default function StaffPaymentMonitoringPage({ onLogout }: Props) {
  const [data, setData] = useState<PagedResult<PaymentListItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState('');
  const [page, setPage] = useState(1);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const url = new URL(`${API_BASE}/api/staff/payments`);
      if (activeTab) url.searchParams.set('status', activeTab);
      url.searchParams.set('page', page.toString());
      url.searchParams.set('pageSize', '10');

      const res = await authFetch(url.toString());
      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `Lỗi tải danh sách thanh toán (${res.status})`);
      }
      const result = await res.json();
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
    { label: 'Báo giá', onClick: () => {} },
    { label: 'Thanh toán', onClick: () => {}, active: true },
  ];

  return (
    <div className="min-h-screen bg-surface">
      <AppHeader onLogout={onLogout} pages={navPages} />

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
        {/* Page Header */}
        <div className="mb-6">
          <h1 className="text-headline-lg font-bold text-on-surface flex items-center gap-3">
            <span className="material-symbols-outlined text-primary">payments</span>
            Theo dõi thanh toán
          </h1>
          <p className="text-body-md text-on-surface-variant mt-1">
            Theo dõi trạng thái thanh toán của khách hàng
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
              <div key={i} className="h-24 bg-white rounded-2xl animate-pulse border border-outline-variant" />
            ))}
          </div>
        )}

        {/* Empty */}
        {!loading && data && data.items.length === 0 && (
          <div className="text-center py-16 bg-white rounded-2xl border border-outline-variant">
            <span className="material-symbols-outlined text-[48px] text-gray-300">receipt_long</span>
            <p className="text-title-lg font-semibold text-on-surface-variant mt-3">
              Không có giao dịch nào
            </p>
          </div>
        )}

        {/* Payment List */}
        {!loading && data && data.items.length > 0 && (
          <>
            <div className="space-y-3">
              {data.items.map((p) => (
                <div
                  key={p.id}
                  className="bg-white rounded-2xl border border-outline-variant p-5 hover:shadow-md transition-shadow"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-full bg-primary/10 flex items-center justify-center">
                        <span className="material-symbols-outlined text-primary text-xl">
                          {p.paymentType === 'Deposit' ? 'account_balance_wallet' : 'payments'}
                        </span>
                      </div>
                      <div>
                        <p className="text-body-md font-bold text-on-surface">
                          {p.paymentCode}
                          <span className="text-label-sm text-on-surface-variant ml-2">
                            {TYPE_LABELS[p.paymentType] || p.paymentType}
                          </span>
                        </p>
                        <p className="text-label-sm text-on-surface-variant">
                          {p.shipmentCode} • {p.customerName}
                        </p>
                      </div>
                    </div>
                    <div className="text-right">
                      <p className="text-body-lg font-bold text-on-surface">{formatCurrency(p.amount)}</p>
                      <span
                        className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-label-sm font-semibold border ${
                          STATUS_BADGE[p.status] || 'bg-gray-100 text-gray-600'
                        }`}
                      >
                        {STATUS_LABELS[p.status] || p.status}
                      </span>
                    </div>
                  </div>
                  <div className="flex items-center gap-4 mt-3 text-label-sm text-on-surface-variant">
                    <span>Tạo: {formatDate(p.createdAt)}</span>
                    {p.paidAt && <span>Thanh toán: {formatDate(p.paidAt)}</span>}
                    {p.transactionReference && <span>Ref: {p.transactionReference}</span>}
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
