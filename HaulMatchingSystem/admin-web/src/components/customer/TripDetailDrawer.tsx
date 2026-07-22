import type { PublicTripPost } from '../../api/tripPostApi';

interface TripDetailDrawerProps {
    trip: PublicTripPost | null;
    open: boolean;
    onClose: () => void;
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

export default function TripDetailDrawer({ trip, open, onClose, onNewProposal }: TripDetailDrawerProps) {
    if (!trip || !open) return null;

    const expired = isExpired(trip.acceptUntil);

    return (
        <>
            {/* Overlay */}
            <div
                className="fixed inset-0 bg-black/40 z-40 transition-opacity"
                onClick={onClose}
            />

            {/* Drawer */}
            <div className="fixed top-0 right-0 h-full w-full max-w-lg bg-white z-50 shadow-2xl flex flex-col animate-slide-in">
                {/* Header */}
                <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
                    <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-primary/10 rounded-xl flex items-center justify-center">
                            <span className="material-symbols-outlined text-primary text-[22px]">route</span>
                        </div>
                        <div>
                            <h2 className="text-lg font-bold text-gray-800">{trip.title}</h2>
                            <span className={`text-xs font-bold px-2.5 py-0.5 rounded-full ${
                                expired
                                    ? 'bg-gray-100 text-gray-500'
                                    : 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                            }`}>
                                {expired ? 'Hết hạn' : 'Đang nhận hàng'}
                            </span>
                        </div>
                    </div>
                    <button
                        onClick={onClose}
                        className="w-9 h-9 rounded-lg flex items-center justify-center hover:bg-gray-100 text-gray-500 transition-colors"
                    >
                        <span className="material-symbols-outlined text-[22px]">close</span>
                    </button>
                </div>

                {/* Content */}
                <div className="flex-1 overflow-y-auto px-6 py-5 space-y-5">
                    {/* Route */}
                    <div className="bg-gray-50 rounded-xl p-4">
                        <h3 className="text-sm font-bold text-gray-500 mb-3 flex items-center gap-1.5">
                            <span className="material-symbols-outlined text-[16px]">alt_route</span>
                            Tuyến đường
                        </h3>
                        <div className="flex items-center gap-3">
                            <div className="flex-1 text-center">
                                <p className="text-xs text-gray-400 mb-1">Điểm đi</p>
                                <p className="font-bold text-gray-800">{trip.originHubName}</p>
                            </div>
                            <span className="material-symbols-outlined text-primary text-[24px]">arrow_forward</span>
                            <div className="flex-1 text-center">
                                <p className="text-xs text-gray-400 mb-1">Điểm đến</p>
                                <p className="font-bold text-gray-800">{trip.destinationHubName}</p>
                            </div>
                        </div>
                    </div>

                    {/* Driver & Vehicle */}
                    <div className="bg-gray-50 rounded-xl p-4">
                        <h3 className="text-sm font-bold text-gray-500 mb-3 flex items-center gap-1.5">
                            <span className="material-symbols-outlined text-[16px]">person</span>
                            Tài xế & Xe
                        </h3>
                        <div className="space-y-2 text-sm">
                            <div className="flex justify-between">
                                <span className="text-gray-500">Tài xế</span>
                                <span className="font-medium text-gray-800">{trip.driverName}</span>
                            </div>
                            <div className="flex justify-between">
                                <span className="text-gray-500">Loại xe</span>
                                <span className="font-medium text-gray-800">{trip.truckType}</span>
                            </div>
                            <div className="flex justify-between">
                                <span className="text-gray-500">Biển số</span>
                                <span className="font-medium text-gray-800">{trip.licensePlate}</span>
                            </div>
                            {/* Pickup mode */}
                            <div className="flex justify-between items-center">
                                <span className="text-gray-500">Hình thức giao</span>
                                <span className={`text-xs font-bold px-2.5 py-0.5 rounded-full ${
                                    trip.pickupMode === 'DirectPickup'
                                        ? 'bg-amber-50 text-amber-700 border border-amber-200'
                                        : 'bg-blue-50 text-blue-700 border border-blue-200'
                                }`}>
                                    {trip.pickupMode === 'DirectPickup' ? '🚚 Tài xế đến nhận' : '🏢 Giao tại Hub'}
                                </span>
                            </div>
                        </div>
                    </div>

                    {/* Capacity */}
                    <div className="bg-gray-50 rounded-xl p-4">
                        <h3 className="text-sm font-bold text-gray-500 mb-3 flex items-center gap-1.5">
                            <span className="material-symbols-outlined text-[16px]">scale</span>
                            Trọng tải & Thể tích còn lại
                        </h3>
                        <div className="space-y-3">
                            <div>
                                <div className="flex items-center justify-between text-sm mb-1">
                                    <span className="text-gray-500">Khối lượng</span>
                                    <span className="font-bold text-primary">{trip.remainingWeightKg.toLocaleString()} kg</span>
                                </div>
                                <div className="w-full h-2 bg-gray-200 rounded-full overflow-hidden">
                                    <div className="h-full bg-primary rounded-full" style={{ width: '60%' }} />
                                </div>
                            </div>
                            <div>
                                <div className="flex items-center justify-between text-sm mb-1">
                                    <span className="text-gray-500">Thể tích</span>
                                    <span className="font-bold text-secondary">{trip.remainingVolumeCbm} CBM</span>
                                </div>
                                <div className="w-full h-2 bg-gray-200 rounded-full overflow-hidden">
                                    <div className="h-full bg-secondary rounded-full" style={{ width: '50%' }} />
                                </div>
                            </div>
                        </div>
                    </div>

                    {/* Schedule */}
                    <div className="bg-gray-50 rounded-xl p-4">
                        <h3 className="text-sm font-bold text-gray-500 mb-3 flex items-center gap-1.5">
                            <span className="material-symbols-outlined text-[16px]">schedule</span>
                            Thời gian
                        </h3>
                        <div className="space-y-2 text-sm">
                            <div className="flex justify-between">
                                <span className="text-gray-500">Ngày khởi hành</span>
                                <span className="font-medium text-gray-800">{formatDate(trip.departureTime)}</span>
                            </div>
                            <div className="flex justify-between">
                                <span className="text-gray-500">Hạn nhận đề xuất</span>
                                <span className="font-medium text-gray-800">{formatDateTime(trip.acceptUntil)}</span>
                            </div>
                        </div>
                    </div>

                    {/* Description */}
                    {trip.description && (
                        <div className="bg-gray-50 rounded-xl p-4">
                            <h3 className="text-sm font-bold text-gray-500 mb-2 flex items-center gap-1.5">
                                <span className="material-symbols-outlined text-[16px]">description</span>
                                Mô tả
                            </h3>
                            <p className="text-sm text-gray-600 leading-relaxed">{trip.description}</p>
                        </div>
                    )}
                </div>

                {/* Footer */}
                <div className="px-6 py-4 border-t border-gray-100">
                    <button
                        onClick={() => onNewProposal(trip)}
                        disabled={expired}
                        className="w-full flex items-center justify-center gap-2 px-6 py-3 bg-primary text-white font-bold text-sm rounded-xl hover:bg-primary-container transition-colors shadow-md disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                        <span className="material-symbols-outlined text-[20px]">add_task</span>
                        Tạo đề xuất ghép chuyến
                    </button>
                </div>
            </div>
        </>
    );
}
