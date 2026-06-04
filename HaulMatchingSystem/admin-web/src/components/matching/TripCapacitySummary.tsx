import React from 'react';

interface Props {
    currentWeight: number;
    currentVolume: number;
    remainingWeight: number;
    remainingVolume: number;
}

export default function TripCapacitySummary({ currentWeight, currentVolume, remainingWeight, remainingVolume }: Props) {
    const totalWeight = currentWeight + remainingWeight;
    const totalVolume = currentVolume + remainingVolume;
    const weightPercent = totalWeight ? Math.round((remainingWeight / totalWeight) * 100) : 0;
    const volumePercent = totalVolume ? Math.round((remainingVolume / totalVolume) * 100) : 0;

    return (
        <div className="soft-card p-4">
            <div className="flex items-center gap-2 text-sm font-semibold">
                <span className="material-symbols-outlined text-base text-[#1b39b7]">inventory_2</span>
                Tải Trọng Trống
            </div>

            <div className="mt-4 space-y-4">
                <div>
                    <div className="flex justify-between text-sm">
                        <span className="text-slate-600">Trọng lượng (kg)</span>
                        <span className="font-semibold text-[#1b39b7]">{remainingWeight}kg ({weightPercent}%)</span>
                    </div>
                    <div className="h-2 rounded-full bg-[#e2e8f8] mt-2 overflow-hidden">
                        <div className="h-2 rounded-full bg-[#1b39b7]" style={{ width: `${weightPercent}%` }} />
                    </div>
                </div>

                <div>
                    <div className="flex justify-between text-sm">
                        <span className="text-slate-600">Thể tích (m³)</span>
                        <span className="font-semibold text-emerald-700">{remainingVolume}m³ ({volumePercent}%)</span>
                    </div>
                    <div className="h-2 rounded-full bg-[#e2e8f8] mt-2 overflow-hidden">
                        <div className="h-2 rounded-full bg-emerald-600" style={{ width: `${volumePercent}%` }} />
                    </div>
                </div>
            </div>
        </div>
    );
}
