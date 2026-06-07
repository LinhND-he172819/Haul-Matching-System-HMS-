
interface Props {
    onAcceptAll: () => void;
    onRejectAll: () => void;
    onAcceptSelected: () => void;
    onRejectSelected: () => void;
    selectedCount: number;
}

export default function ActionsBar({
    onAcceptAll,
    onRejectAll,
    onAcceptSelected,
    onRejectSelected,
    selectedCount
}: Props) {
    return (
        <div className="mt-4 space-y-3">
            <button
                className="w-full bg-emerald-700 text-white py-3 rounded-xl font-semibold disabled:opacity-50 disabled:cursor-not-allowed"
                onClick={onAcceptSelected}
                disabled={selectedCount === 0}
            >
                Xác Nhận Đã Chọn ({selectedCount})
            </button>

            <div className="grid grid-cols-2 gap-3">
                <button
                    className="border border-emerald-300 text-emerald-700 py-3 rounded-xl font-semibold hover:bg-emerald-50"
                    onClick={onAcceptAll}
                >
                    Xác Nhận Tất Cả
                </button>

                <button
                    className="border border-rose-300 text-rose-600 py-3 rounded-xl font-semibold hover:bg-rose-50"
                    onClick={onRejectAll}
                >
                    Từ Chối Tất Cả
                </button>
            </div>

            <button
                className="w-full border border-rose-300 text-rose-600 py-3 rounded-xl font-semibold hover:bg-rose-50 disabled:opacity-50 disabled:cursor-not-allowed"
                onClick={onRejectSelected}
                disabled={selectedCount === 0}
            >
                Từ Chối Đã Chọn ({selectedCount})
            </button>
        </div>
    );
}