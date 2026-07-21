import type { TripPostStatus } from '../../api/tripPostApi';

const statusConfig: Record<TripPostStatus, { label: string; className: string }> = {
    Open: 'border-emerald-400/30 bg-emerald-50 text-emerald-700 ring-1 ring-emerald-600/20',
    Closed: 'border-slate-300 bg-slate-100 text-slate-600 ring-1 ring-slate-500/20',
    Expired: 'border-amber-300 bg-amber-50 text-amber-700 ring-1 ring-amber-500/20',
    Cancelled: 'border-red-300 bg-red-50 text-red-600 ring-1 ring-red-500/20',
};

const statusLabels: Record<TripPostStatus, string> = {
    Open: 'Đang mở',
    Closed: 'Đã đóng',
    Expired: 'Đã hết hạn',
    Cancelled: 'Đã hủy',
};

export default function StatusBadge({ status }: { status: TripPostStatus | string }) {
    const s = status as TripPostStatus;
    const style = statusConfig[s] ?? statusConfig.Open;
    const label = statusLabels[s] ?? status;

    return (
        <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold ${style}`}>
            {s === 'Open' && (
                <span className="relative flex h-2 w-2">
                    <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75"></span>
                    <span className="relative inline-flex h-2 w-2 rounded-full bg-emerald-500"></span>
                </span>
            )}
            {label}
        </span>
    );
}
