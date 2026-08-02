import { useState } from 'react';

interface RejectProposalDialogProps {
  open: boolean;
  onClose: () => void;
  onConfirm: (reason: string) => void;
  loading?: boolean;
  proposalCode?: string;
}

export default function RejectProposalDialog({
  open,
  onClose,
  onConfirm,
  loading = false,
  proposalCode,
}: RejectProposalDialogProps) {
  const [reason, setReason] = useState('');

  const handleConfirm = () => {
    const trimmed = reason.trim();
    if (!trimmed || trimmed.length < 2) return;
    onConfirm(trimmed);
  };

  const handleClose = () => {
    if (loading) return;
    setReason('');
    onClose();
  };

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" onClick={handleClose}>
      <div
        className="bg-white rounded-2xl shadow-xl w-full max-w-md mx-4 p-6"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center gap-3 mb-4">
          <div className="w-10 h-10 rounded-full bg-rose-50 flex items-center justify-center">
            <span className="material-symbols-outlined text-rose-600 text-xl">cancel</span>
          </div>
          <div>
            <h3 className="text-headline-sm font-bold text-on-surface">Từ chối đề xuất</h3>
            {proposalCode && (
              <p className="text-label-sm text-on-surface-variant">{proposalCode}</p>
            )}
          </div>
        </div>

        {/* Reason */}
        <div className="mb-5">
          <label className="block text-label-md font-semibold text-on-surface mb-1.5">
            Lý do từ chối <span className="text-rose-500">*</span>
          </label>
          <textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Nhập lý do từ chối đề xuất này..."
            rows={3}
            maxLength={500}
            className="w-full border border-outline-variant rounded-xl px-4 py-3 text-body-md
                       placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/30
                       focus:border-primary transition-colors resize-none"
          />
          <p className="text-label-sm text-on-surface-variant mt-1">
            {reason.trim().length}/500 ký tự
          </p>
        </div>

        {/* Actions */}
        <div className="flex gap-3 justify-end">
          <button
            onClick={handleClose}
            disabled={loading}
            className="btn-ghost"
          >
            Hủy
          </button>
          <button
            onClick={handleConfirm}
            disabled={loading || reason.trim().length < 2}
            className="bg-rose-600 text-white px-5 py-2.5 rounded-xl font-semibold text-sm
                       hover:bg-rose-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {loading ? (
              <span className="flex items-center gap-2">
                <span className="material-symbols-outlined animate-spin text-[16px]">sync</span>
                Đang xử lý...
              </span>
            ) : (
              'Từ chối'
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
