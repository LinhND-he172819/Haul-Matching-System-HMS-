import type { ShipmentSuggestionDto } from '../../api/matchingApi';

interface Props {
    shipment: ShipmentSuggestionDto;
    selected: boolean;
    matchPercent?: number;
    distanceKm?: number;
    onSelect: (id: string, selected: boolean) => void;
    onAccept: (id: string) => void;
    onReject: (id: string) => void;
    onDetails?: (id: string) => void;
}

export default function ShipmentSuggestionCard({ shipment, selected, distanceKm = 2.1, onSelect, onAccept, onDetails }: Props) {
    return (
        <div className="soft-card p-4">
            <div className="flex items-start justify-between">
                <div className="flex items-start gap-3">
                    <input
                        aria-label="select shipment"
                        type="checkbox"
                        checked={selected}
                        onChange={e => onSelect(shipment.shipmentId, e.target.checked)}
                        className="mt-1 h-4 w-4 accent-[#1b39b7]"
                    />
                    <div>
                        <div className="text-sm font-semibold">{shipment.receiverName || 'GC-VN'}-{shipment.shipmentId.slice(0, 4)}</div>
                        <div className="text-xs text-slate-500">Cách vị trí hiện tại: {distanceKm}km</div>
                    </div>
                </div>
                {/* <span className="pill">
                    <span className="material-symbols-outlined text-sm">bolt</span>
                    {matchPercent}% Match
                </span> */}
            </div>

            <div className="mt-3 grid grid-cols-2 gap-2 text-xs">
                <div className="flex items-center gap-2 rounded-lg bg-[#f0f4ff] px-3 py-2">
                    <span className="material-symbols-outlined text-sm text-slate-500">inventory_2</span>
                    {shipment.weightKg} kg
                </div>
                <div className="flex items-center gap-2 rounded-lg bg-[#f0f4ff] px-3 py-2">
                    <span className="material-symbols-outlined text-sm text-slate-500">box</span>
                    {shipment.volumeCbm} m³
                </div>
            </div>

            <div className="mt-3 flex items-center gap-2 text-xs text-slate-600">
                <span className="material-symbols-outlined text-sm text-[#1b39b7]">location_on</span>
                Pick: {shipment.destinationAddress}
            </div>

            {shipment.specialHandlingNote && (
                <div className="mt-2 text-xs text-slate-500">Ghi chú: {shipment.specialHandlingNote}</div>
            )}

            <div className="mt-4 flex gap-2">
                <button className="btn-primary flex-1" onClick={() => onAccept(shipment.shipmentId)}>Chọn Ghép</button>
                <button className="btn-outline" onClick={() => onDetails?.(shipment.shipmentId)}>Chi Tiết</button>
                {/* <button className="btn-ghost" onClick={() => onReject(shipment.shipmentId)}>Từ chối</button> */}
            </div>
        </div>
    );
}
