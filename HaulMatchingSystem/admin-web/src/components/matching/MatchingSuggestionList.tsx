import React from 'react';
import { ShipmentSuggestionDto } from '../../api/matchingApi';
import ShipmentSuggestionCard from './ShipmentSuggestionCard';

interface Props {
    shipments: ShipmentSuggestionDto[];
    selectedIds: Set<string>;
    onSelect: (id: string, sel: boolean) => void;
    onAccept: (id: string) => void;
    onReject: (id: string) => void;
}

export default function MatchingSuggestionList({ shipments, selectedIds, onSelect, onAccept, onReject }: Props) {
    if (!shipments || shipments.length === 0) return <div className="text-body-md text-on-surface-variant">Không có gợi ý.</div>;

    return (
        <div className="space-y-4">
            {shipments.map((s, idx) => (
                <ShipmentSuggestionCard
                    key={s.shipmentId}
                    shipment={s}
                    selected={selectedIds.has(s.shipmentId)}
                    onSelect={onSelect}
                    onAccept={onAccept}
                    onReject={onReject}
                    matchPercent={Math.max(78, 95 - idx * 6)}
                    distanceKm={Number((2.1 + idx * 2.4).toFixed(1))}
                />
            ))}
        </div>
    );
}
