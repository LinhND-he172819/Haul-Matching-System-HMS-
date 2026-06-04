import React from 'react';

interface Props {
    onAcceptAll: () => void;
    onRejectAll: () => void;
    onAcceptSelected: () => void;
    onRejectSelected: () => void;
    selectedCount: number;
}

export default function ActionsBar({ onAcceptAll, onRejectAll, onAcceptSelected, onRejectSelected, selectedCount }: Props) {
    return (
        <div className="mt-4 space-y-3">
            <button className="w-full bg-emerald-700 text-white py-3 rounded-xl font-semibold" onClick={onAcceptSelected} disabled={selectedCount === 0}>
                <span className="material-symbols-outlined text-sm align-middle mr-2">check_circle</span>
                Xác Nhận Nhận Hàng ({selectedCount})
            </button>
            <div className="grid grid-cols-2 gap-3">
                <button className="btn-ghost" onClick={onRejectAll}>Từ Chối Tất Cả</button>
                <button className="btn-outline text-rose-600 border-rose-200" onClick={onRejectSelected} disabled={selectedCount === 0}>
                    <span className="material-symbols-outlined text-sm align-middle mr-2">error</span>
                    Báo Sự Cố
                </button>
            </div>
        </div>
    );
}
