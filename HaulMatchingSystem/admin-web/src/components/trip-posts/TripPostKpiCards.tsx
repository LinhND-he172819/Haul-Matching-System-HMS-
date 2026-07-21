import type { TripPostKpi } from '../../api/tripPostApi';

interface Props {
    kpi: TripPostKpi | null;
    loading: boolean;
}

const cards = [
    { key: 'totalPosts', label: 'Tổng bài đăng', icon: 'article', color: 'text-primary bg-primary/10' },
    { key: 'openPosts', label: 'Đang mở', icon: 'radio_button_checked', color: 'text-emerald-600 bg-emerald-50' },
    { key: 'closedPosts', label: 'Đã đóng', icon: 'lock', color: 'text-slate-500 bg-slate-100' },
    { key: 'expiredPosts', label: 'Hết hạn', icon: 'schedule', color: 'text-amber-600 bg-amber-50' },
    { key: 'cancelledPosts', label: 'Đã hủy', icon: 'cancel', color: 'text-red-500 bg-red-50' },
    { key: 'eligibleTrips', label: 'Trip đủ điều kiện', icon: 'local_shipping', color: 'text-violet-600 bg-violet-50' },
] as const;

export default function TripPostKpiCards({ kpi, loading }: Props) {
    return (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
            {cards.map(c => (
                <div
                    key={c.key}
                    className="rounded-xl border border-outline-variant/40 bg-surface-container-lowest p-4 flex flex-col items-center gap-2 shadow-sm hover:shadow-md transition-shadow"
                >
                    <div className={`w-10 h-10 rounded-lg flex items-center justify-center ${c.color}`}>
                        <span className="material-symbols-outlined text-[20px]">{c.icon}</span>
                    </div>
                    <div className="text-center">
                        {loading ? (
                            <div className="h-7 w-10 mx-auto bg-surface-variant/50 rounded animate-pulse" />
                        ) : (
                            <p className="text-headline-sm font-bold text-on-surface">
                                {kpi ? (kpi as Record<string, number>)[c.key] ?? 0 : '—'}
                            </p>
                        )}
                        <p className="text-label-sm text-on-surface-variant mt-0.5">{c.label}</p>
                    </div>
                </div>
            ))}
        </div>
    );
}
