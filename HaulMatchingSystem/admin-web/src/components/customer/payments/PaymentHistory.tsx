import type { PaymentHistoryEntry } from '../../api/customer/customerQuotationApi';
import PaymentStatusBadge from './PaymentStatusBadge';

const TYPE_LABELS: Record<string, string> = {
  Deposit: 'Đặt cọc',
  FinalPayment: 'Thanh toán cuối',
  AdditionalCharge: 'Phụ phí',
  Refund: 'Hoàn tiền',
};

interface PaymentHistoryProps {
  payments: PaymentHistoryEntry[];
  loading?: boolean;
  onRetry?: (paymentId: string) => void;
  onCancel?: (paymentId: string) => void;
  onDetail?: (paymentId: string) => void;
  actionLoading?: string | null;
}

export default function PaymentHistory({ payments, loading, onRetry, onCancel, onDetail, actionLoading }: PaymentHistoryProps) {
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

  if (loading) {
    return (
      <div className="space-y-3">
        {[1, 2].map((i) => (
          <div key={i} className="h-16 bg-gray-100 rounded-xl animate-pulse" />
        ))}
      </div>
    );
  }

  if (payments.length === 0) {
    return (
      <div className="text-center py-6 text-on-surface-variant">
        <span className="material-symbols-outlined text-[32px] text-gray-300">receipt_long</span>
        <p className="text-body-md mt-2">Chưa có lịch sử thanh toán</p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {payments.map((p) => (
        <div
          key={p.id}
          className="flex items-center justify-between p-4 bg-white border border-outline-variant rounded-xl"
        >
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-full bg-surface-container-low flex items-center justify-center">
              <span className="material-symbols-outlined text-[20px] text-on-surface-variant">
                {p.paymentType === 'Deposit' ? 'account_balance_wallet' : 'payments'}
              </span>
            </div>
            <div>
              <p className="text-body-md font-semibold text-on-surface">
                {TYPE_LABELS[p.paymentType] || p.paymentType}
              </p>
              <p className="text-label-sm text-on-surface-variant">
                {formatDate(p.createdAt)}
                {p.transactionReference && ` • ${p.transactionReference}`}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            {/* Action Buttons */}
            <div className="flex items-center gap-1.5">
              {p.status === 'Failed' && onRetry && (
                <button
                  onClick={() => onRetry(p.id)}
                  disabled={actionLoading === p.id}
                  className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg bg-amber-50 text-amber-700 border border-amber-200 hover:bg-amber-100 transition-colors text-label-sm font-semibold disabled:opacity-50"
                >
                  <span className="material-symbols-outlined text-[14px]">
                    {actionLoading === p.id ? 'sync' : 'refresh'}
                  </span>
                  Thử lại
                </button>
              )}
              {p.status === 'Pending' && onCancel && (
                <button
                  onClick={() => onCancel(p.id)}
                  disabled={actionLoading === p.id}
                  className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg bg-rose-50 text-rose-700 border border-rose-200 hover:bg-rose-100 transition-colors text-label-sm font-semibold disabled:opacity-50"
                >
                  <span className="material-symbols-outlined text-[14px]">
                    {actionLoading === p.id ? 'sync' : 'cancel'}
                  </span>
                  Hủy
                </button>
              )}
              {onDetail && (
                <button
                  onClick={() => onDetail(p.id)}
                  className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg bg-surface-container-low text-on-surface-variant border border-outline-variant hover:bg-surface-container transition-colors text-label-sm font-semibold"
                >
                  <span className="material-symbols-outlined text-[14px]">info</span>
                  Chi tiết
                </button>
              )}
            </div>
            <div className="text-right">
              <p className="text-body-md font-bold text-on-surface">{formatCurrency(p.amount)}</p>
              <PaymentStatusBadge status={p.status} />
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
