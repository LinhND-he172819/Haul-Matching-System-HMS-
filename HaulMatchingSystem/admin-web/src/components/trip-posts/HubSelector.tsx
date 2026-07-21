import { useEffect, useState } from 'react';
import { fetchHubs, type Hub } from '../../api/tripPostApi';

interface Props {
    selectedHubId: string;
    onChange: (hubId: string) => void;
    loading?: boolean;
}

export default function HubSelector({ selectedHubId, onChange, loading }: Props) {
    const [hubs, setHubs] = useState<Hub[]>([]);

    useEffect(() => {
        fetchHubs()
            .then(setHubs)
            .catch(() => {});
    }, []);

    return (
        <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-on-surface-variant text-[20px]">hub</span>
            <select
                value={selectedHubId}
                onChange={e => onChange(e.target.value)}
                disabled={loading}
                className="rounded-lg border border-outline-variant bg-surface-container-lowest px-3 py-2 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
            >
                <option value="">Tất cả Hub</option>
                {hubs.map(h => (
                    <option key={h.id} value={h.id}>{h.name}</option>
                ))}
            </select>
        </div>
    );
}
