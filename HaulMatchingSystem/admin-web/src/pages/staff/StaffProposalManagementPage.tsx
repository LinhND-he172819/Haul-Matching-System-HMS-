import { useState, useEffect, useCallback } from 'react';
import AppHeader from '../../components/AppHeader';
import RejectProposalDialog from '../../components/staff/proposals/RejectProposalDialog';
import Toast from '../../components/matching/Toast';
import {
  getStaffProposals,
  approveProposal,
  rejectProposal,
  type StaffProposalSummary,
  type PagedResult,
} from '../../api/staff/staffProposalApi';

/* ─── Constants ─────────────────────────────────────────────────── */

const STATUS_OPTIONS = [
  { key: '', label: 'Tất cả trạng thái' },
  { key: 'PendingReview', label: 'Chờ duyệt' },
  { key: 'Approved', label: 'Đã duyệt' },
  { key: 'Confirmed', label: 'Đã xác nhận' },
  { key: 'Rejected', label: 'Đã từ chối' },
  { key: 'Expired', label: 'Đã hết hạn' },
  { key: 'Cancelled', label: 'Đã hủy' },
];

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

const SOURCE_OPTIONS = [
  { key: '', label: 'Tất cả nguồn' },
  { key: 'Customer', label: 'Khách hàng' },
  { key: 'Driver', label: 'Tài xế' },
];

/* ─── Props ─────────────────────────────────────────────────────── */

type Props = {
  onLogout: () => void;
  onSelectProposal: (proposalId: string) => void;
  onCreateQuotation: (proposalId: string) => void;
  onViewQuotation: (quotationId: string) => void;
};

/* ─── Component ─────────────────────────────────────────────────── */

export default function StaffProposalManagementPage({
  onLogout,
  onSelectProposal,
  onCreateQuotation,
  onViewQuotation,
}: Props) {
  const [data, setData] = useState<PagedResult<StaffProposalSummary> | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState('');
  const [sourceFilter, setSourceFilter] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearch(searchInput);
      setPage(1);
    }, 400);
    return () => clearTimeout(timer);
  }, [searchInput]);

  // Reject dialog state
  const [rejectDialogOpen, setRejectDialogOpen] = useState(false);
  const [rejectProposalId, setRejectProposalId] = useState<string | null>(null);
  const [rejectProposalCode, setRejectProposalCode] = useState('');
  const [rejectLoading, setRejectLoading] = useState(false);

  // Approve loading
  const [approveLoadingId, setApproveLoadingId] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const result = await getStaffProposals({
        status: activeTab || undefined,
        proposalSource: sourceFilter || undefined,
        search: search || undefined,
        page,
        pageSize: 10,
      });
      setData(result);
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi tải dữ liệu', type: 'error' });
    } finally {
      setLoading(false);
    }
  }, [activeTab, sourceFilter, search, page]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const handleApprove = async (proposalId: string) => {
    setApproveLoadingId(proposalId);
    try {
      await approveProposal(proposalId);
      setToast({ message: 'Duyệt đề xuất thành công', type: 'success' });
      await loadData();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi duyệt đề xuất', type: 'error' });
    } finally {
      setApproveLoadingId(null);
    }
  };

  const handleRejectClick = (proposalId: string, proposalCode: string) => {
    setRejectProposalId(proposalId);
    setRejectProposalCode(proposalCode);
    setRejectDialogOpen(true);
  };

  const handleRejectConfirm = async (reason: string) => {
    if (!rejectProposalId) return;
    setRejectLoading(true);
    try {
      await rejectProposal(rejectProposalId, reason);
      setToast({ message: 'Đã từ chối đề xuất', type: 'success' });
      setRejectDialogOpen(false);
      setRejectProposalId(null);
      await loadData();
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
        })
      : '-';

  const navPages = [
    { label: 'Đề xuất', onClick: () => {}, active: true },
    { label: 'Báo giá', onClick: () => {} },
    { label: 'Thanh toán', onClick: () => {} },
  ];

  return (
    <div className="min-h-screen bg-surface">
      <AppHeader onLogout={onLogout} pages={navPages} />

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
        {/* Page Header */}
        <div className="mb-6">
          <h1 className="text-headline-lg font-bold text-on-surface flex items-center gap-3">
            <span className="material-symbols-outlined text-primary">gavel</span>
            Quản lý đề xuất
          </h1>
          <p className="text-body-md text-on-surface-variant mt-1">
            Duyệt và quản lý đề xuất từ khách hàng và tài xế
          </p>
        </div>

        {/* Filter Bar: Search + Dropdowns */}
        <div className="flex flex-col sm:flex-row gap-3 mb-6">
          {/* Search Input */}
          <div className="relative flex-1">
            <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant text-[20px]">
              search
            </span>
            <input
              type="text"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="Tìm mã đề xuất, mã lô, người gửi, người nhận..."
              className="w-full pl-10 pr-4 py-2.5 rounded-xl bg-white border border-outline-variant
                         text-body-md text-on-surface placeholder:text-on-surface-variant/60
                         focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary
                         transition-all"
            />
            {searchInput && (
              <button
                onClick={() => { setSearchInput(''); setSearch(''); setPage(1); }}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-on-surface-variant hover:text-on-surface"
              >
                <span className="material-symbols-outlined text-[18px]">close</span>
              </button>
            )}
          </div>

          {/* Status Dropdown */}
          <select
            value={activeTab}
            onChange={(e) => { setActiveTab(e.target.value); setPage(1); }}
            className="px-4 py-2.5 rounded-xl bg-white border border-outline-variant text-body-md text-on-surface
                       focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary
                       transition-all min-w-[180px] cursor-pointer appearance-auto"
          >
            {STATUS_OPTIONS.map((opt) => (
              <option key={opt.key} value={opt.key}>{opt.label}</option>
            ))}
          </select>

          {/* Source Dropdown */}
          <select
            value={sourceFilter}
            onChange={(e) => { setSourceFilter(e.target.value); setPage(1); }}
            className="px-4 py-2.5 rounded-xl bg-white border border-outline-variant text-body-md text-on-surface
                       focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary
                       transition-all min-w-[150px] cursor-pointer appearance-auto"
          >
            {SOURCE_OPTIONS.map((opt) => (
              <option key={opt.key} value={opt.key}>{opt.label}</option>
            ))}
          </select>
        </div>

        {/* Loading */}
        {loading && (
          <div className="space-y-4">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-48 bg-white rounded-2xl animate-pulse border border-outline-variant" />
            ))}
          </div>
        )}

        {/* Empty */}
        {!loading && data && data.items.length === 0 && (
          <div className="text-center py-16 bg-white rounded-2xl border border-outline-variant">
            <span className="material-symbols-outlined text-[48px] text-gray-300">inbox</span>
            <p className="text-title-lg font-semibold text-on-surface-variant mt-3">
              Không có đề xuất nào
            </p>
            <p className="text-body-md text-on-surface-variant mt-1">
              {search ? `Không tìm thấy kết quả cho "${search}"` : activeTab ? 'Thử chuyển trạng thái khác' : 'Chưa có đề xuất nào'}
            </p>
          </div>
        )}

        {/* Proposal Cards */}
        {!loading && data && data.items.length > 0 && (
          <>
            <div className="space-y-4">
              {data.items.map((p) => (
                <div
                  key={p.proposalId}
                  className="bg-white rounded-2xl border border-outline-variant p-5 hover:shadow-md transition-shadow"
                >
                  {/* Header */}
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-full bg-primary/10 flex items-center justify-center">
                        <span className="material-symbols-outlined text-primary text-xl">
                          {p.proposalSource === 'Driver' ? 'local_shipping' : 'description'}
                        </span>
                      </div>
                      <div>
                        <p className="text-title-lg font-bold text-on-surface flex items-center gap-2">
                          {p.shipmentCode}
                          {p.proposalSource === 'Driver' && (
                            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-label-xs font-semibold bg-blue-50 text-blue-700 border border-blue-200">
                              <span className="material-symbols-outlined text-[12px]">local_shipping</span>
                              Tài xế khai báo
                            </span>
                          )}
                        </p>
                        <p className="text-label-sm text-on-surface-variant">{p.code} • {formatDate(p.createdAt)}</p>
                      </div>
                    </div>
                    <span
                      className={`inline-flex items-center gap-1 px-3 py-1.5 rounded-full text-label-sm font-semibold ${
                        STATUS_BADGE[p.status] || 'bg-gray-100 text-gray-600'
                      }`}
                    >
                      {STATUS_LABELS[p.status] || p.status}
                    </span>
                  </div>

                  {/* Info Grid */}
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-4">
                    {/* Cargo */}
                    <div className="bg-surface-container-low rounded-xl p-3">
                      <p className="text-label-sm text-on-surface-variant mb-1">
                        <span className="material-symbols-outlined text-[14px] align-middle mr-1">inventory_2</span>
                        Hàng hóa
                      </p>
                      <p className="text-body-md font-semibold">{p.commodity}</p>
                      <p className="text-label-sm text-on-surface-variant">
                        {p.weightKg} kg • {p.volumeCbm} m³
                      </p>
                    </div>

                    {/* Sender & Receiver */}
                    <div className="bg-surface-container-low rounded-xl p-3">
                      <p className="text-label-sm text-on-surface-variant mb-1">
                        <span className="material-symbols-outlined text-[14px] align-middle mr-1">person</span>
                        Gửi → Nhận
                      </p>
                      <p className="text-body-md">{p.senderName} → {p.receiverName}</p>
                      <p className="text-label-sm text-on-surface-variant truncate">{p.deliveryAddress}</p>
                    </div>

                    {/* Trip / Driver */}
                    <div className="bg-surface-container-low rounded-xl p-3">
                      {p.proposalSource === 'Driver' ? (
                        <>
                          <p className="text-label-sm text-on-surface-variant mb-1">
                            <span className="material-symbols-outlined text-[14px] align-middle mr-1">local_shipping</span>
                            Tài xế khai báo
                          </p>
                          <p className="text-body-md font-semibold">{p.driverName || 'N/A'}</p>
                          <p className="text-label-sm text-on-surface-variant">
                            {p.driverPhone && `${p.driverPhone} • `}{p.vehiclePlate || 'N/A'}
                          </p>
                        </>
                      ) : (
                        <>
                          <p className="text-label-sm text-on-surface-variant mb-1">
                            <span className="material-symbols-outlined text-[14px] align-middle mr-1">route</span>
                            Chuyến
                          </p>
                          <p className="text-body-md font-semibold">{p.tripCode || 'N/A'}</p>
                          <p className="text-label-sm text-on-surface-variant truncate">{p.origin} → {p.destination}</p>
                        </>
                      )}
                    </div>
                  </div>

                  {/* Capacity bar */}
                  <div className="mb-4">
                    <div className="flex items-center justify-between text-label-sm text-on-surface-variant mb-1">
                      <span>Tải trọng còn lại: {p.remainingWeight} kg</span>
                      <span>Thể tích còn lại: {p.remainingVolume} m³</span>
                    </div>
                    <div className="w-full h-1.5 bg-gray-100 rounded-full overflow-hidden">
                      <div
                        className="h-full bg-primary rounded-full transition-all"
                        style={{
                          width: `${Math.min(100, (p.currentWeight / (p.maxWeight || 1)) * 100)}%`,
                        }}
                      />
                    </div>
                  </div>

                  {/* Actions */}
                  <div className="flex items-center gap-2 pt-3 border-t border-outline-variant">
                    <button
                      onClick={() => onSelectProposal(p.proposalId)}
                      className="btn-ghost text-label-md flex items-center gap-1.5"
                    >
                      <span className="material-symbols-outlined text-[18px]">visibility</span>
                      Xem chi tiết
                    </button>

                    {p.status === 'PendingReview' && (
                      <>
                        <button
                          onClick={() => handleApprove(p.proposalId)}
                          disabled={approveLoadingId === p.proposalId}
                          className="bg-emerald-600 text-white px-4 py-2 rounded-xl text-label-md font-semibold
                                     hover:bg-emerald-700 disabled:opacity-50 transition-colors flex items-center gap-1.5"
                        >
                          {approveLoadingId === p.proposalId ? (
                            <>
                              <span className="material-symbols-outlined animate-spin text-[16px]">sync</span>
                              Đang xử lý...
                            </>
                          ) : (
                            <>
                              <span className="material-symbols-outlined text-[16px]">check_circle</span>
                              Duyệt
                            </>
                          )}
                        </button>
                        <button
                          onClick={() => handleRejectClick(p.proposalId, p.shipmentCode)}
                          className="bg-rose-50 text-rose-600 px-4 py-2 rounded-xl text-label-md font-semibold
                                     hover:bg-rose-100 transition-colors flex items-center gap-1.5"
                        >
                          <span className="material-symbols-outlined text-[16px]">cancel</span>
                          Từ chối
                        </button>
                      </>
                    )}

                    {p.status === 'Approved' && !p.quotationId && (
                      <button
                        onClick={() => onCreateQuotation(p.proposalId)}
                        className="bg-primary text-on-primary px-4 py-2 rounded-xl text-label-md font-semibold
                                   hover:bg-primary-700 transition-colors flex items-center gap-1.5"
                      >
                        <span className="material-symbols-outlined text-[16px]">request_quote</span>
                        Tạo báo giá
                      </button>
                    )}

                    {p.quotationId && (
                      <button
                        onClick={() => onViewQuotation(p.quotationId!)}
                        className="bg-primary/10 text-primary px-4 py-2 rounded-xl text-label-md font-semibold
                                   hover:bg-primary/20 transition-colors flex items-center gap-1.5"
                      >
                        <span className="material-symbols-outlined text-[16px]">request_quote</span>
                        Xem báo giá
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>

            {/* Pagination */}
            {data.totalPages > 1 && (
              <div className="flex items-center justify-center gap-3 mt-6">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="btn-ghost disabled:opacity-50"
                >
                  ← Trước
                </button>
                <span className="text-body-md text-on-surface-variant">
                  Trang {page} / {data.totalPages}
                </span>
                <button
                  onClick={() => setPage((p) => Math.min(data.totalPages, p + 1))}
                  disabled={page === data.totalPages}
                  className="btn-ghost disabled:opacity-50"
                >
                  Sau →
                </button>
              </div>
            )}
          </>
        )}
      </div>

      {/* Reject Dialog */}
      <RejectProposalDialog
        open={rejectDialogOpen}
        onClose={() => {
          setRejectDialogOpen(false);
          setRejectProposalId(null);
        }}
        onConfirm={handleRejectConfirm}
        loading={rejectLoading}
        proposalCode={rejectProposalCode}
      />

      {/* Toast */}
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
