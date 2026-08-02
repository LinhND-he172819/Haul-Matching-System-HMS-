import { useEffect, useState, useCallback } from 'react';
import {
  getShipmentDetail,
  cancelShipment,
  type CustomerShipmentDetail,
} from '../api/customerShipmentApi';
import {
  getCustomerQuotation,
  createDepositPayment,
  createFinalPayment,
  getPaymentHistory,
  retryPayment,
  cancelPayment,
  getPaymentDetail,
  getPaymentTimeline,
  type CustomerQuotationDetail,
  type PaymentResponseDto,
  type PaymentHistoryEntry,
  type PaymentDetailDto,
  type PaymentTimelineEntry,
} from '../api/customer/customerQuotationApi';
import QuotationCountdown from '../components/customer/quotations/QuotationCountdown';
import PaymentStatusBadge from '../components/customer/payments/PaymentStatusBadge';
import PaymentHistory from '../components/customer/payments/PaymentHistory';
import PaymentTimeline from '../components/customer/payments/PaymentTimeline';
import Toast from '../components/matching/Toast';

/* ─── Status Badge ────────────────────────────────────────────────── */

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

/* ─── Props ───────────────────────────────────────────────────────── */

type Props = {
  shipmentId: string;
  onBack: () => void;
  onLogout: () => void;
};

/* ─── Component ───────────────────────────────────────────────────── */

export default function ShipmentDetailPage({ shipmentId, onBack, onLogout }: Props) {
  const [detail, setDetail] = useState<CustomerShipmentDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [showCancelDialog, setShowCancelDialog] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelling, setCancelling] = useState(false);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  // Payment state
  const [quotationDetail, setQuotationDetail] = useState<CustomerQuotationDetail | null>(null);
  const [paymentHistory, setPaymentHistory] = useState<PaymentHistoryEntry[]>([]);
  const [payingDeposit, setPayingDeposit] = useState(false);
  const [payingFinal, setPayingFinal] = useState(false);
  const [showPayDialog, setShowPayDialog] = useState<'deposit' | 'final' | null>(null);
  const [showHistory, setShowHistory] = useState(false);

  // Part 13: New payment states
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [showPaymentDetail, setShowPaymentDetail] = useState(false);
  const [paymentDetailData, setPaymentDetailData] = useState<PaymentDetailDto | null>(null);
  const [paymentTimelineData, setPaymentTimelineData] = useState<PaymentTimelineEntry[]>([]);
  const [loadingTimeline, setLoadingTimeline] = useState(false);
  const [showCancelPaymentDialog, setShowCancelPaymentDialog] = useState<string | null>(null);
  const [cancelPaymentReason, setCancelPaymentReason] = useState('');

  const loadDetail = async () => {
    setLoading(true);
    try {
      const result = await getShipmentDetail(shipmentId);
      setDetail(result);

      // Load quotation detail if available
      if (result.quotation?.id) {
        try {
          const qDetail = await getCustomerQuotation(result.quotation.id);
          setQuotationDetail(qDetail);
        } catch { /* quotation detail not critical */ }
      }

      // Load payment history
      try {
        if (detail.quotation?.id) {
          const history = await getPaymentHistory(detail.quotation.id);
          setPaymentHistory(history);
        }
      } catch { /* payment history not critical */ }
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi tải dữ liệu', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadDetail();
  }, [shipmentId]);

  const handlePayDeposit = async () => {
    setPayingDeposit(true);
    try {
      if (!detail.quotation?.id) throw new Error('Không tìm thấy báo giá.');
      const result = await createDepositPayment(detail.quotation.id);
      setToast({ message: 'Đặt cọc thành công! Đơn hàng đã được xác nhận.', type: 'success' });
      setShowPayDialog(null);
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi đặt cọc', type: 'error' });
    } finally {
      setPayingDeposit(false);
    }
  };

  const handlePayFinal = async () => {
    setPayingFinal(true);
    try {
      if (!detail.quotation?.id) throw new Error('Không tìm thấy báo giá.');
      const result = await createFinalPayment(detail.quotation.id);
      setToast({ message: 'Thanh toán cuối thành công! Đơn hàng đã hoàn tất.', type: 'success' });
      setShowPayDialog(null);
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi thanh toán cuối', type: 'error' });
    } finally {
      setPayingFinal(false);
    }
  };

  const handleCancel = async () => {
    if (!cancelReason.trim()) return;
    setCancelling(true);
    try {
      await cancelShipment(shipmentId, cancelReason);
      setToast({ message: 'Đã hủy đơn hàng thành công.', type: 'success' });
      setShowCancelDialog(false);
      setCancelReason('');
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi hủy đơn hàng', type: 'error' });
    } finally {
      setCancelling(false);
    }
  };

  // ─── Part 13: Retry Payment ───
  const handleRetryPayment = async (paymentId: string) => {
    setActionLoading(paymentId);
    try {
      await retryPayment(paymentId);
      setToast({ message: 'Đã gửi lại thanh toán thành công.', type: 'success' });
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi thử lại thanh toán', type: 'error' });
    } finally {
      setActionLoading(null);
    }
  };

  // ─── Part 13: Cancel Payment ───
  const handleCancelPayment = async (paymentId: string) => {
    setActionLoading(paymentId);
    try {
      await cancelPayment(paymentId, cancelPaymentReason);
      setToast({ message: 'Đã hủy thanh toán thành công.', type: 'success' });
      setShowCancelPaymentDialog(null);
      setCancelPaymentReason('');
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi hủy thanh toán', type: 'error' });
    } finally {
      setActionLoading(null);
    }
  };

  // ─── Part 13: Payment Detail ───
  const handleShowPaymentDetail = async (paymentId: string) => {
    setShowPaymentDetail(true);
    setPaymentDetailData(null);
    setPaymentTimelineData([]);
    setLoadingTimeline(true);
    try {
      const [detail, timeline] = await Promise.all([
        getPaymentDetail(paymentId),
        getPaymentTimeline(paymentId),
      ]);
      setPaymentDetailData(detail);
      setPaymentTimelineData(timeline);
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi tải chi tiết thanh toán', type: 'error' });
      setShowPaymentDetail(false);
    } finally {
      setLoadingTimeline(false);
    }
  };

  const formatDate = (s?: string) =>
    s ? new Date(s).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '-';

  const formatCurrency = (n: number) => n.toLocaleString('vi-VN', { style: 'currency', currency: 'VND' });

  if (loading) {
    return (
      <div className="min-h-screen bg-surface flex items-center justify-center">
        <div className="flex flex-col items-center gap-3">
          <span className="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
          <p className="text-body-md text-on-surface-variant">Đang tải chi tiết...</p>
        </div>
      </div>
    );
  }

  if (!detail) {
    return (
      <div className="min-h-screen bg-surface flex items-center justify-center">
        <div className="text-center">
          <span className="material-symbols-outlined text-[48px] text-on-surface-variant/40">error</span>
          <p className="text-title-md text-on-surface mt-3">Không tìm thấy đơn hàng</p>
          <button onClick={onBack} className="mt-4 px-4 py-2 rounded-xl bg-primary text-on-primary text-label-md font-bold">Quay lại</button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-surface text-on-surface">
      {/* Header */}
      <div className="bg-surface-container-lowest border-b border-outline-variant sticky top-0 z-10">
        <div className="max-w-3xl mx-auto px-4 py-4 flex items-center gap-3">
          <button onClick={onBack} className="w-9 h-9 rounded-xl border border-outline-variant flex items-center justify-center hover:bg-surface-container-low transition-colors">
            <span className="material-symbols-outlined text-[20px]">arrow_back</span>
          </button>
          <div className="flex-1">
            <h1 className="text-headline-sm font-bold text-on-surface">{detail.shipmentCode}</h1>
          </div>
          <span className={`text-label-sm font-medium px-2.5 py-1 rounded-lg ${STATUS_BADGE[detail.status] || 'bg-gray-100 text-gray-600'}`}>
            {STATUS_LABELS[detail.status] || detail.status}
          </span>
          <button onClick={onLogout} className="flex items-center gap-2 px-3 py-2 rounded-xl border border-outline-variant text-on-surface-variant hover:bg-surface-container-low transition-colors text-label-md">
            <span className="material-symbols-outlined text-[18px]">logout</span>
          </button>
        </div>
      </div>

      <div className="max-w-3xl mx-auto px-4 py-6 space-y-4">
        {/* Route Card */}
        {(detail.originName || detail.destinationName || detail.deliveryAddress) && (
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
            <h3 className="text-label-lg font-bold text-on-surface mb-3 flex items-center gap-2">
              <span className="material-symbols-outlined text-[18px] text-primary">route</span>
              Thông tin tuyến đường
            </h3>
            <div className="space-y-2">
              {detail.originName && (
                <div className="flex items-start gap-3">
                  <div className="w-6 h-6 rounded-full bg-green-100 flex items-center justify-center mt-0.5">
                    <div className="w-2 h-2 rounded-full bg-green-500"></div>
                  </div>
                  <div>
                    <p className="text-body-sm text-on-surface-variant">Điểm đi</p>
                    <p className="text-body-md text-on-surface font-medium">{detail.originName}</p>
                  </div>
                </div>
              )}
              {detail.deliveryAddress && (
                <div className="flex items-start gap-3">
                  <div className="w-6 h-6 rounded-full bg-red-100 flex items-center justify-center mt-0.5">
                    <div className="w-2 h-2 rounded-full bg-red-500"></div>
                  </div>
                  <div>
                    <p className="text-body-sm text-on-surface-variant">Điểm đến</p>
                    <p className="text-body-md text-on-surface font-medium">{detail.destinationName || detail.deliveryAddress}</p>
                  </div>
                </div>
              )}
            </div>
            {detail.tripCode && (
              <div className="mt-3 pt-3 border-t border-outline-variant/30 flex items-center gap-2 text-body-sm text-on-surface-variant">
                <span className="material-symbols-outlined text-[14px]">confirmation_number</span>
                Chuyến: <span className="font-medium text-on-surface">{detail.tripCode}</span>
                {detail.vehiclePlate && <span className="ml-2">• {detail.vehiclePlate}</span>}
              </div>
            )}
          </div>
        )}

        {/* Cargo Info */}
        <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
          <h3 className="text-label-lg font-bold text-on-surface mb-3 flex items-center gap-2">
            <span className="material-symbols-outlined text-[18px] text-primary">inventory_2</span>
            Thông tin hàng hóa
          </h3>
          <div className="grid grid-cols-2 gap-4">
            {detail.commodity && (
              <div>
                <p className="text-body-sm text-on-surface-variant">Loại hàng</p>
                <p className="text-body-md text-on-surface font-medium">{detail.commodity}</p>
              </div>
            )}
            <div>
              <p className="text-body-sm text-on-surface-variant">Khối lượng</p>
              <p className="text-body-md text-on-surface font-medium">{detail.weight} kg</p>
            </div>
            <div>
              <p className="text-body-sm text-on-surface-variant">Thể tích</p>
              <p className="text-body-md text-on-surface font-medium">{detail.volume} m³</p>
            </div>
            {detail.specialInstructions && (
              <div className="col-span-2">
                <p className="text-body-sm text-on-surface-variant">Ghi chú đặc biệt</p>
                <p className="text-body-md text-on-surface">{detail.specialInstructions}</p>
              </div>
            )}
          </div>
        </div>

        {/* Sender / Receiver */}
        <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
            <div>
              <h4 className="text-label-lg font-bold text-on-surface mb-2 flex items-center gap-2">
                <span className="material-symbols-outlined text-[16px] text-primary">upload</span>
                Người gửi
              </h4>
              {detail.senderName && <p className="text-body-md text-on-surface">{detail.senderName}</p>}
              {detail.senderPhone && <p className="text-body-sm text-on-surface-variant">{detail.senderPhone}</p>}
              {detail.pickupAddress && <p className="text-body-sm text-on-surface-variant mt-1">{detail.pickupAddress}</p>}
              {detail.pickupNote && <p className="text-body-sm text-on-surface-variant italic mt-1">Ghi chú: {detail.pickupNote}</p>}
            </div>
            <div>
              <h4 className="text-label-lg font-bold text-on-surface mb-2 flex items-center gap-2">
                <span className="material-symbols-outlined text-[16px] text-primary">download</span>
                Người nhận
              </h4>
              {detail.receiverName && <p className="text-body-md text-on-surface">{detail.receiverName}</p>}
              {detail.receiverPhone && <p className="text-body-sm text-on-surface-variant">{detail.receiverPhone}</p>}
              {detail.deliveryAddress && <p className="text-body-sm text-on-surface-variant mt-1">{detail.deliveryAddress}</p>}
            </div>
          </div>
        </div>

        {/* Quotation & Payment Section */}
        {detail.quotation && (
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
            <h3 className="text-label-lg font-bold text-on-surface mb-3 flex items-center gap-2">
              <span className="material-symbols-outlined text-[18px] text-primary">receipt_long</span>
              Báo giá & Thanh toán
            </h3>

            {/* Quotation Info */}
            <div className="space-y-2">
              <div className="flex justify-between">
                <span className="text-body-md text-on-surface-variant">Phí vận chuyển</span>
                <span className="text-body-md text-on-surface font-medium">{formatCurrency(detail.quotation.shippingFee)}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-body-md text-on-surface-variant">Đặt cọc ({quotationDetail?.depositPercentage ? `${quotationDetail.depositPercentage}%` : ''})</span>
                <span className="text-body-md text-on-surface font-medium">{formatCurrency(detail.quotation.depositAmount)}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-body-md text-on-surface-variant">Còn lại</span>
                <span className="text-body-md text-on-surface font-medium">{formatCurrency(detail.quotation.remainingAmount)}</span>
              </div>
              {detail.payment && (
                <div className="pt-2 mt-2 border-t border-outline-variant/30 space-y-2">
                  <div className="flex justify-between">
                    <span className="text-body-md text-on-surface-variant">Đã đặt cọc</span>
                    <span className={`text-body-md font-bold ${detail.payment.depositPaid > 0 ? 'text-emerald-600' : 'text-on-surface-variant'}`}>
                      {formatCurrency(detail.payment.depositPaid)}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-body-md text-on-surface-variant">Đã thanh toán cuối</span>
                    <span className={`text-body-md font-bold ${detail.payment.finalPaid > 0 ? 'text-emerald-600' : 'text-on-surface-variant'}`}>
                      {formatCurrency(detail.payment.finalPaid)}
                    </span>
                  </div>
                  {detail.payment.outstandingAmount > 0 && (
                    <div className="flex justify-between">
                      <span className="text-body-md text-on-surface-variant">Còn nợ</span>
                      <span className="text-body-md font-bold text-error">{formatCurrency(detail.payment.outstandingAmount)}</span>
                    </div>
                  )}
                </div>
              )}
            </div>

            {/* Quotation Countdown */}
            {detail.status === 'PendingDeposit' && detail.quotation.expiresAt && (
              <div className="mt-4 pt-3 border-t border-outline-variant/30">
                <p className="text-label-sm text-on-surface-variant mb-2">Thời hạn thanh toán đặt cọc:</p>
                <QuotationCountdown
                  expiresAt={detail.quotation.expiresAt}
                  onExpired={() => setToast({ message: 'Báo giá đã hết hiệu lực. Vui lòng liên hệ_staff để được hỗ trợ.', type: 'error' })}
                />
              </div>
            )}

            {/* Deposit Payment Button */}
            {detail.allowedActions?.canPayDeposit && detail.status === 'PendingDeposit' && (
              <div className="mt-4 pt-3 border-t border-outline-variant/30">
                <button
                  onClick={() => setShowPayDialog('deposit')}
                  className="w-full flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-primary text-on-primary text-label-md font-bold
                             hover:bg-primary-700 transition-colors"
                >
                  <span className="material-symbols-outlined text-[18px]">account_balance_wallet</span>
                  Thanh toán đặt cọc — {formatCurrency(detail.quotation.depositAmount)}
                </button>
              </div>
            )}

            {/* Final Payment Button */}
            {detail.allowedActions?.canPayRemaining && detail.status === 'Delivered' && (
              <div className="mt-4 pt-3 border-t border-outline-variant/30">
                <button
                  onClick={() => setShowPayDialog('final')}
                  className="w-full flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-primary text-on-primary text-label-md font-bold
                             hover:bg-primary-700 transition-colors"
                >
                  <span className="material-symbols-outlined text-[18px]">payments</span>
                  Thanh toán số còn lại — {formatCurrency(detail.quotation.remainingAmount)}
                </button>
              </div>
            )}

            {/* Payment History Toggle */}
            {paymentHistory.length > 0 && (
              <div className="mt-3 pt-3 border-t border-outline-variant/30">
                <button
                  onClick={() => setShowHistory(!showHistory)}
                  className="flex items-center gap-2 text-label-md font-semibold text-primary hover:text-primary-700 transition-colors"
                >
                  <span className="material-symbols-outlined text-[18px]">{showHistory ? 'expand_less' : 'expand_more'}</span>
                  {showHistory ? 'Ẩn lịch sử thanh toán' : `Xem lịch sử thanh toán (${paymentHistory.length})`}
                </button>
                {showHistory && (
                  <div className="mt-3">
                    <PaymentHistory
                      payments={paymentHistory}
                      onRetry={handleRetryPayment}
                      onCancel={(id) => { setShowCancelPaymentDialog(id); setCancelPaymentReason(''); }}
                      onDetail={handleShowPaymentDetail}
                      actionLoading={actionLoading}
                    />
                  </div>
                )}
              </div>
            )}
          </div>
        )}

        {/* Proposal */}
        {detail.proposal && (
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
            <h3 className="text-label-lg font-bold text-on-surface mb-3 flex items-center gap-2">
              <span className="material-symbols-outlined text-[18px] text-primary">description</span>
              Đề xuất vận chuyển
            </h3>
            <div className="space-y-2 text-body-md">
              <div className="flex justify-between">
                <span className="text-on-surface-variant">Trạng thái</span>
                <span className="text-on-surface font-medium">{detail.proposal.status || '-'}</span>
              </div>
              {detail.proposal.submittedAt && (
                <div className="flex justify-between">
                  <span className="text-on-surface-variant">Ngày nộp</span>
                  <span className="text-on-surface">{formatDate(detail.proposal.submittedAt)}</span>
                </div>
              )}
              {detail.proposal.rejectReason && (
                <div className="mt-2 p-3 rounded-xl bg-red-50 text-red-700 text-body-sm">
                  Lý do từ chối: {detail.proposal.rejectReason}
                </div>
              )}
            </div>
          </div>
        )}

        {/* Timeline */}
        {detail.timeline.length > 0 && (
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
            <h3 className="text-label-lg font-bold text-on-surface mb-3 flex items-center gap-2">
              <span className="material-symbols-outlined text-[18px] text-primary">timeline</span>
              Lịch sử trạng thái
            </h3>
            <div className="space-y-3">
              {detail.timeline.map((entry, idx) => (
                <div key={idx} className="flex items-start gap-3">
                  <div className="flex flex-col items-center">
                    <div className={`w-3 h-3 rounded-full ${entry.isCompleted ? 'bg-primary' : 'bg-outline-variant'}`}></div>
                    {idx < detail.timeline.length - 1 && <div className="w-0.5 h-6 bg-outline-variant/30 mt-1"></div>}
                  </div>
                  <div className="flex-1 pb-2">
                    <p className="text-body-md text-on-surface font-medium">{entry.label}</p>
                    {entry.timestamp && (
                      <p className="text-body-sm text-on-surface-variant">{formatDate(entry.timestamp)}</p>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Action Buttons */}
        <div className="flex gap-3 pt-2 pb-8">
          {detail.allowedActions.canEdit && (
            <button className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl border border-primary text-primary hover:bg-primary/5 transition-colors text-label-md font-bold">
              <span className="material-symbols-outlined text-[18px]">edit</span>
              Chỉnh sửa
            </button>
          )}
          {detail.allowedActions.canCancel && (
            <button
              onClick={() => setShowCancelDialog(true)}
              className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl border border-error text-error hover:bg-error/5 transition-colors text-label-md font-bold"
            >
              <span className="material-symbols-outlined text-[18px]">cancel</span>
              Hủy đơn
            </button>
          )}
        </div>
      </div>

      {/* Cancel Dialog */}
      {showCancelDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-6 w-full max-w-md card-shadow">
            <h3 className="text-title-lg font-bold text-on-surface mb-2">Xác nhận hủy đơn hàng</h3>
            <p className="text-body-md text-on-surface-variant mb-4">
              Hành động này không thể hoàn tác. Vui lòng nhập lý do hủy.
            </p>
            <textarea
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
              placeholder="Nhập lý do hủy..."
              className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md placeholder:text-on-surface-variant/50 focus:outline-none focus:border-primary resize-none"
              rows={3}
            />
            <div className="flex gap-3 mt-4">
              <button
                onClick={() => { setShowCancelDialog(false); setCancelReason(''); }}
                className="flex-1 px-4 py-3 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low transition-colors text-label-md font-bold"
              >
                Đóng
              </button>
              <button
                onClick={handleCancel}
                disabled={!cancelReason.trim() || cancelling}
                className="flex-1 px-4 py-3 rounded-xl bg-error text-on-error hover:bg-error/90 transition-colors text-label-md font-bold disabled:opacity-50"
              >
                {cancelling ? 'Đang hủy...' : 'Xác nhận hủy'}
              </button>
            </div>
          </div>
        </div>
      )}

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}

      {/* Cancel Payment Dialog */}
      {showCancelPaymentDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-6 w-full max-w-md card-shadow">
            <h3 className="text-title-lg font-bold text-on-surface mb-2">Xác nhận hủy thanh toán</h3>
            <p className="text-body-md text-on-surface-variant mb-4">
              Thanh toán này sẽ được hủy. Vui lòng nhập lý do hủy.
            </p>
            <textarea
              value={cancelPaymentReason}
              onChange={(e) => setCancelPaymentReason(e.target.value)}
              placeholder="Nhập lý do hủy..."
              className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md placeholder:text-on-surface-variant/50 focus:outline-none focus:border-primary resize-none"
              rows={3}
            />
            <div className="flex gap-3 mt-4">
              <button
                onClick={() => { setShowCancelPaymentDialog(null); setCancelPaymentReason(''); }}
                className="flex-1 px-4 py-3 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low transition-colors text-label-md font-bold"
              >
                Đóng
              </button>
              <button
                onClick={() => handleCancelPayment(showCancelPaymentDialog)}
                disabled={actionLoading === showCancelPaymentDialog}
                className="flex-1 px-4 py-3 rounded-xl bg-error text-on-error hover:bg-error/90 transition-colors text-label-md font-bold disabled:opacity-50"
              >
                {actionLoading === showCancelPaymentDialog ? 'Đang hủy...' : 'Xác nhận hủy'}
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

            {(!paymentDetailData && loadingTimeline) && (
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
                    <PaymentStatusBadge status={paymentDetailData.status} />
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Loại thanh toán</span>
                    <span className="text-body-md font-medium text-on-surface">
                      {paymentDetailData.paymentType === 'Deposit' ? 'Đặt cọc' : paymentDetailData.paymentType === 'FinalPayment' ? 'Thanh toán cuối' : paymentDetailData.paymentType}
                    </span>
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="text-body-md text-on-surface-variant">Số tiền</span>
                    <span className="text-body-lg font-bold text-primary">
                      {paymentDetailData.amount?.toLocaleString('vi-VN', { style: 'currency', currency: 'VND' })}
                    </span>
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
                    <span className="text-body-md text-on-surface">{new Date(paymentDetailData.createdAt).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })}</span>
                  </div>
                  {paymentDetailData.paidAt && (
                    <div className="flex justify-between items-center">
                      <span className="text-body-md text-on-surface-variant">Ngày thanh toán</span>
                      <span className="text-body-md text-emerald-600 font-medium">{new Date(paymentDetailData.paidAt).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })}</span>
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
                  <PaymentTimeline entries={paymentTimelineData} loading={loadingTimeline} />
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Payment Confirmation Dialog */}
      {showPayDialog && detail.quotation && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={() => !payingDeposit && !payingFinal && setShowPayDialog(null)}>
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-6 w-full max-w-md card-shadow" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-center gap-3 mb-4">
              <div className="w-10 h-10 rounded-full bg-primary/10 flex items-center justify-center">
                <span className="material-symbols-outlined text-primary text-xl">
                  {showPayDialog === 'deposit' ? 'account_balance_wallet' : 'payments'}
                </span>
              </div>
              <div>
                <h3 className="text-headline-sm font-bold text-on-surface">
                  {showPayDialog === 'deposit' ? 'Thanh toán đặt cọc' : 'Thanh toán số còn lại'}
                </h3>
                <p className="text-label-sm text-on-surface-variant">{detail.shipmentCode}</p>
              </div>
            </div>
            <div className="bg-surface-container-low rounded-xl p-4 mb-5 space-y-2">
              <div className="flex justify-between text-body-md">
                <span className="text-on-surface-variant">Phí vận chuyển:</span>
                <span className="font-bold">{formatCurrency(detail.quotation.shippingFee)}</span>
              </div>
              {showPayDialog === 'deposit' && (
                <>
                  <div className="flex justify-between text-body-md">
                    <span className="text-on-surface-variant">Số tiền đặt cọc:</span>
                    <span className="font-bold text-primary text-lg">{formatCurrency(detail.quotation.depositAmount)}</span>
                  </div>
                  <div className="flex justify-between text-body-md">
                    <span className="text-on-surface-variant">Thanh toán sau khi giao:</span>
                    <span className="font-semibold">{formatCurrency(detail.quotation.remainingAmount)}</span>
                  </div>
                </>
              )}
              {showPayDialog === 'final' && (
                <div className="flex justify-between text-body-md">
                  <span className="text-on-surface-variant">Số tiền cần thanh toán:</span>
                  <span className="font-bold text-primary text-lg">{formatCurrency(detail.quotation.remainingAmount)}</span>
                </div>
              )}
            </div>
            <p className="text-body-sm text-on-surface-variant mb-4">
              {showPayDialog === 'deposit'
                ? 'Sau khi đặt cọc, đơn hàng sẽ được xác nhận và tiến hành ghép chuyến.'
                : 'Thanh toán số tiền còn lại để hoàn tất đơn hàng.'}
            </p>
            <div className="flex gap-3 justify-end">
              <button
                onClick={() => setShowPayDialog(null)}
                disabled={payingDeposit || payingFinal}
                className="px-4 py-2.5 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low transition-colors text-label-md font-bold"
              >
                Đóng
              </button>
              <button
                onClick={showPayDialog === 'deposit' ? handlePayDeposit : handlePayFinal}
                disabled={payingDeposit || payingFinal}
                className="px-5 py-2.5 rounded-xl bg-primary text-on-primary font-bold text-sm
                           hover:bg-primary-700 disabled:opacity-50 transition-colors flex items-center gap-2"
              >
                {(showPayDialog === 'deposit' ? payingDeposit : payingFinal) ? (
                  <>
                    <span className="material-symbols-outlined animate-spin text-[16px]">sync</span>
                    Đang xử lý...
                  </>
                ) : (
                  <>
                    <span className="material-symbols-outlined text-[16px]">lock</span>
                    {showPayDialog === 'deposit' ? 'Xác nhận đặt cọc' : 'Xác nhận thanh toán'}
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
