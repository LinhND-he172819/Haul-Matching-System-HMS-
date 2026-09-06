import { useState, useEffect } from 'react';
import AppHeader from '../../components/AppHeader';
import QuotationForm from '../../components/staff/quotations/QuotationForm';
import Toast from '../../components/matching/Toast';
import {
  createQuotation,
} from '../../api/staff/staffQuotationApi';
import {
  getStaffProposalDetail,
  type StaffProposalDetail,
} from '../../api/staff/staffProposalApi';

/* ─── Props ─────────────────────────────────────────────────────── */

type Props = {
  proposalId: string;
  onBack: () => void;
  onLogout: () => void;
  onCreated?: (quotationId: string) => void;
};

/* ─── Component ─────────────────────────────────────────────────── */

export default function StaffCreateQuotationPage({
  proposalId,
  onBack,
  onLogout,
  onCreated,
}: Props) {
  const [proposal, setProposal] = useState<StaffProposalDetail | null>(null);
  const [loadingProposal, setLoadingProposal] = useState(true);
  const [creating, setCreating] = useState(false);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  useEffect(() => {
    const load = async () => {
      setLoadingProposal(true);
      try {
        const result = await getStaffProposalDetail(proposalId);
        setProposal(result);
      } catch (err: any) {
        setToast({ message: err.message || 'Lỗi tải đề xuất', type: 'error' });
      } finally {
        setLoadingProposal(false);
      }
    };
    load();
  }, [proposalId]);

  const handleSubmit = async (data: {
    shippingFee: number;
    depositAmount: number;
    currency: string;
    expiresAt: string;
  }) => {
    setCreating(true);
    try {
      const quotation = await createQuotation(proposalId, data);
      setToast({ message: 'Tạo báo giá thành công', type: 'success' });
      if (onCreated) {
        onCreated(quotation.id);
      } else {
        onBack();
      }
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi tạo báo giá', type: 'error' });
    } finally {
      setCreating(false);
    }
  };

  const navPages = [
    { label: 'Đề xuất', onClick: onBack },
  ];

  if (loadingProposal) {
    return (
      <div className="min-h-screen bg-surface">
        <AppHeader onLogout={onLogout} pages={navPages} />
        <div className="max-w-3xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
          <div className="space-y-4">
            {[1, 2].map((i) => (
              <div key={i} className="h-40 bg-white rounded-2xl animate-pulse border border-outline-variant" />
            ))}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-surface">
      <AppHeader onLogout={onLogout} pages={navPages} />

      <div className="max-w-3xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
        {/* Back */}
        <button onClick={onBack} className="flex items-center gap-1 text-on-surface-variant hover:text-primary mb-4 transition-colors">
          <span className="material-symbols-outlined text-[20px]">arrow_back</span>
          <span className="text-label-md">Quay lại</span>
        </button>

        <h1 className="text-headline-lg font-bold text-on-surface mb-2 flex items-center gap-3">
          <span className="material-symbols-outlined text-primary">request_quote</span>
          Tạo báo giá mới
        </h1>

        {/* Proposal Summary */}
        {proposal && (
          <div className="bg-white rounded-2xl border border-outline-variant p-5 mb-6">
            <h2 className="text-title-md font-bold text-on-surface mb-3">Thông tin đề xuất</h2>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <p className="text-label-sm text-on-surface-variant">Mã lô hàng</p>
                <p className="text-body-md font-semibold">{proposal.shipment.shipmentCode}</p>
              </div>
              <div>
                <p className="text-label-sm text-on-surface-variant">Loại hàng</p>
                <p className="text-body-md">{proposal.shipment.commodity || 'Chưa cập nhật'}</p>
              </div>
              <div>
                <p className="text-label-sm text-on-surface-variant">Khối lượng</p>
                <p className="text-body-md">{proposal.shipment.weightKg} kg</p>
              </div>
              <div>
                <p className="text-label-sm text-on-surface-variant">Thể tích</p>
                <p className="text-body-md">{proposal.shipment.volumeCbm} m³</p>
              </div>
              <div>
                <p className="text-label-sm text-on-surface-variant">Điểm đi</p>
                <p className="text-body-md">{proposal.pickupAddress || proposal.trip.origin || '—'}</p>
              </div>
              <div>
                <p className="text-label-sm text-on-surface-variant">Điểm đến</p>
                <p className="text-body-md">{proposal.shipment.deliveryAddress || proposal.trip.destination || '—'}</p>
              </div>
            </div>
          </div>
        )}

        {/* Quotation Form */}
        <div className="bg-white rounded-2xl border border-outline-variant p-5">
          <h2 className="text-title-lg font-bold text-on-surface mb-4 flex items-center gap-2">
            <span className="material-symbols-outlined text-primary">edit</span>
            Thông tin báo giá
          </h2>
          <QuotationForm
            onSubmit={handleSubmit}
            onCancel={onBack}
            loading={creating}
            submitLabel="Tạo báo giá"
          />
        </div>
      </div>

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
