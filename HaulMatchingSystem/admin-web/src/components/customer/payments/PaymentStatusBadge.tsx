const STATUS_STYLES: Record<string, { bg: string; text: string; label: string }> = {
  Pending: { bg: 'bg-amber-50 border-amber-200', text: 'text-amber-700', label: 'Đang chờ thanh toán' },
  Paid: { bg: 'bg-emerald-50 border-emerald-200', text: 'text-emerald-700', label: 'Đã thanh toán' },
  Failed: { bg: 'bg-rose-50 border-rose-200', text: 'text-rose-700', label: 'Thanh toán thất bại' },
  Cancelled: { bg: 'bg-gray-50 border-gray-200', text: 'text-gray-600', label: 'Đã hủy' },
  PendingRefund: { bg: 'bg-amber-50 border-amber-200', text: 'text-amber-700', label: 'Đang chờ hoàn tiền' },
  Refunded: { bg: 'bg-blue-50 border-blue-200', text: 'text-blue-700', label: 'Đã hoàn tiền' },
  PartiallyRefunded: { bg: 'bg-blue-50 border-blue-200', text: 'text-blue-700', label: 'Hoàn tiền một phần' },
};

interface PaymentStatusBadgeProps {
  status: string;
}

export default function PaymentStatusBadge({ status }: PaymentStatusBadgeProps) {
  const style = STATUS_STYLES[status] || {
    bg: 'bg-gray-50 border-gray-200',
    text: 'text-gray-600',
    label: status,
  };

  return (
    <span
      className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-label-sm font-semibold border ${style.bg} ${style.text}`}
    >
      {status === 'Paid' && <span className="material-symbols-outlined text-[14px]">check_circle</span>}
      {status === 'Failed' && <span className="material-symbols-outlined text-[14px]">error</span>}
      {status === 'Pending' && <span className="material-symbols-outlined text-[14px]">schedule</span>}
      {style.label}
    </span>
  );
}
