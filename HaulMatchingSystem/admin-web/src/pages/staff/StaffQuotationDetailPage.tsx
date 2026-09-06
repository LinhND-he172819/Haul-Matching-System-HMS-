import { useState, useEffect, useCallback } from 'react';
import AppHeader from '../../components/AppHeader';
import QuotationForm from '../../components/staff/quotations/QuotationForm';
import Toast from '../../components/matching/Toast';
import {
  getStaffQuotationDetail,
  updateQuotation,
  sendQuotation,
  cancelQuotation,
  type QuotationResponseDto,
} from '../../api/staff/staffQuotationApi';

/* ─── Constants ─────────────────────────────────────────────────── */

const STATUS_BADGE: Record<string, string> = {
  Draft: 'bg-gray-100 text-gray-600 border border-gray-200',
  Sent: 'bg-amber-50 text-amber-700 border border-amber-200',
  Accepted: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
  Expired: 'bg-gray-100 text-gray-500 border border-gray-200',
  Cancelled: 'bg-rose-50 text-rose-700 border border-rose-200',
};

const STATUS_LABELS: Record<string, string> = {
  Draft: 'Bản nháp',
  Sent: 'Đã gửi',
  Accepted: 'Đã chấp nhận',
  Expired: 'Đã hết hạn',
  Cancelled: 'Đã hủy',
};

/* ─── Props ─────────────────────────────────────────────────────── */

type Props = {
  quotationId: string;
  onBack: () => void;
  onLogout: () => void;
};

/* ─── Component ─────────────────────────────────────────────────── */

export default function StaffQuotationDetailPage({
  quotationId,
  onBack,
  onLogout,
}: Props) {
  const [detail, setDetail] = useState<QuotationResponseDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [sending, setSending] = useState(false);
  const [cancelling, setCancelling] = useState(false);

  // Send dialog
  const [showSendDialog, setShowSendDialog] = useState(false);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const result = await getStaffQuotationDetail(quotationId);
      setDetail(result);
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi tải dữ liệu', type: 'error' });
    } finally {
      setLoading(false);
    }
  }, [quotationId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleUpdate = async (data: {
    shippingFee: number;
    depositAmount: number;
    currency: string;
    expiresAt: string;
  }) => {
    setSaving(true);
    try {
      const updated = await updateQuotation(quotationId, data);
      setDetail(updated);
      setEditing(false);
      setToast({ message: 'Cập nhật báo giá thành công', type: 'success' });
    } catch (err: any) {
      const msg = err.message || '';
      if (msg.includes('409') || msg.includes('Conflict')) {
        setToast({ message: 'Báo giá đã được cập nhật bởi người khác. Vui lòng tải lại dữ liệu.', type: 'error' });
        await loadData();
      } else {
        setToast({ message: msg || 'Lỗi cập nhật báo giá', type: 'error' });
      }
    } finally {
      setSaving(false);
    }
  };

  const handleSend = async () => {
    setSending(true);
    try {
      await sendQuotation(quotationId);
      setToast({ message: 'Gửi báo giá thành công', type: 'success' });
      setShowSendDialog(false);
      await loadData();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi gửi báo giá', type: 'error' });
    } finally {
      setSending(false);
    }
  };

  const handleCancel = async () => {
    setCancelling(true);
    try {
      await cancelQuotation(quotationId);
      setToast({ message: 'Hủy báo giá thành công', type: 'success' });
      await loadData();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi hủy báo giá', type: 'error' });
    } finally {
      setCancelling(false);
    }
  };

  const formatCurrency = (n: number) =>
    n.toLocaleString('vi-VN', { style: 'currency', currency: 'VND' });

  const formatDate = (s?: string) =>
    s
      ? new Date(s).toLocaleDateString('vi-VN', {
          day: '2-digit',
          month: '2-digit',
          year: 'numeric',
          hour: '2-digit',
          minute: '2-digit',
        })
      : '-';

  const isDraft = detail?.status === 'Draft';
  const isSent = detail?.status === 'Sent';
  const canEdit = isDraft;
  const canSend = isDraft;
  const canCancel = isDraft || isSent;

  const navPages = [
    { label: 'Báo giá', onClick: onBack },
  ];

  if (loading) {
    return (
      <div className="min-h-screen bg-surface">
        <AppHeader onLogout={onLogout} pages={navPages} />
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
          <div className="space-y-4">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-40 bg-white rounded-2xl animate-pulse border border-outline-variant" />
            ))}
          </div>
        </div>
      </div>
    );
  }

  if (!detail) {
    return (
      <div className="min-h-screen bg-surface">
        <AppHeader onLogout={onLogout} pages={navPages} />
        <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-16 text-center">
          <span className="material-symbols-outlined text-[48px] text-gray-300">error</span>
          <p className="text-title-lg font-semibold text-on-surface-variant mt-3">Không tìm thấy báo giá</p>
          <button onClick={onBack} className="btn-primary mt-4">Quay lại</button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-surface">
      <AppHeader onLogout={onLogout} pages={navPages} />

      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
        {/* Back */}
        <button onClick={onBack} className="flex items-center gap-1 text-on-surface-variant hover:text-primary mb-4 transition-colors">
          <span className="material-symbols-outlined text-[20px]">arrow_back</span>
          <span className="text-label-md">Quay lại danh sách</span>
        </button>

        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <div className="flex items-center gap-3">
            <div className="w-12 h-12 rounded-full bg-primary/10 flex items-center justify-center">
              <span className="material-symbols-outlined text-primary text-xl">receipt_long</span>
            </div>
            <div>
              <h1 className="text-headline-lg font-bold text-on-surface">{detail.quotationCode}</h1>
              <p className="text-label-md text-on-surface-variant">
                {formatDate(detail.quotedAt || detail.createdAt)}
              </p>
            </div>
          </div>
          <span
            className={`inline-flex items-center gap-1 px-4 py-2 rounded-full text-label-md font-semibold ${
              STATUS_BADGE[detail.status] || 'bg-gray-100 text-gray-600'
            }`}
          >
            {STATUS_LABELS[detail.status] || detail.status}
          </span>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
          {/* Main Content */}
          <div className="lg:col-span-2 space-y-4">
            {/* Quotation Details */}
            <div className="bg-white rounded-2xl border border-outline-variant p-5">
              <h2 className="text-title-lg font-bold text-on-surface mb-4 flex items-center gap-2">
                <span className="material-symbols-outlined text-primary">receipt_long</span>
                Chi tiết báo giá
              </h2>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <p className="text-label-sm text-on-surface-variant">Mã báo giá</p>
                  <p className="text-body-md font-semibold">{detail.quotationCode}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Mã đề xuất</p>
                  <p className="text-body-md font-semibold">{detail.proposalId}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Phí vận chuyển</p>
                  <p className="text-body-md font-bold text-primary">{formatCurrency(detail.shippingFee)}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Tiền đặt cọc</p>
                  <p className="text-body-md font-semibold">{formatCurrency(detail.depositAmount)}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Số tiền còn lại</p>
                  <p className="text-body-md font-semibold text-primary">
                    {formatCurrency(detail.shippingFee - detail.depositAmount)}
                  </p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Đơn vị tiền tệ</p>
                  <p className="text-body-md font-semibold">{detail.currency}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Người báo giá</p>
                  <p className="text-body-md">{detail.quotedByName || detail.quotedBy}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Ngày báo giá</p>
                  <p className="text-body-md">{formatDate(detail.quotedAt || detail.createdAt)}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Ngày gửi</p>
                  <p className="text-body-md">{formatDate(detail.sentAt)}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Thời hạn</p>
                  <p className="text-body-md">{formatDate(detail.expiresAt)}</p>
                </div>
                {detail.acceptedAt && (
                  <div>
                    <p className="text-label-sm text-on-surface-variant">Ngày chấp nhận</p>
                    <p className="text-body-md">{formatDate(detail.acceptedAt)}</p>
                  </div>
                )}
              </div>
            </div>

            {/* Edit Form (only for Draft) */}
            {canEdit && editing && (
              <div className="bg-white rounded-2xl border border-outline-variant p-5">
                <h2 className="text-title-lg font-bold text-on-surface mb-4 flex items-center gap-2">
                  <span className="material-symbols-outlined text-primary">edit</span>
                  Chỉnh sửa báo giá
                </h2>
                <QuotationForm
                  initialData={{
                    shippingFee: detail.shippingFee,
                    depositAmount: detail.depositAmount,
                    currency: detail.currency,
                    expiresAt: detail.expiresAt,
                  }}
                  onSubmit={handleUpdate}
                  onCancel={() => setEditing(false)}
                  loading={saving}
                  submitLabel="Cập nhật"
                />
              </div>
            )}
          </div>

          {/* Sidebar: Actions */}
          <div className="space-y-4">
            <div className="bg-white rounded-2xl border border-outline-variant p-5">
              <h2 className="text-title-lg font-bold text-on-surface mb-4">Hành động</h2>
              <div className="space-y-2">
                {canEdit && !editing && (
                  <button
                    onClick={() => setEditing(true)}
                    className="w-full bg-primary/10 text-primary px-4 py-3 rounded-xl text-label-md font-semibold
                               hover:bg-primary/20 transition-colors flex items-center justify-center gap-2"
                  >
                    <span className="material-symbols-outlined text-[18px]">edit</span>
                    Chỉnh sửa
                  </button>
                )}
                {canSend && (
                  <button
                    onClick={() => setShowSendDialog(true)}
                    className="w-full bg-primary text-on-primary px-4 py-3 rounded-xl text-label-md font-semibold
                               hover:bg-primary-700 transition-colors flex items-center justify-center gap-2"
                  >
                    <span className="material-symbols-outlined text-[18px]">send</span>
                    Gửi báo giá
                  </button>
                )}
                {canCancel && (
                  <button
                    onClick={handleCancel}
                    disabled={cancelling}
                    className="w-full bg-rose-50 text-rose-600 px-4 py-3 rounded-xl text-label-md font-semibold
                               hover:bg-rose-100 disabled:opacity-50 transition-colors flex items-center justify-center gap-2"
                  >
                    {cancelling ? (
                      <>
                        <span className="material-symbols-outlined animate-spin text-[16px]">sync</span>
                        Đang xử lý...
                      </>
                    ) : (
                      <>
                        <span className="material-symbols-outlined text-[18px]">cancel</span>
                        Hủy báo giá
                      </>
                    )}
                  </button>
                )}
                {!canEdit && !canSend && !canCancel && (
                  <p className="text-body-md text-on-surface-variant text-center py-4">
                    Không có hành động khả dụng
                  </p>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Send Confirm Dialog */}
      {showSendDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" onClick={() => !sending && setShowSendDialog(false)}>
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md mx-4 p-6" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-center gap-3 mb-4">
              <div className="w-10 h-10 rounded-full bg-primary/10 flex items-center justify-center">
                <span className="material-symbols-outlined text-primary text-xl">send</span>
              </div>
              <div>
                <h3 className="text-headline-sm font-bold text-on-surface">Xác nhận gửi báo giá</h3>
                <p className="text-label-sm text-on-surface-variant">{detail.quotationCode}</p>
              </div>
            </div>
            <div className="bg-surface-container-low rounded-xl p-4 mb-5 space-y-2">
              <div className="flex justify-between text-body-md">
                <span className="text-on-surface-variant">Tổng phí vận chuyển:</span>
                <span className="font-bold">{formatCurrency(detail.shippingFee)}</span>
              </div>
              <div className="flex justify-between text-body-md">
                <span className="text-on-surface-variant">Tiền đặt cọc:</span>
                <span className="font-semibold">{formatCurrency(detail.depositAmount)}</span>
              </div>
              <div className="flex justify-between text-body-md">
                <span className="text-on-surface-variant">Thời hạn:</span>
                <span className="font-semibold">{formatDate(detail.expiresAt)}</span>
              </div>
            </div>
            <div className="flex gap-3 justify-end">
              <button
                onClick={() => setShowSendDialog(false)}
                disabled={sending}
                className="btn-ghost"
              >
                Hủy
              </button>
              <button
                onClick={handleSend}
                disabled={sending}
                className="bg-primary text-on-primary px-5 py-2.5 rounded-xl font-semibold text-sm
                           hover:bg-primary-700 disabled:opacity-50 transition-colors"
              >
                {sending ? (
                  <span className="flex items-center gap-2">
                    <span className="material-symbols-outlined animate-spin text-[16px]">sync</span>
                    Đang gửi...
                  </span>
                ) : (
                  'Xác nhận gửi'
                )}
              </button>
            </div>
          </div>
        </div>
      )}

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
