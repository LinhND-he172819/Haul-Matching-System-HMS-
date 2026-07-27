import { useState, useEffect } from 'react';
import AppHeader from '../../components/AppHeader';
import RejectProposalDialog from '../../components/staff/proposals/RejectProposalDialog';
import Toast from '../../components/matching/Toast';
import {
  getStaffProposalDetail,
  approveProposal,
  rejectProposal,
  type StaffProposalDetail,
} from '../../api/staff/staffProposalApi';

/* ─── Constants ─────────────────────────────────────────────────── */

const STATUS_BADGE: Record<string, string> = {
  PendingReview: 'bg-amber-50 text-amber-700 border border-amber-200',
  Approved: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
  Confirmed: 'bg-purple-50 text-purple-700 border border-purple-200',
  Rejected: 'bg-rose-50 text-rose-700 border border-rose-200',
  Cancelled: 'bg-gray-100 text-gray-600 border border-gray-200',
  Expired: 'bg-gray-100 text-gray-500 border border-gray-200',
};

const STATUS_LABELS: Record<string, string> = {
  PendingReview: 'Chờ duyệt',
  Approved: 'Đã duyệt',
  Confirmed: 'Đã xác nhận',
  Rejected: 'Đã từ chối',
  Cancelled: 'Đã hủy',
  Expired: 'Đã hết hạn',
};

/* ─── Props ─────────────────────────────────────────────────────── */

type Props = {
  proposalId: string;
  onBack: () => void;
  onLogout: () => void;
  onCreateQuotation?: (proposalId: string) => void;
  onViewQuotation?: (quotationId: string) => void;
};

/* ─── Component ─────────────────────────────────────────────────── */

export default function StaffProposalDetailPage({
  proposalId,
  onBack,
  onLogout,
  onCreateQuotation,
  onViewQuotation,
}: Props) {
  const [detail, setDetail] = useState<StaffProposalDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  // Reject dialog
  const [rejectDialogOpen, setRejectDialogOpen] = useState(false);
  const [rejectLoading, setRejectLoading] = useState(false);

  // Approve loading
  const [approveLoading, setApproveLoading] = useState(false);

  const loadDetail = async () => {
    setLoading(true);
    try {
      const result = await getStaffProposalDetail(proposalId);
      setDetail(result);
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi tải dữ liệu', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadDetail();
  }, [proposalId]);

  const handleApprove = async () => {
    setApproveLoading(true);
    try {
      await approveProposal(proposalId);
      setToast({ message: 'Duyệt đề xuất thành công', type: 'success' });
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi duyệt đề xuất', type: 'error' });
    } finally {
      setApproveLoading(false);
    }
  };

  const handleRejectConfirm = async (reason: string) => {
    setRejectLoading(true);
    try {
      await rejectProposal(proposalId, reason);
      setToast({ message: 'Đã từ chối đề xuất', type: 'success' });
      setRejectDialogOpen(false);
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi từ chối đề xuất', type: 'error' });
    } finally {
      setRejectLoading(false);
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

  const navPages = [
    { label: 'Đề xuất', onClick: onBack },
  ];

  if (loading) {
    return (
      <div className="min-h-screen bg-surface">
        <AppHeader onLogout={onLogout} pages={navPages} />
        <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
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
        <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-16 text-center">
          <span className="material-symbols-outlined text-[48px] text-gray-300">error</span>
          <p className="text-title-lg font-semibold text-on-surface-variant mt-3">Không tìm thấy đề xuất</p>
          <button onClick={onBack} className="btn-primary mt-4">Quay lại</button>
        </div>
      </div>
    );
  }

  const { shipment, trip, tripCapacity, customer, quotations, audit } = detail;

  return (
    <div className="min-h-screen bg-surface">
      <AppHeader onLogout={onLogout} pages={navPages} />

      <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
        {/* Back + Header */}
        <button onClick={onBack} className="flex items-center gap-1 text-on-surface-variant hover:text-primary mb-4 transition-colors">
          <span className="material-symbols-outlined text-[20px]">arrow_back</span>
          <span className="text-label-md">Quay lại danh sách</span>
        </button>

        <div className="flex items-center justify-between mb-6">
          <div className="flex items-center gap-3">
            <div className="w-12 h-12 rounded-full bg-primary/10 flex items-center justify-center">
              <span className="material-symbols-outlined text-primary text-xl">description</span>
            </div>
            <div>
              <h1 className="text-headline-lg font-bold text-on-surface">{shipment.shipmentCode}</h1>
              <p className="text-label-md text-on-surface-variant">
                {detail.code} • {formatDate(detail.createdAt)}
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
          {/* Left: Shipment Info */}
          <div className="lg:col-span-2 space-y-4">
            {/* Shipment Info */}
            <div className="bg-white rounded-2xl border border-outline-variant p-5">
              <h2 className="text-title-lg font-bold text-on-surface mb-4 flex items-center gap-2">
                <span className="material-symbols-outlined text-primary">inventory_2</span>
                Thông tin hàng hóa
              </h2>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <p className="text-label-sm text-on-surface-variant">Loại hàng</p>
                  <p className="text-body-md font-semibold">{shipment.commodity}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Khối lượng</p>
                  <p className="text-body-md font-semibold">{shipment.weightKg} kg</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Thể tích</p>
                  <p className="text-body-md font-semibold">{shipment.volumeCbm} m³</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Trạng thái</p>
                  <p className="text-body-md font-semibold">{shipment.status}</p>
                </div>
                {shipment.specialHandlingNote && (
                  <div className="col-span-2">
                    <p className="text-label-sm text-on-surface-variant">Ghi chú đặc biệt</p>
                    <p className="text-body-md">{shipment.specialHandlingNote}</p>
                  </div>
                )}
              </div>
            </div>

            {/* Receiver Info */}
            <div className="bg-white rounded-2xl border border-outline-variant p-5">
              <h2 className="text-title-lg font-bold text-on-surface mb-4 flex items-center gap-2">
                <span className="material-symbols-outlined text-primary">location_on</span>
                Thông tin giao hàng
              </h2>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <p className="text-label-sm text-on-surface-variant">Người nhận</p>
                  <p className="text-body-md font-semibold">{shipment.receiver}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Địa chỉ</p>
                  <p className="text-body-md">{shipment.deliveryAddress}</p>
                </div>
              </div>
            </div>

            {/* Trip Info */}
            <div className="bg-white rounded-2xl border border-outline-variant p-5">
              <h2 className="text-title-lg font-bold text-on-surface mb-4 flex items-center gap-2">
                <span className="material-symbols-outlined text-primary">route</span>
                Thông tin chuyến
              </h2>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <p className="text-label-sm text-on-surface-variant">Mã chuyến</p>
                  <p className="text-body-md font-semibold">{trip.tripCode || 'N/A'}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Chế độ nhận</p>
                  <p className="text-body-md font-semibold">{trip.pickupMode}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Điểm đi</p>
                  <p className="text-body-md">{trip.origin}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Điểm đến</p>
                  <p className="text-body-md">{trip.destination}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Thời gian khởi hành</p>
                  <p className="text-body-md">{formatDate(trip.departureTime)}</p>
                </div>
              </div>
            </div>

            {/* Capacity Info */}
            <div className="bg-white rounded-2xl border border-outline-variant p-5">
              <h2 className="text-title-lg font-bold text-on-surface mb-4 flex items-center gap-2">
                <span className="material-symbols-outlined text-primary">scale</span>
                Sức chứa chuyến
              </h2>
              <div className="space-y-3">
                <div>
                  <div className="flex items-center justify-between text-label-sm text-on-surface-variant mb-1">
                    <span>Trọng lượng: {tripCapacity.currentWeight} / {tripCapacity.maxWeight} kg</span>
                    <span>{tripCapacity.remainingWeight} kg còn lại</span>
                  </div>
                  <div className="w-full h-2 bg-gray-100 rounded-full overflow-hidden">
                    <div
                      className="h-full bg-primary rounded-full transition-all"
                      style={{ width: `${Math.min(100, (tripCapacity.currentWeight / (tripCapacity.maxWeight || 1)) * 100)}%` }}
                    />
                  </div>
                </div>
                <div>
                  <div className="flex items-center justify-between text-label-sm text-on-surface-variant mb-1">
                    <span>Thể tích: {tripCapacity.currentVolume} / {tripCapacity.maxVolume} m³</span>
                    <span>{tripCapacity.remainingVolume} m³ còn lại</span>
                  </div>
                  <div className="w-full h-2 bg-gray-100 rounded-full overflow-hidden">
                    <div
                      className="h-full bg-secondary-container rounded-full transition-all"
                      style={{ width: `${Math.min(100, (tripCapacity.currentVolume / (tripCapacity.maxVolume || 1)) * 100)}%` }}
                    />
                  </div>
                </div>
              </div>
            </div>

            {/* Customer Info */}
            <div className="bg-white rounded-2xl border border-outline-variant p-5">
              <h2 className="text-title-lg font-bold text-on-surface mb-4 flex items-center gap-2">
                <span className="material-symbols-outlined text-primary">person</span>
                Thông tin khách hàng
              </h2>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <p className="text-label-sm text-on-surface-variant">Họ tên</p>
                  <p className="text-body-md font-semibold">{customer.fullName}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Số điện thoại</p>
                  <p className="text-body-md">{customer.phone}</p>
                </div>
                <div>
                  <p className="text-label-sm text-on-surface-variant">Email</p>
                  <p className="text-body-md">{customer.email}</p>
                </div>
              </div>
            </div>
          </div>

          {/* Right sidebar: Quotation + Actions + Audit */}
          <div className="space-y-4">
            {/* Actions */}
            <div className="bg-white rounded-2xl border border-outline-variant p-5">
              <h2 className="text-title-lg font-bold text-on-surface mb-4">Hành động</h2>
              <div className="space-y-2">
                {detail.status === 'PendingReview' && (
                  <>
                    <button
                      onClick={handleApprove}
                      disabled={approveLoading}
                      className="w-full bg-emerald-600 text-white px-4 py-3 rounded-xl text-label-md font-semibold
                                 hover:bg-emerald-700 disabled:opacity-50 transition-colors flex items-center justify-center gap-2"
                    >
                      {approveLoading ? (
                        <>
                          <span className="material-symbols-outlined animate-spin text-[16px]">sync</span>
                          Đang xử lý...
                        </>
                      ) : (
                        <>
                          <span className="material-symbols-outlined text-[18px]">check_circle</span>
                          Duyệt đề xuất
                        </>
                      )}
                    </button>
                    <button
                      onClick={() => setRejectDialogOpen(true)}
                      className="w-full bg-rose-50 text-rose-600 px-4 py-3 rounded-xl text-label-md font-semibold
                                 hover:bg-rose-100 transition-colors flex items-center justify-center gap-2"
                    >
                      <span className="material-symbols-outlined text-[18px]">cancel</span>
                      Từ chối
                    </button>
                  </>
                )}
                {detail.status === 'Approved' && onCreateQuotation && (
                  <button
                    onClick={() => onCreateQuotation(proposalId)}
                    className="w-full bg-primary text-on-primary px-4 py-3 rounded-xl text-label-md font-semibold
                               hover:bg-primary-700 transition-colors flex items-center justify-center gap-2"
                  >
                    <span className="material-symbols-outlined text-[18px]">request_quote</span>
                    Tạo báo giá
                  </button>
                )}
              </div>
            </div>

            {/* Quotation History */}
            <div className="bg-white rounded-2xl border border-outline-variant p-5">
              <h2 className="text-title-lg font-bold text-on-surface mb-4 flex items-center gap-2">
                <span className="material-symbols-outlined text-primary">receipt_long</span>
                Lịch sử báo giá
              </h2>
              {quotations.length === 0 ? (
                <p className="text-body-md text-on-surface-variant text-center py-4">
                  Chưa có báo giá
                </p>
              ) : (
                <div className="space-y-3">
                  {quotations.map((q) => (
                    <div
                      key={q.id}
                      className="border border-outline-variant rounded-xl p-3 hover:bg-surface-container-low cursor-pointer transition-colors"
                      onClick={() => onViewQuotation?.(q.id)}
                    >
                      <div className="flex items-center justify-between mb-1">
                        <p className="text-body-md font-semibold">{q.quotationCode}</p>
                        <span
                          className={`inline-flex items-center px-2 py-0.5 rounded-full text-label-sm font-semibold ${
                            STATUS_BADGE[q.status] || 'bg-gray-100 text-gray-600'
                          }`}
                        >
                          {STATUS_LABELS[q.status] || q.status}
                        </span>
                      </div>
                      <p className="text-label-sm text-on-surface-variant">
                        {formatCurrency(q.shippingFee)} • Cọc: {formatCurrency(q.depositAmount)}
                      </p>
                      <p className="text-label-sm text-on-surface-variant">{formatDate(q.sentAt || q.createdAt)}</p>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Audit Timeline */}
            <div className="bg-white rounded-2xl border border-outline-variant p-5">
              <h2 className="text-title-lg font-bold text-on-surface mb-4 flex items-center gap-2">
                <span className="material-symbols-outlined text-primary">history</span>
                Lịch sử thay đổi
              </h2>
              {audit.length === 0 ? (
                <p className="text-body-md text-on-surface-variant text-center py-4">
                  Chưa có lịch sử
                </p>
              ) : (
                <div className="space-y-3">
                  {audit.map((a, i) => (
                    <div key={i} className="flex gap-3">
                      <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
                        <span className="material-symbols-outlined text-primary text-[14px]">
                          {a.action.includes('Approve') ? 'check_circle' : a.action.includes('Reject') ? 'cancel' : 'edit'}
                        </span>
                      </div>
                      <div>
                        <p className="text-body-md font-semibold text-on-surface">{a.action}</p>
                        <p className="text-label-sm text-on-surface-variant">{a.details}</p>
                        <p className="text-label-sm text-on-surface-variant">
                          {a.performedByName} • {formatDate(a.occurredAt)}
                        </p>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Reject Dialog */}
      <RejectProposalDialog
        open={rejectDialogOpen}
        onClose={() => setRejectDialogOpen(false)}
        onConfirm={handleRejectConfirm}
        loading={rejectLoading}
        proposalCode={detail.code}
      />

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
