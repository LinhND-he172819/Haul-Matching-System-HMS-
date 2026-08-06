import { useEffect, useState } from 'react';
import {
  getExternalShipmentDetail,
  type ExternalShipmentDetail,
} from '../api/driverExternalShipmentApi';
import Toast from '../components/matching/Toast';
import AppHeader from '../components/AppHeader';

/* ─── Status Badge Mapping ────────────────────────────────────────── */

const PROPOSAL_STATUS_BADGE: Record<string, string> = {
  PendingReview: 'bg-amber-50 text-amber-700 border border-amber-200',
  Approved: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
  Rejected: 'bg-red-50 text-red-700 border border-red-200',
  Cancelled: 'bg-gray-100 text-gray-600 border border-gray-200',
};

const PROPOSAL_STATUS_LABELS: Record<string, string> = {
  PendingReview: 'Chờ xét duyệt',
  Approved: 'Đã chấp nhận',
  Rejected: 'Bị từ chối',
  Cancelled: 'Đã hủy',
};

/* ─── Props ───────────────────────────────────────────────────────── */

type Props = {
  proposalId: string;
  onLogout: () => void;
  onNavigate?: (page: string) => void;
  onBack?: () => void;
};

/* ─── Component ───────────────────────────────────────────────────── */

export default function DriverExternalShipmentDetail({
  proposalId,
  onLogout,
  onNavigate,
  onBack,
}: Props) {
  const [detail, setDetail] = useState<ExternalShipmentDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  useEffect(() => {
    loadDetail();
  }, [proposalId]);

  const loadDetail = async () => {
    setLoading(true);
    try {
      const result = await getExternalShipmentDetail(proposalId);
      setDetail(result);
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi tải chi tiết', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

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

  const formatCurrency = (v?: number) =>
    v != null
      ? new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(v)
      : '-';

  const TimelineStep = ({
    label,
    timestamp,
    note,
    isActive,
    isCompleted,
  }: {
    label: string;
    timestamp?: string;
    note?: string;
    isActive: boolean;
    isCompleted: boolean;
  }) => (
    <div className="flex gap-4">
      <div className="flex flex-col items-center">
        <div
          className={`w-3 h-3 rounded-full mt-1.5 ${
            isCompleted
              ? 'bg-emerald-500'
              : isActive
              ? 'bg-primary ring-4 ring-primary/20'
              : 'bg-outline-variant'
          }`}
        />
        <div className="w-0.5 flex-1 bg-outline-variant/50 min-h-[24px]" />
      </div>
      <div className="pb-6">
        <p
          className={`text-body-md font-medium ${
            isActive ? 'text-primary' : isCompleted ? 'text-on-surface' : 'text-on-surface-variant'
          }`}
        >
          {label}
        </p>
        {timestamp && (
          <p className="text-body-sm text-on-surface-variant mt-0.5">{formatDate(timestamp)}</p>
        )}
        {note && <p className="text-body-sm text-on-surface-variant/70 mt-0.5 italic">{note}</p>}
      </div>
    </div>
  );

  return (
    <div className="min-h-screen bg-surface text-on-surface">
      <AppHeader
        onLogout={onLogout}
        pages={[
          { label: 'Chuyến đi', onClick: () => onNavigate?.('driver-trips') },
          { label: 'Đơn ngoài hệ thống', onClick: () => onNavigate?.('driver-external-history') },
          { label: 'Chi tiết', onClick: () => {}, active: true },
        ]}
      />

      <div className="max-w-3xl mx-auto px-4 py-6">
        {/* Back button */}
        <button
          onClick={onBack ?? (() => onNavigate?.('driver-external-history'))}
          className="flex items-center gap-1 text-primary hover:text-primary-dark transition-colors mb-4"
        >
          <span className="material-symbols-outlined text-[20px]">arrow_back</span>
          <span className="text-label-lg font-medium">Quay lại</span>
        </button>

        {loading ? (
          <div className="flex flex-col items-center justify-center py-20 gap-3">
            <span className="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
            <p className="text-body-md text-on-surface-variant">Đang tải chi tiết...</p>
          </div>
        ) : !detail ? (
          <div className="flex flex-col items-center justify-center py-20 gap-4">
            <span className="material-symbols-outlined text-[56px] text-on-surface-variant/30">error</span>
            <p className="text-body-lg text-on-surface-variant">Không tìm thấy đơn hàng</p>
          </div>
        ) : (
          <div className="space-y-5">
            {/* Header Card */}
            <div className="bg-surface-container-low rounded-2xl p-5">
              <div className="flex items-start justify-between mb-3">
                <div>
                  <h1 className="text-title-lg font-bold text-on-surface">
                    {detail.shipmentCode ?? detail.proposalCode ?? `#${detail.proposalId.slice(0, 8)}`}
                  </h1>
                  <p className="text-body-sm text-on-surface-variant mt-1">{detail.category}</p>
                </div>
                <span
                  className={`inline-flex px-3 py-1 rounded-full text-label-sm font-medium ${
                    PROPOSAL_STATUS_BADGE[detail.proposalStatus] ?? 'bg-gray-100 text-gray-600'
                  }`}
                >
                  {PROPOSAL_STATUS_LABELS[detail.proposalStatus] ?? detail.proposalStatus}
                </span>
              </div>

              <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-body-sm">
                <div>
                  <span className="text-on-surface-variant">Khối lượng</span>
                  <p className="font-semibold text-on-surface text-title-sm">{detail.weightKg.toFixed(1)} kg</p>
                </div>
                <div>
                  <span className="text-on-surface-variant">Thể tích</span>
                  <p className="font-semibold text-on-surface text-title-sm">{detail.volumeCbm.toFixed(2)} m³</p>
                </div>
                <div>
                  <span className="text-on-surface-variant">Số kiện</span>
                  <p className="font-semibold text-on-surface text-title-sm">{detail.quantity}</p>
                </div>
                <div>
                  <span className="text-on-surface-variant">COD</span>
                  <p className="font-semibold text-on-surface text-title-sm">
                    {detail.codRequired ? formatCurrency(detail.codAmount) : 'Không'}
                  </p>
                </div>
              </div>
            </div>

            {/* Sender & Receiver */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="bg-surface-container-low rounded-2xl p-5">
                <h3 className="text-title-sm font-semibold text-on-surface mb-3 flex items-center gap-2">
                  <span className="material-symbols-outlined text-[20px] text-primary">person</span>
                  Người gửi
                </h3>
                <div className="space-y-2 text-body-sm">
                  <div className="flex justify-between">
                    <span className="text-on-surface-variant">Họ tên</span>
                    <span className="font-medium text-on-surface">{detail.senderName}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-on-surface-variant">SĐT</span>
                    <span className="font-medium text-on-surface">{detail.senderPhone}</span>
                  </div>
                  <div>
                    <span className="text-on-surface-variant">Địa chỉ nhận</span>
                    <p className="font-medium text-on-surface mt-0.5">{detail.pickupAddress}</p>
                  </div>
                </div>
              </div>

              <div className="bg-surface-container-low rounded-2xl p-5">
                <h3 className="text-title-sm font-semibold text-on-surface mb-3 flex items-center gap-2">
                  <span className="material-symbols-outlined text-[20px] text-primary">local_shipping</span>
                  Người nhận
                </h3>
                <div className="space-y-2 text-body-sm">
                  <div className="flex justify-between">
                    <span className="text-on-surface-variant">Họ tên</span>
                    <span className="font-medium text-on-surface">{detail.receiverName}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-on-surface-variant">SĐT</span>
                    <span className="font-medium text-on-surface">{detail.receiverPhone}</span>
                  </div>
                  <div>
                    <span className="text-on-surface-variant">Địa chỉ giao</span>
                    <p className="font-medium text-on-surface mt-0.5">{detail.destAddress}</p>
                  </div>
                </div>
              </div>
            </div>

            {/* Description & Note */}
            {(detail.description || detail.note) && (
              <div className="bg-surface-container-low rounded-2xl p-5">
                <h3 className="text-title-sm font-semibold text-on-surface mb-3 flex items-center gap-2">
                  <span className="material-symbols-outlined text-[20px] text-primary">notes</span>
                  Ghi chú
                </h3>
                <div className="space-y-2 text-body-sm">
                  {detail.description && (
                    <div>
                      <span className="text-on-surface-variant">Mô tả:</span>
                      <p className="text-on-surface mt-0.5">{detail.description}</p>
                    </div>
                  )}
                  {detail.note && (
                    <div>
                      <span className="text-on-surface-variant">Ghi chú đặc biệt:</span>
                      <p className="text-on-surface mt-0.5 italic">{detail.note}</p>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Pricing Info (if quotation available) */}
            {(detail.shippingFee != null || detail.depositAmount != null) && (
              <div className="bg-surface-container-low rounded-2xl p-5">
                <h3 className="text-title-sm font-semibold text-on-surface mb-3 flex items-center gap-2">
                  <span className="material-symbols-outlined text-[20px] text-primary">receipt_long</span>
                  Báo giá
                </h3>
                <div className="grid grid-cols-2 gap-4 text-body-sm">
                  <div>
                    <span className="text-on-surface-variant">Phí vận chuyển</span>
                    <p className="font-semibold text-on-surface text-title-sm">{formatCurrency(detail.shippingFee)}</p>
                  </div>
                  <div>
                    <span className="text-on-surface-variant">Tiền đặt cọc</span>
                    <p className="font-semibold text-on-surface text-title-sm">{formatCurrency(detail.depositAmount)}</p>
                  </div>
                </div>
              </div>
            )}

            {/* Trip Info */}
            {detail.tripCode && (
              <div className="bg-surface-container-low rounded-2xl p-5">
                <h3 className="text-title-sm font-semibold text-on-surface mb-3 flex items-center gap-2">
                  <span className="material-symbols-outlined text-[20px] text-primary">route</span>
                  Chuyến đi
                </h3>
                <div className="grid grid-cols-2 gap-4 text-body-sm">
                  <div>
                    <span className="text-on-surface-variant">Mã chuyến</span>
                    <p className="font-medium text-on-surface">{detail.tripCode}</p>
                  </div>
                  <div>
                    <span className="text-on-surface-variant">Tuyến đường</span>
                    <p className="font-medium text-on-surface">
                      {detail.origin ?? 'N/A'} → {detail.destination ?? 'N/A'}
                    </p>
                  </div>
                </div>
              </div>
            )}

            {/* Timeline */}
            {detail.timeline && detail.timeline.length > 0 && (
              <div className="bg-surface-container-low rounded-2xl p-5">
                <h3 className="text-title-sm font-semibold text-on-surface mb-4 flex items-center gap-2">
                  <span className="material-symbols-outlined text-[20px] text-primary">timeline</span>
                  Lịch sử
                </h3>
                <div>
                  {detail.timeline.map((entry, idx) => {
                    const isCurrentEntry = entry.timestamp == null;
                    const isCompletedEntry = !isCurrentEntry;
                    return (
                      <TimelineStep
                        key={idx}
                        label={entry.label}
                        timestamp={entry.timestamp}
                        note={entry.note}
                        isActive={isCurrentEntry}
                        isCompleted={isCompletedEntry}
                      />
                    );
                  })}
                </div>
              </div>
            )}

            {/* Created At */}
            <div className="text-center text-body-sm text-on-surface-variant pt-2 pb-4">
              Tạo lúc: {formatDate(detail.createdAt)}
            </div>
          </div>
        )}
      </div>

      {toast && (
        <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />
      )}
    </div>
  );
}
