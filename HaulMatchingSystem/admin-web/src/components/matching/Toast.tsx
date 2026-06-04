import React, { useEffect } from 'react';

export type ToastType = 'success' | 'error' | 'info';

interface ToastProps {
    message: string;
    type?: ToastType;
    onClose: () => void;
    duration?: number;
}

export default function Toast({ message, type = 'info', onClose, duration = 2600 }: ToastProps) {
    useEffect(() => {
        const timer = setTimeout(() => onClose(), duration);
        return () => clearTimeout(timer);
    }, [duration, onClose]);

    const tone = type === 'success' ? 'bg-emerald-600' : type === 'error' ? 'bg-rose-600' : 'bg-slate-900';

    return (
        <div className={`text-white px-4 py-3 rounded-xl shadow-lg flex items-center gap-3 ${tone}`} role="status">
            <span className="material-symbols-outlined text-base">notifications</span>
            <span className="text-sm font-medium">{message}</span>
        </div>
    );
}
