import { useState, useEffect, useCallback } from 'react';
import AppHeader from '../../components/AppHeader';
import Toast from '../../components/matching/Toast';
import PaymentTimeline from '../../components/customer/payments/PaymentTimeline';

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

interface PaymentDetailDto {
  id: string;
  paymentCode: string;
  paymentGateway?: string;
  paymentMethod?: string;
  status: string;
  paymentType: string;
  amount: number;
  currency: string;
  createdAt: string;
  paidAt?: string;
  cancelledAt?: string;
  failedAt?: string;
  transactionReference?: string;
  failureReason?: string;
  quotationId?: string;
  quotationCode?: string;
  shippingFee?: number;
  depositAmount?: number;
  shipmentId?: string;
  shipmentCode?: string;
  shipmentStatus?: string;
  customerId?: string;
  customerName?: string;
}

interface PaymentTimelineEntry {
  status: string;
  action: string;
  details?: string;
  occurredAt: string;
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

  // Part 13: Refund state
  const [showRefundDialog, setShowRefundDialog] = useState<string | null>(null);
  const [refundReason, setRefundReason] = useState('');
  const [refundLoading, setRefundLoading] = useState(false);
  const [showApproveDialog, setShowApproveDialog] = useState<string | null>(null);
  const [approveLoading, setApproveLoading] = useState(false);

  // Part 13: Payment Detail state
  const [showPaymentDetail, setShowPaymentDetail] = useState(false);
  const [paymentDetailData, setPaymentDetailData] = useState<PaymentDetailDto | null>(null);
  const [paymentTimelineData, setPaymentTimelineData] = useState<PaymentTimelineEntry[]>([]);
  const [loadingDetail, setLoadingDetail] = useState(false);

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

  // ─── Part 13: Request Refund ───
  const handleRequestRefund = async (paymentId: string) => {
    if (!refundReason.trim()) return;
    setRefundLoading(true);
    try {
      const url = new URL(`${API_BASE}/api/staff/payments/${paymentId}/refund`);
      const res = await authFetch(url.toString(), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reason: refundReason }),
      });
      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `Lỗi yêu cầu hoàn tiền (${res.status})`);
      }
      setToast({ message: 'Đã gửi yêu cầu hoàn tiền thành công.', type: 'success' });
      setShowRefundDialog(null);
      setRefundReason('');
      loadData();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi yêu cầu hoàn tiền', type: 'error' });
    } finally {
      setRefundLoading(false);
    }
  };

  // ─── Part 13: Approve Refund ───
  const handleApproveRefund = async (paymentId: string) => {
    setApproveLoading(true);
    try {
      const url = new URL(`${API_BASE}/api/staff/payments/${paymentId}/refund/approve`);
      const res = await authFetch(url.toString(), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
      });
      if (!res.ok) {
        const text = await res.text();
        throw new Error(text || `Lỗi duyệt hoàn tiền (${res.status})`);
      }
      setToast({ message: 'Đã duyệt hoàn tiền thành công.', type: 'success' });
      setShowApproveDialog(null);
      loadData();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi duyệt hoàn tiền', type: 'error' });
    } finally {
      setApproveLoading(false);
    }
  };

  // ─── Part 13: Payment Detail ───
  const handleShowPaymentDetail = async (paymentId: string) => {
    setShowPaymentDetail(true);
    setPaymentDetailData(null);
    setPaymentTimelineData([]);
    setLoadingDetail(true);
    try {
      const [detailRes, timelineRes] = await Promise.all([
        authFetch(`${API_BASE}/api/staff/payments/${paymentId}`),
        authFetch(`${API_BASE}/api/staff/payments/${paymentId}/timeline`),
      ]);
      if (detailRes.ok) setPaymentDetailData(await detailRes.json());
      if (timelineRes.ok) setPaymentTimelineData(await timelineRes.json());
    } catch {
      setToast({ message: 'Lỗi tải chi tiết thanh toán', type: 'error' });
      setShowPaymentDetail(false);
    } finally {
      setLoadingDetail(false);
    }
  };

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
                  {/* Action Buttons */}
                  <div className="flex items-center gap-2 mt-3 pt-3 border-t border-outline-variant/30">
                    <button
                      onClick={() => handleShowPaymentDetail(p.id)}
                      className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-surface-container-low text-on-surface-variant border border-outline-variant hover:bg-surface-container transition-colors text-label-sm font-semibold"
                    >
                      <span className="material-symbols-outlined text-[14px]">info</span>
                      Chi tiết & Timeline
                    </button>
                    {p.status === 'Paid' && (
                      <button
                        onClick={() => { setShowRefundDialog(p.id); setRefundReason(''); }}
                        className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-amber-50 text-amber-700 border border-amber-200 hover:bg-amber-100 transition-colors text-label-sm font-semibold"
                      >
                        <span className="material-symbols-outlined text-[14px]">replay</span>
                        Yêu cầu hoàn tiền
                      </button>
                    )}
                    {p.status === 'PendingRefund' && (
                      <button
                        onClick={() => setShowApproveDialog(p.id)}
                        className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-50 text-emerald-700 border border-emerald-200 hover:bg-emerald-100 transition-colors text-label-sm font-semibold"
                      >
                        <span className="material-symbols-outlined text-[14px]">check_circle</span>
                        Duyệt hoàn tiền
                      </button>
                    )}
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

      {/* Refund Request Dialog */}
      {showRefundDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-6 w-full max-w-md card-shadow">
            <h3 className="text-title-lg font-bold text-on-surface mb-2">Yêu cầu hoàn tiền</h3>
            <p className="text-body-md text-on-surface-variant mb-4">
              Nhập lý do yêu cầu hoàn tiền cho giao dịch này.
            </p>
            <textarea
              value={refundReason}
              onChange={(e) => setRefundReason(e.target.value)}
              placeholder="Nhập lý do hoàn tiền..."
              className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md placeholder:text-on-surface-variant/50 focus:outline-none focus:border-primary resize-none"
              rows={3}
            />
            <div className="flex gap-3 mt-4">
              <button
                onClick={() => { setShowRefundDialog(null); setRefundReason(''); }}
                className="flex-1 px-4 py-3 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low transition-colors text-label-md font-bold"
              >
                Đóng
              </button>
              <button
                onClick={() => handleRequestRefund(showRefundDialog)}
                disabled={!refundReason.trim() || refundLoading}
                className="flex-1 px-4 py-3 rounded-xl bg-amber-600 text-white hover:bg-amber-700 transition-colors text-label-md font-bold disabled:opacity-50"
              >
                {refundLoading ? 'Đang gửi...' : 'Gửi yêu cầu'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Approve Refund Dialog */}
      {showApproveDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-6 w-full max-w-md card-shadow">
            <div className="flex items-center gap-3 mb-4">
              <div className="w-10 h-10 rounded-full bg-emerald-100 flex items-center justify-center">
                <span className="material-symbols-outlined text-emerald-600 text-xl">check_circle</span>
              </div>
              <div>
                <h3 className="text-headline-sm font-bold text-on-surface">Duyệt hoàn tiền</h3>
                <p className="text-label-sm text-on-surface-variant">Xác nhận duyệt yêu cầu hoàn tiền</p>
              </div>
            </div>
            <p className="text-body-md text-on-surface-variant mb-5">
              Khoản tiền sẽ được hoàn lại cho khách hàng. Hành động này không thể hoàn tác.
            </p>
            <div className="flex gap-3 justify-end">
              <button
                onClick={() => setShowApproveDialog(null)}
                disabled={approveLoading}
                className="px-4 py-2.5 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low transition-colors text-label-md font-bold"
              >
                Hủy
              </button>
              <button
                onClick={() => handleApproveRefund(showApproveDialog)}
                disabled={approveLoading}
                className="px-5 py-2.5 rounded-xl bg-emerald-600 text-white font-bold text-sm hover:bg-emerald-700 disabled:opacity-50 transition-colors flex items-center gap-2"
              >
                {approveLoading ? (
                  <>
                    <span className="material-symbols-outlined animate-spin text-[16px]">sync</span>
                    Đang xử lý...
                  </>
                ) : (
                  <>
                    <span className="material-symbols-outlined text-[16px]">check</span>
                    Duyệt hoàn tiền
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Payment Detail Modal */}
      {showPaymentDetail && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={() => setShowPaymentDetail(false)}>
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant w-full max-w-lg max-h-[85vh] overflow-y-auto card-shadow" onClick={(e) => e.stopPropagation()}>
            {/* Header */}
            <div className="sticky top-0 bg-surface-container-lowest border-b border-outline-variant px-6 py-4 flex items-center justify-between rounded-t-2xl z-10">
              <h3 className="text-headline-sm font-bold text-on-surface flex items-center gap-2">
                <span className="material-symbols-outlined text-primary">receipt_long</span>
                Chi tiết thanh toán
              </h3>
              <button
                onClick={() => setShowPaymentDetail(false)}
                className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-surface-container-low transition-colors"
              >
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            {loadingDetail && (
              <div className="p-6 space-y-4">
                {[1, 2, 3].map((i) => (
                  <div key={i} className="h-12 bg-gray-100 rounded-xl animate-pulse" />
                ))}
              </div>
            )}

            {paymentDetailData && (
              <div className="p-6 space-y-5">
                {/* Payment Info */}
                <div className="space-y-3">
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Mã thanh toán</span>
                    <span className="text-body-md font-semibold text-on-surface">{paymentDetailData.paymentCode}</span>
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Trạng thái</span>
                    <span className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-label-sm font-semibold border ${
                      STATUS_BADGE[paymentDetailData.status] || 'bg-gray-100 text-gray-600'
                    }`}>
                      {STATUS_LABELS[paymentDetailData.status] || paymentDetailData.status}
                    </span>
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Loại thanh toán</span>
                    <span className="text-body-md font-medium text-on-surface">
                      {TYPE_LABELS[paymentDetailData.paymentType] || paymentDetailData.paymentType}
                    </span>
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Số tiền</span>
                    <span className="text-body-lg font-bold text-primary">{formatCurrency(paymentDetailData.amount)}</span>
                  </div>
                  {paymentDetailData.paymentMethod && (
                    <div className="flex justify-between items-center">
                      <span className="text-body-md text-on-surface-variant">Phương thức</span>
                      <span className="text-body-md text-on-surface">{paymentDetailData.paymentMethod}</span>
                    </div>
                  )}
                  {paymentDetailData.transactionReference && (
                    <div className="flex justify-between items-center">
                      <span className="text-body-md text-on-surface-variant">Mã giao dịch</span>
                      <span className="text-body-md text-on-surface font-mono">{paymentDetailData.transactionReference}</span>
                    </div>
                  )}
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Ngày tạo</span>
                    <span className="text-body-md text-on-surface">{formatDate(paymentDetailData.createdAt)}</span>
                  </div>
                  {paymentDetailData.paidAt && (
                    <div className="flex justify-between items-center">
                      <span className="text-body-md text-on-surface-variant">Ngày thanh toán</span>
                      <span className="text-body-md text-emerald-600 font-medium">{formatDate(paymentDetailData.paidAt)}</span>
                    </div>
                  )}
                  {paymentDetailData.failureReason && (
                    <div className="p-3 rounded-xl bg-rose-50 border border-rose-200">
                      <p className="text-label-sm font-semibold text-rose-700">Lý do thất bại:</p>
                      <p className="text-body-sm text-rose-600 mt-1">{paymentDetailData.failureReason}</p>
                    </div>
                  )}
                </div>

                {/* Related Info */}
                {(paymentDetailData.shipmentCode || paymentDetailData.quotationCode || paymentDetailData.customerName) && (
                  <div className="border-t border-outline-variant/30 pt-4 space-y-3">
                    <h4 className="text-label-lg font-bold text-on-surface flex items-center gap-2">
                      <span className="material-symbols-outlined text-[16px] text-primary">link</span>
                      Thông tin liên kết
                    </h4>
                    {paymentDetailData.shipmentCode && (
                      <div className="flex justify-between items-center">
                        <span className="text-body-md text-on-surface-variant">Đơn hàng</span>
                        <span className="text-body-md font-medium text-on-surface">{paymentDetailData.shipmentCode}</span>
                      </div>
                    )}
                    {paymentDetailData.quotationCode && (
                      <div className="flex justify-between items-center">
                        <span className="text-body-md text-on-surface-variant">Báo giá</span>
                        <span className="text-body-md font-medium text-on-surface">{paymentDetailData.quotationCode}</span>
                      </div>
                    )}
                    {paymentDetailData.customerName && (
                      <div className="flex justify-between items-center">
                        <span className="text-body-md text-on-surface-variant">Khách hàng</span>
                        <span className="text-body-md font-medium text-on-surface">{paymentDetailData.customerName}</span>
                      </div>
                    )}
                  </div>
                )}

                {/* Timeline */}
                <div className="border-t border-outline-variant/30 pt-4">
                  <h4 className="text-label-lg font-bold text-on-surface mb-3 flex items-center gap-2">
                    <span className="material-symbols-outlined text-[16px] text-primary">timeline</span>
                    Lịch sử trạng thái
                  </h4>
                  <PaymentTimeline entries={paymentTimelineData} loading={loadingDetail} />
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
