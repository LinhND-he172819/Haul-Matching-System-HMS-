import { useState, useEffect } from 'react';
import type { PaymentDetailDto, PaymentTimelineEntry } from '../../../api/customer/customerQuotationApi';
import { getPaymentDetail, getPaymentTimeline } from '../../../api/customer/customerQuotationApi';
import PaymentStatusBadge from './PaymentStatusBadge';
import PaymentTimeline from './PaymentTimeline';

/* ─── Props ─────────────────────────────────────────────────────── */

interface PaymentDetailDialogProps {
  paymentId: string;
  onClose: () => void;
  onToast: (message: string, type: 'success' | 'error') => void;
  /** Optional: if provided, use this data directly instead of fetching */
  initialData?: PaymentDetailDto;
  /** Whether to show staff-specific info (hub, etc.) */
  isStaff?: boolean;
}

const TYPE_LABELS: Record<string, string> = {
  Deposit: 'Đặt cọc',
  FinalPayment: 'Thanh toán cuối',
  AdditionalCharge: 'Phụ phí',
  Refund: 'Hoàn tiền',
};

/* ─── Component ─────────────────────────────────────────────────── */

export default function PaymentDetailDialog({
  paymentId,
  onClose,
  onToast,
  initialData,
  isStaff = false,
}: PaymentDetailDialogProps) {
  const [detail, setDetail] = useState<PaymentDetailDto | null>(initialData || null);
  const [timeline, setTimeline] = useState<PaymentTimelineEntry[]>([]);
  const [loading, setLoading] = useState(!initialData);
  const [loadingTimeline, setLoadingTimeline] = useState(false);

  useEffect(() => {
    if (initialData) {
      setDetail(initialData);
      // Still load timeline if not provided
      if (!initialData.timeline?.length) {
        loadTimeline();
      } else {
        setTimeline(initialData.timeline);
      }
      return;
    }
    loadData();
  }, [paymentId]);

  const loadData = async () => {
    setLoading(true);
    try {
      const [detailResult, timelineResult] = await Promise.all([
        getPaymentDetail(paymentId),
        getPaymentTimeline(paymentId),
      ]);
      setDetail(detailResult);
      setTimeline(timelineResult);
    } catch (err: any) {
      onToast(err.message || 'Lỗi tải chi tiết thanh toán', 'error');
      onClose();
    } finally {
      setLoading(false);
    }
  };

  const loadTimeline = async () => {
    setLoadingTimeline(true);
    try {
      const result = await getPaymentTimeline(paymentId);
      setTimeline(result);
    } catch { /* timeline not critical */ }
    setLoadingTimeline(false);
  };

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

  const formatCurrency = (n?: number) =>
    n?.toLocaleString('vi-VN', { style: 'currency', currency: 'VND' }) ?? '-';

  const formatExpiry = (s?: string) => {
    if (!s) return null;
    const exp = new Date(s);
    const now = new Date();
    const diffMs = exp.getTime() - now.getTime();
    if (diffMs <= 0) return 'Đã hết hạn';
    const mins = Math.floor(diffMs / 60000);
    return `${mins} phút nữa hết hạn`;
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="bg-surface-container-lowest rounded-2xl border border-outline-variant w-full max-w-lg max-h-[85vh] overflow-y-auto card-shadow"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="sticky top-0 bg-surface-container-lowest border-b border-outline-variant px-6 py-4 flex items-center justify-between rounded-t-2xl z-10">
          <h3 className="text-headline-sm font-bold text-on-surface flex items-center gap-2">
            <span className="material-symbols-outlined text-primary">receipt_long</span>
            Chi tiết thanh toán
          </h3>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-surface-container-low transition-colors"
          >
            <span className="material-symbols-outlined text-[20px]">close</span>
          </button>
        </div>

        {loading && (
          <div className="p-6 space-y-4">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="h-10 bg-gray-100 rounded-xl animate-pulse" />
            ))}
          </div>
        )}

        {detail && (
          <div className="p-6 space-y-5">
            {/* Status + Type Header */}
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-12 h-12 rounded-full bg-primary/10 flex items-center justify-center">
                  <span className="material-symbols-outlined text-[20px] text-primary">
                    {detail.paymentType === 'Deposit' ? 'account_balance_wallet' : 'payments'}
                  </span>
                </div>
                <div>
                  <p className="text-body-md font-bold text-on-surface">{detail.paymentCode}</p>
                  <p className="text-label-sm text-on-surface-variant">
                    {TYPE_LABELS[detail.paymentType] || detail.paymentType}
                  </p>
                </div>
              </div>
              <PaymentStatusBadge status={detail.status} />
            </div>

            {/* Amount */}
            <div className="bg-primary/5 border border-primary/20 rounded-xl p-4 text-center">
              <p className="text-label-sm text-on-surface-variant">Số tiền thanh toán</p>
              <p className="text-headline-md font-bold text-primary mt-1">
                {formatCurrency(detail.amount)}
              </p>
              <p className="text-label-sm text-on-surface-variant mt-0.5">{detail.currency}</p>
            </div>

            {/* Payment Info */}
            <div className="space-y-3">
              <h4 className="text-label-lg font-bold text-on-surface flex items-center gap-2">
                <span className="material-symbols-outlined text-[16px] text-primary">info</span>
                Thông tin thanh toán
              </h4>
              <div className="space-y-2">
                <div className="flex justify-between items-center">
                  <span className="text-body-md text-on-surface-variant">Phương thức</span>
                  <span className="text-body-md text-on-surface">{detail.paymentMethod || 'Chưa chọn'}</span>
                </div>
                {detail.transactionReference && (
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Mã giao dịch</span>
                    <span className="text-body-md text-on-surface font-mono text-sm">{detail.transactionReference}</span>
                  </div>
                )}
                <div className="flex justify-between items-center">
                  <span className="text-body-md text-on-surface-variant">Ngày tạo</span>
                  <span className="text-body-md text-on-surface">{formatDate(detail.createdAt)}</span>
                </div>
                {detail.paidAt && (
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Ngày thanh toán</span>
                    <span className="text-body-md text-emerald-600 font-medium">{formatDate(detail.paidAt)}</span>
                  </div>
                )}
                {detail.cancelledAt && (
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Ngày hủy</span>
                    <span className="text-body-md text-gray-500">{formatDate(detail.cancelledAt)}</span>
                  </div>
                )}
                {detail.failedAt && (
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Ngày thất bại</span>
                    <span className="text-body-md text-rose-600">{formatDate(detail.failedAt)}</span>
                  </div>
                )}
                {detail.expiresAt && (
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Thời hạn</span>
                    <span className={`text-body-md font-semibold ${
                      new Date(detail.expiresAt).getTime() < Date.now() ? 'text-rose-600' : 'text-amber-600'
                    }`}>
                      {formatExpiry(detail.expiresAt)}
                    </span>
                  </div>
                )}
              </div>
            </div>

            {/* Failure Reason */}
            {detail.failureReason && (
              <div className="p-3 rounded-xl bg-rose-50 border border-rose-200">
                <p className="text-label-sm font-semibold text-rose-700 flex items-center gap-1">
                  <span className="material-symbols-outlined text-[14px]">error</span>
                  Lý do thất bại
                </p>
                <p className="text-body-sm text-rose-600 mt-1">{detail.failureReason}</p>
              </div>
            )}

            {/* Linked Info */}
            {(detail.shipmentCode || detail.quotationCode || detail.customerName) && (
              <div className="border-t border-outline-variant/30 pt-4 space-y-3">
                <h4 className="text-label-lg font-bold text-on-surface flex items-center gap-2">
                  <span className="material-symbols-outlined text-[16px] text-primary">link</span>
                  Thông tin liên kết
                </h4>
                {detail.shipmentCode && (
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Đơn hàng</span>
                    <span className="text-body-md font-medium text-on-surface">{detail.shipmentCode}</span>
                  </div>
                )}
                {detail.quotationCode && (
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Báo giá</span>
                    <span className="text-body-md font-medium text-on-surface">{detail.quotationCode}</span>
                  </div>
                )}
                {detail.customerName && isStaff && (
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Khách hàng</span>
                    <span className="text-body-md font-medium text-on-surface">{detail.customerName}</span>
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
              <PaymentTimeline entries={detail.timeline || timeline} loading={loadingTimeline} />
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
