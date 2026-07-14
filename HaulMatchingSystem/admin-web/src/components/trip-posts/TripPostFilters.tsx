import { useState } from 'react';
import HubSelector from './HubSelector';
import type { TripPostStatus } from '../../api/tripPostApi';

interface Filters {
    keyword: string;
    status: TripPostStatus | '';
    hubId: string;
    fromDate: string;
    toDate: string;
}

interface Props {
    filters: Filters;
    onFilterChange: (filters: Filters) => void;
    loading?: boolean;
}

const statuses: { value: TripPostStatus | ''; label: string }[] = [
    { value: '', label: 'Tất cả trạng thái' },
    { value: 'Open', label: 'Đang mở' },
    { value: 'Closed', label: 'Đã đóng' },
    { value: 'Expired', label: 'Đã hết hạn' },
    { value: 'Cancelled', label: 'Đã hủy' },
];

export default function TripPostFilters({ filters, onFilterChange, loading }: Props) {
    const [local, setLocal] = useState(filters);

    const update = (partial: Partial<Filters>) => {
        const next = { ...local, ...partial };
        setLocal(next);
        onFilterChange(next);
    };

    const clearAll = () => {
        const empty: Filters = { keyword: '', status: '', hubId: '', fromDate: '', toDate: '' };
        setLocal(empty);
        onFilterChange(empty);
    };

    return (
        <div className="rounded-xl border border-outline-variant/40 bg-surface-container-lowest p-4 space-y-3">
            {/* Row 1: Keyword + Status + Hub */}
            <div className="flex flex-wrap items-end gap-3">
                <div className="flex-1 min-w-[200px]">
                    <label className="block text-label-sm text-on-surface-variant mb-1">Tìm kiếm</label>
                    <div className="relative">
                        <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant/50 text-[18px]">
                            search
                        </span>
                        <input
                            type="text"
                            value={local.keyword}
                            onChange={e => update({ keyword: e.target.value })}
                            placeholder="Tìm tiêu đề..."
                            className="w-full rounded-lg border border-outline-variant bg-surface-container-lowest pl-9 pr-3 py-2 text-body-sm text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
                        />
                    </div>
                </div>

                <div className="min-w-[180px]">
                    <label className="block text-label-sm text-on-surface-variant mb-1">Trạng thái</label>
                    <select
                        value={local.status}
                        onChange={e => update({ status: e.target.value as TripPostStatus | '' })}
                        disabled={loading}
                        className="w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 py-2 text-body-sm text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
                    >
                        {statuses.map(s => (
                            <option key={s.value} value={s.value}>{s.label}</option>
                        ))}
                    </select>
                </div>

                <HubSelector
                    selectedHubId={local.hubId}
                    onChange={hubId => update({ hubId })}
                    loading={loading}
                />

                <button
                    onClick={clearAll}
                    className="flex items-center gap-1 text-label-sm text-primary hover:text-primary/80 py-2"
                >
                    <span className="material-symbols-outlined text-[16px]">restart_alt</span>
                    Xoá bộ lọc
                </button>
            </div>

            {/* Row 2: Date range */}
            <div className="flex items-end gap-3">
                <div className="min-w-[180px]">
                    <label className="block text-label-sm text-on-surface-variant mb-1">Từ ngày</label>
                    <input
                        type="date"
                        value={local.fromDate}
                        onChange={e => update({ fromDate: e.target.value })}
                        className="w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 py-2 text-body-sm text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
                    />
                </div>
                <div className="min-w-[180px]">
                    <label className="block text-label-sm text-on-surface-variant mb-1">Đến ngày</label>
                    <input
                        type="date"
                        value={local.toDate}
                        onChange={e => update({ toDate: e.target.value })}
                        className="w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 py-2 text-body-sm text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
                    />
                </div>
            </div>
        </div>
    );
}
