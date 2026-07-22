import { useState } from 'react';

interface Props {
    open: boolean;
    onConfirm: (reason: string) => void;
    onCancel: () => void;
}

export default function RejectProposalDialog({ open, onConfirm, onCancel }: Props) {
    const [reason, setReason] = useState('');

    if (!open) return null;

    function handleConfirm() {
        onConfirm(reason.trim());
        setReason('');
    }

    function handleCancel() {
        setReason('');
        onCancel();
    }

    return (
        <div className="fixed inset-0 bg-slate-900/30 flex items-center justify-center z-50">
            <div className="soft-card p-6 w-[420px]">
                <h3 className="text-lg font-semibold mb-2">Lý do từ chối</h3>
                <p className="text-sm text-slate-500 mb-4">
                    Vui lòng nhập lý do từ chối đề xuất này. Khách hàng sẽ nhận được thông báo.
                </p>
                <textarea
                    className="w-full border border-outline-variant rounded-lg p-3 text-sm resize-none focus:outline-primary h-24 mb-4"
                    placeholder="Ví dụ: Quá tải trọng, không đúng tuyến..."
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                />
                <div className="flex justify-end gap-3">
                    <button className="btn-ghost" onClick={handleCancel}>Hủy</button>
                    <button
                        className="btn-primary"
                        onClick={handleConfirm}
                        disabled={!reason.trim()}
                    >
                        Từ chối
                    </button>
                </div>
            </div>
        </div>
    );
}
