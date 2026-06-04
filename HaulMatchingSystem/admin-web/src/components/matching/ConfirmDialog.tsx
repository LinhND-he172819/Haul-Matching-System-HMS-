import React from 'react';

interface Props {
    title: string;
    open: boolean;
    onConfirm: () => void;
    onCancel: () => void;
    children?: React.ReactNode;
}

export default function ConfirmDialog({ title, open, onConfirm, onCancel, children }: Props) {
    if (!open) return null;

    return (
        <div className="fixed inset-0 bg-slate-900/30 flex items-center justify-center z-50">
            <div className="soft-card p-6 w-[420px]">
                <h3 className="text-lg font-semibold mb-2">{title}</h3>
                <div className="text-sm text-slate-500 mb-4">{children}</div>
                <div className="flex justify-end gap-3">
                    <button className="btn-ghost" onClick={onCancel}>Hủy</button>
                    <button className="btn-primary" onClick={onConfirm}>Xác nhận</button>
                </div>
            </div>
        </div>
    );
}
