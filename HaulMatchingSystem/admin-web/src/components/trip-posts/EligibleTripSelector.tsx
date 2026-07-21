import type { EligibleTrip } from '../../api/tripPostApi';

interface Props {
    trips: EligibleTrip[];
    selectedTripId: string;
    onSelect: (trip: EligibleTrip) => void;
    loading: boolean;
}

export default function EligibleTripSelector({ trips, selectedTripId, onSelect, loading }: Props) {
    if (loading) {
        return (
            <div className="rounded-xl border border-outline-variant/40 bg-surface-container-lowest p-4">
                <div className="flex items-center gap-2 mb-3">
                    <span className="material-symbols-outlined animate-spin text-primary text-[20px]">sync</span>
                    <span className="text-body-sm text-on-surface-variant">Đang tải chuyến...</span>
                </div>
                <div className="space-y-2">
                    {[1, 2, 3].map(i => (
                        <div key={i} className="h-16 bg-surface-variant/30 rounded-lg animate-pulse" />
                    ))}
                </div>
            </div>
        );
    }

    if (trips.length === 0) {
        return (
            <div className="rounded-xl border border-outline-variant/40 bg-surface-container-lowest p-6 text-center">
                <span className="material-symbols-outlined text-on-surface-variant/40 text-[40px]">local_shipping</span>
                <p className="text-body-md text-on-surface-variant mt-2">Không có chuyến nào đủ điều kiện đăng bài.</p>
            </div>
        );
    }

    return (
        <div className="rounded-xl border border-outline-variant/40 bg-surface-container-lowest overflow-hidden">
            <div className="px-4 py-3 border-b border-outline-variant/30 bg-surface-container-low">
                <h3 className="text-label-lg font-bold text-on-surface">Chọn chuyến xe ({trips.length})</h3>
            </div>
            <div className="max-h-80 overflow-y-auto divide-y divide-outline-variant/20">
                {trips.map(trip => (
                    <button
                        key={trip.tripId}
                        onClick={() => onSelect(trip)}
                        className={`w-full px-4 py-3 text-left transition-all hover:bg-primary/5 ${
                            selectedTripId === trip.tripId
                                ? 'bg-primary/10 border-l-4 border-l-primary'
                                : 'border-l-4 border-l-transparent'
                        }`}
                    >
                        <div className="flex items-center justify-between gap-3">
                            <div className="flex-1 min-w-0">
                                <p className="text-body-sm font-semibold text-on-surface truncate">
                                    {trip.originHubName} → {trip.destinationHubName}
                                </p>
                                <p className="text-label-sm text-on-surface-variant mt-0.5">
                                    🚛 {trip.licensePlate} • {trip.driverName}
                                </p>
                            </div>
                            <div className="text-right shrink-0">
                                <p className="text-label-sm font-semibold text-secondary">
                                    {trip.remainingWeightKg.toLocaleString()} kg
                                </p>
                                <p className="text-label-sm text-on-surface-variant">
                                    {trip.remainingVolumeCbm.toLocaleString()} CBM
                                </p>
                            </div>
                        </div>
                    </button>
                ))}
            </div>
        </div>
    );
}
