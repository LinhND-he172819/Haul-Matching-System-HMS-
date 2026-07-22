import type { ProposalDto } from '../../api/proposalApi';

interface Props {
    proposal: ProposalDto;
    onAccept: (proposalId: string) => void;
    onReject: (proposalId: string) => void;
}

function shortId(id: string) {
    return id.slice(0, 8);
}

export default function ProposalCard({ proposal, onAccept, onReject }: Props) {
    return (
        <div className="soft-card p-5 transition-all duration-200 hover:shadow-lg">
            {/* Header: Shipment code + Status */}
            <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-2">
                    <span className="material-symbols-outlined text-[18px] text-primary">inventory_2</span>
                    <span className="text-label-lg font-bold text-on-surface">
                        #{proposal.shipmentCode ?? shortId(proposal.shipmentId)}
                    </span>
                </div>
                <span className={`px-2.5 py-0.5 rounded-full text-[11px] font-bold uppercase tracking-wide ${
                    proposal.status === 'Pending' ? 'bg-amber-100 text-amber-700' :
                    proposal.status === 'Accepted' ? 'bg-emerald-100 text-emerald-700' :
                    proposal.status === 'Rejected' ? 'bg-red-100 text-red-700' :
                    'bg-gray-100 text-gray-600'
                }`}>
                    {proposal.status === 'Pending' ? 'Chờ xử lý' :
                     proposal.status === 'Accepted' ? 'Đã chấp nhận' :
                     proposal.status === 'Rejected' ? 'Đã từ chối' :
                     proposal.status === 'Cancelled' ? 'Đã hủy' : proposal.status}
                </span>
            </div>

            {/* Sender / Pickup info */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-4">
                {/* Sender */}
                <div className="bg-surface-container-low rounded-lg p-3">
                    <div className="flex items-center gap-1.5 text-[11px] text-on-surface-variant font-bold uppercase tracking-wider mb-1.5">
                        <span className="material-symbols-outlined text-[14px]">person</span>
                        Người gửi
                    </div>
                    <p className="text-body-md font-semibold text-on-surface">{proposal.senderName}</p>
                    <p className="text-body-sm text-on-surface-variant">{proposal.senderPhone}</p>
                </div>

                {/* Receiver */}
                <div className="bg-surface-container-low rounded-lg p-3">
                    <div className="flex items-center gap-1.5 text-[11px] text-on-surface-variant font-bold uppercase tracking-wider mb-1.5">
                        <span className="material-symbols-outlined text-[14px]">local_shipping</span>
                        Người nhận
                    </div>
                    <p className="text-body-md font-semibold text-on-surface">{proposal.receiverName ?? '—'}</p>
                    <p className="text-body-sm text-on-surface-variant">{proposal.receiverPhone ?? '—'}</p>
                </div>
            </div>

            {/* Pickup address */}
            <div className="mb-3">
                <div className="flex items-center gap-1.5 text-[11px] text-on-surface-variant font-bold uppercase tracking-wider mb-1">
                    <span className="material-symbols-outlined text-[14px]">location_on</span>
                    Địa chỉ lấy hàng
                </div>
                <p className="text-body-md text-on-surface">{proposal.pickupAddress}</p>
                {proposal.pickupNote && (
                    <p className="text-body-sm text-on-surface-variant mt-1 italic">Ghi chú: {proposal.pickupNote}</p>
                )}
            </div>

            {/* Delivery address */}
            <div className="mb-3">
                <div className="flex items-center gap-1.5 text-[11px] text-on-surface-variant font-bold uppercase tracking-wider mb-1">
                    <span className="material-symbols-outlined text-[14px]">flag</span>
                    Địa chỉ giao hàng
                </div>
                <p className="text-body-md text-on-surface">{proposal.deliveryAddress ?? '—'}</p>
            </div>

            {/* Weight / Volume */}
            <div className="flex gap-4 mb-4 text-body-sm">
                <div className="flex items-center gap-1">
                    <span className="material-symbols-outlined text-[14px] text-primary">scale</span>
                    <span className="font-semibold">{proposal.weightKg} kg</span>
                </div>
                <div className="flex items-center gap-1">
                    <span className="material-symbols-outlined text-[14px] text-emerald-600">square_foot</span>
                    <span className="font-semibold">{proposal.volumeCbm} m³</span>
                </div>
                {proposal.commodity && (
                    <div className="flex items-center gap-1 text-on-surface-variant">
                        <span className="material-symbols-outlined text-[14px]">category</span>
                        <span>{proposal.commodity}</span>
                    </div>
                )}
            </div>

            {/* Action buttons */}
            <div className="flex gap-3">
                <button
                    onClick={() => onAccept(proposal.proposalId)}
                    className="flex-1 flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl bg-primary text-on-primary font-bold text-label-lg hover:bg-primary/90 transition-all"
                >
                    <span className="material-symbols-outlined text-[18px]">check_circle</span>
                    Chấp nhận
                </button>
                <button
                    onClick={() => onReject(proposal.proposalId)}
                    className="flex-1 flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl border border-error text-error font-bold text-label-lg hover:bg-error/5 transition-all"
                >
                    <span className="material-symbols-outlined text-[18px]">cancel</span>
                    Từ chối
                </button>
            </div>
        </div>
    );
}
