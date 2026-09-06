import type { PaymentTimelineEntry } from '../../../api/customer/customerQuotationApi';

const STATUS_ICONS: Record<string, string> = {
  Pending: 'schedule',
  Paid: 'check_circle',
  Failed: 'error',
  Cancelled: 'cancel',
  PendingRefund: 'pending',
  Refunded: 'replay',
  PartiallyRefunded: 'replay',
};

const STATUS_COLORS: Record<string, string> = {
  Pending: 'bg-amber-100 text-amber-600',
  Paid: 'bg-emerald-100 text-emerald-600',
  Failed: 'bg-rose-100 text-rose-600',
  Cancelled: 'bg-gray-100 text-gray-500',
  PendingRefund: 'bg-amber-100 text-amber-600',
  Refunded: 'bg-blue-100 text-blue-600',
  PartiallyRefunded: 'bg-blue-100 text-blue-600',
};

const STATUS_LABELS: Record<string, string> = {
  Pending: 'Đang chờ',
  Paid: 'Đã thanh toán',
  Failed: 'Thất bại',
  Cancelled: 'Đã hủy',
  PendingRefund: 'Chờ hoàn tiền',
  Refunded: 'Đã hoàn tiền',
  PartiallyRefunded: 'Hoàn tiền một phần',
};

interface PaymentTimelineProps {
  entries: PaymentTimelineEntry[];
  loading?: boolean;
}

export default function PaymentTimeline({ entries, loading }: PaymentTimelineProps) {
  const formatDate = (s: string) =>
    new Date(s).toLocaleDateString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });

  if (loading) {
    return (
      <div className="space-y-4">
        {[1, 2, 3].map((i) => (
          <div key={i} className="h-16 bg-gray-100 rounded-xl animate-pulse" />
        ))}
      </div>
    );
  }

  if (entries.length === 0) {
    return (
      <div className="text-center py-6 text-on-surface-variant">
        <span className="material-symbols-outlined text-[32px] text-gray-300">timeline</span>
        <p className="text-body-md mt-2">Chưa có lịch sử trạng thái</p>
      </div>
    );
  }

  return (
    <div className="relative">
      {/* Vertical line */}
      <div className="absolute left-5 top-0 bottom-0 w-0.5 bg-outline-variant/30" />

      <div className="space-y-4">
        {entries.map((entry, idx) => (
          <div key={idx} className="flex items-start gap-3 relative">
            {/* Status dot */}
            <div
              className={`w-10 h-10 rounded-full flex items-center justify-center z-10 flex-shrink-0 ${
                STATUS_COLORS[entry.status] || 'bg-gray-100 text-gray-500'
              }`}
            >
              <span className="material-symbols-outlined text-[18px]">
                {STATUS_ICONS[entry.status] || 'circle'}
              </span>
            </div>

            {/* Content */}
            <div className="flex-1 pb-1">
              <div className="flex items-center gap-2">
                <p className="text-body-md font-semibold text-on-surface">
                  {STATUS_LABELS[entry.status] || entry.status}
                </p>
                <span className="text-label-sm text-on-surface-variant">
                  {formatDate(entry.occurredAt)}
                </span>
              </div>
              <p className="text-body-sm text-on-surface-variant mt-0.5">
                {entry.action}
              </p>
              {entry.details && (
                <p className="text-label-sm text-on-surface-variant/70 mt-1 italic">
                  {entry.details}
                </p>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
