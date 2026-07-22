import type { PublicTripPost } from '../../api/tripPostApi';

interface TripCardProps {
    trip: PublicTripPost;
    onViewDetail: (trip: PublicTripPost) => void;
    onNewProposal: (trip: PublicTripPost) => void;
}

function formatDate(dateStr: string | null): string {
    if (!dateStr) return '—';
    const d = new Date(dateStr);
    return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

function formatDateTime(dateStr: string | null): string {
    if (!dateStr) return '—';
    const d = new Date(dateStr);
    return d.toLocaleDateString('vi-VN', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit',
    });
}

function isExpired(acceptUntil: string): boolean {
    return new Date(acceptUntil) < new Date();
}

export default function TripCard({ trip, onViewDetail, onNewProposal }: TripCardProps) {
    const expired = isExpired(trip.acceptUntil);

    return (
        <div className="bg-white rounded-2xl shadow-md border border-gray-100 overflow-hidden flex flex-col transition-shadow hover:shadow-lg group">
            {/* Header */}
            <div className="px-5 pt-5 pb-3">
                <div className="flex items-start justify-between mb-3">
                    <h3 className="text-lg font-bold text-gray-800 leading-tight">
                        {trip.title}
                    </h3>
                    <div className="flex flex-col items-end gap-1 shrink-0 ml-2">
                        {/* Pickup mode badge */}
                        <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${
                            trip.pickupMode === 'DirectPickup'
                                ? 'bg-amber-50 text-amber-700 border border-amber-200'
                                : 'bg-blue-50 text-blue-700 border border-blue-200'
                        }`}>
                            {trip.pickupMode === 'DirectPickup' ? '🚚 Tài xế đến nhận' : '🏢 Giao tại Hub'}
                        </span>
                        <span className={`text-xs font-bold px-3 py-1 rounded-full ${
                            expired
                                ? 'bg-gray-100 text-gray-500'
                                : 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                        }`}>
                            {expired ? 'Hết hạn' : 'Đang nhận hàng'}
                        </span>
                    </div>
                </div>

                {/* Route */}
                <div className="flex items-center gap-2 text-sm text-gray-500 mb-3">
                    <span className="material-symbols-outlined text-primary text-[18px]">alt_route</span>
                    <span className="font-medium">{trip.originHubName}</span>
                    <span className="material-symbols-outlined text-gray-300 text-[16px]">arrow_forward</span>
                    <span className="font-medium">{trip.destinationHubName}</span>
                </div>

                {/* Info rows */}
                <div className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
                    <div className="flex items-center gap-2 text-gray-500">
                        <span className="material-symbols-outlined text-[16px] text-gray-400">calendar_today</span>
                        <span>Khởi hành: <span className="font-medium text-gray-700">{formatDate(trip.departureTime)}</span></span>
                    </div>
                    <div className="flex items-center gap-2 text-gray-500">
                        <span className="material-symbols-outlined text-[16px] text-gray-400">schedule</span>
                        <span>Hạn nhận: <span className="font-medium text-gray-700">{formatDateTime(trip.acceptUntil)}</span></span>
                    </div>
                    <div className="flex items-center gap-2 text-gray-500">
                        <span className="material-symbols-outlined text-[16px] text-gray-400">person</span>
                        <span>{trip.driverName}</span>
                    </div>
                    <div className="flex items-center gap-2 text-gray-500">
                        <span className="material-symbols-outlined text-[16px] text-gray-400">local_shipping</span>
                        <span>{trip.truckType} • {trip.licensePlate}</span>
                    </div>
                </div>
            </div>

            {/* Capacity section */}
            <div className="px-5 pb-4 pt-2">
                {/* Weight */}
                <div className="mb-2">
                    <div className="flex items-center justify-between text-xs mb-1">
                        <span className="text-gray-500 font-medium">Khối lượng còn nhận</span>
                        <span className="font-bold text-primary">{trip.remainingWeightKg.toLocaleString()} kg</span>
                    </div>
                    <div className="w-full h-1.5 bg-gray-100 rounded-full overflow-hidden">
                        <div
                            className="h-full bg-primary rounded-full transition-all"
                            style={{ width: '60%' }}
                        />
                    </div>
                </div>

                {/* Volume */}
                <div>
                    <div className="flex items-center justify-between text-xs mb-1">
                        <span className="text-gray-500 font-medium">Thể tích còn nhận</span>
                        <span className="font-bold text-secondary">{trip.remainingVolumeCbm} CBM</span>
                    </div>
                    <div className="w-full h-1.5 bg-gray-100 rounded-full overflow-hidden">
                        <div
                            className="h-full bg-secondary rounded-full transition-all"
                            style={{ width: '50%' }}
                        />
                    </div>
                </div>
            </div>

            {/* Description */}
            {trip.description && (
                <div className="px-5 pb-3">
                    <p className="text-xs text-gray-400 line-clamp-2">{trip.description}</p>
                </div>
            )}

            {/* Actions */}
            <div className="px-5 pb-5 mt-auto flex items-center gap-3">
                <button
                    onClick={() => onViewDetail(trip)}
                    className="flex-1 flex items-center justify-center gap-1.5 px-4 py-2.5 border border-gray-200 text-gray-600 font-medium text-sm rounded-xl hover:bg-gray-50 transition-colors"
                >
                    <span className="material-symbols-outlined text-[18px]">visibility</span>
                    Xem chi tiết
                </button>
                <button
                    onClick={() => onNewProposal(trip)}
                    disabled={expired}
                    className="flex-1 flex items-center justify-center gap-1.5 px-4 py-2.5 bg-primary text-white font-bold text-sm rounded-xl hover:bg-primary-container transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
                >
                    <span className="material-symbols-outlined text-[18px]">add_task</span>
                    Tạo đề xuất
                </button>
            </div>
        </div>
    );
}
