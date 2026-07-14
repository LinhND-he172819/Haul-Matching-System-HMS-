import type { EligibleTrip } from '../../api/tripPostApi';

interface Props {
    trip: EligibleTrip;
}

export default function TripSummaryCard({ trip }: Props) {
    return (
        <div className="rounded-xl border border-primary/20 bg-primary/5 p-5">
            {/* Preview title */}
            <div className="mb-4 p-3 rounded-lg bg-surface-container-low border border-outline-variant/30">
                <p className="text-label-sm text-on-surface-variant mb-1">📌 Tiêu đề tự động</p>
                <p className="text-body-sm font-semibold text-on-surface break-words">
                    {trip.originHubName} → {trip.destinationHubName} | Xe {trip.licensePlate} | Còn {trip.remainingWeightKg.toLocaleString()} kg • {trip.remainingVolumeCbm.toLocaleString()} CBM
                </p>
            </div>

            {/* Trip info grid */}
            <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
                <InfoItem icon="alt_route" label="Tuyến" value={`${trip.originHubName} → ${trip.destinationHubName}`} />
                <InfoItem icon="person" label="Tài xế" value={trip.driverName} />
                <InfoItem icon="pin" label="Biển số" value={trip.licensePlate} />
                <InfoItem icon="local_shipping" label="Loại xe" value={trip.truckType} />
                <InfoItem icon="scale" label="Tải tối đa" value={`${trip.maxWeightKg.toLocaleString()} kg`} />
                <InfoItem icon="scale" label="Tải hiện tại" value={`${trip.currentLoadWeightKg.toLocaleString()} kg`} />
                <InfoItem icon="scale" label="Tải còn lại" value={`${trip.remainingWeightKg.toLocaleString()} kg`} highlight />
                <InfoItem icon="inventory_2" label="Thể tích tối đa" value={`${trip.maxVolumeCbm.toLocaleString()} CBM`} />
                <InfoItem icon="inventory_2" label="Thể tích hiện tại" value={`${trip.currentLoadVolumeCbm.toLocaleString()} CBM`} />
                <InfoItem icon="inventory_2" label="Thể tích còn lại" value={`${trip.remainingVolumeCbm.toLocaleString()} CBM`} highlight />
                <InfoItem icon="schedule" label="Khởi hành" value={trip.startedAt ? new Date(trip.startedAt).toLocaleString('vi-VN') : 'Chưa bắt đầu'} />
                <InfoItem icon="flag" label="Trạng thái" value={trip.status} />
            </div>
        </div>
    );
}

function InfoItem({ icon, label, value, highlight = false }: {
    icon: string;
    label: string;
    value: string;
    highlight?: boolean;
}) {
    return (
        <div className="flex items-start gap-2">
            <span className="material-symbols-outlined text-on-surface-variant/50 text-[16px] mt-0.5">{icon}</span>
            <div>
                <p className="text-label-sm text-on-surface-variant">{label}</p>
                <p className={`text-body-sm font-medium ${highlight ? 'text-secondary font-bold' : 'text-on-surface'}`}>
                    {value}
                </p>
            </div>
        </div>
    );
}
