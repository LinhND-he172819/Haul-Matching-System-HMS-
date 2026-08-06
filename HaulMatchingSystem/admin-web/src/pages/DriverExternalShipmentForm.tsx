import { useState } from 'react';
import {
  createExternalShipment,
  type CreateExternalShipmentRequest,
  type CreateExternalShipmentResponse,
} from '../api/driverExternalShipmentApi';
import Toast from '../components/matching/Toast';
import AppHeader from '../components/AppHeader';

/* ─── Props ───────────────────────────────────────────────────────── */

type Props = {
  onLogout: () => void;
  onNavigate?: (page: string) => void;
  onCreated?: (shipmentId: string, proposalId: string) => void;
};

/* ─── Category Options ────────────────────────────────────────────── */

const CATEGORIES = [
  { value: 'Hàng khô', label: 'Hàng khô' },
  { value: 'Hàng đông lạnh', label: 'Hàng đông lạnh' },
  { value: 'Hàng dễ vỡ', label: 'Hàng dễ vỡ' },
  { value: 'Hàng nặng/cồng kềnh', label: 'Hàng nặng/cồng kềnh' },
  { value: 'Hàng hóa chất', label: 'Hàng hóa chất' },
  { value: 'Hàng điện tử', label: 'Hàng điện tử' },
  { value: 'Thực phẩm', label: 'Thực phẩm' },
  { value: 'Khác', label: 'Khác' },
];

/* ─── Component ───────────────────────────────────────────────────── */

export default function DriverExternalShipmentForm({ onLogout, onNavigate, onCreated }: Props) {
  const [step, setStep] = useState(1);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<CreateExternalShipmentResponse | null>(null);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  const [form, setForm] = useState<CreateExternalShipmentRequest>({
    senderName: '',
    senderPhone: '',
    pickupAddress: '',
    receiverName: '',
    receiverPhone: '',
    destAddress: '',
    category: '',
    description: '',
    weightKg: 0,
    volumeCbm: 0,
    quantity: 1,
    codRequired: false,
    note: '',
  });

  const update = <K extends keyof CreateExternalShipmentRequest>(
    key: K,
    value: CreateExternalShipmentRequest[K]
  ) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  /* ─── Step 1: Sender & Pickup ─────────────────────────────────────── */

  const validateStep1 = (): boolean => {
    if (!form.senderName.trim()) {
      alert('Vui lòng nhập tên người gửi.');
      return false;
    }
    if (!form.senderPhone.trim()) {
      alert('Vui lòng nhập số điện thoại người gửi.');
      return false;
    }
    if (!form.pickupAddress.trim()) {
      alert('Vui lòng nhập địa chỉ nhận hàng.');
      return false;
    }
    return true;
  };

  /* ─── Step 2: Receiver & Delivery ─────────────────────────────────── */

  const validateStep2 = (): boolean => {
    if (!form.receiverName.trim()) {
      alert('Vui lòng nhập tên người nhận.');
      return false;
    }
    if (!form.receiverPhone.trim()) {
      alert('Vui lòng nhập số điện thoại người nhận.');
      return false;
    }
    if (!form.destAddress.trim()) {
      alert('Vui lòng nhập địa chỉ giao hàng.');
      return false;
    }
    return true;
  };

  /* ─── Step 3: Cargo Info ──────────────────────────────────────────── */

  const validateStep3 = (): boolean => {
    if (!form.category.trim()) {
      alert('Vui lòng chọn loại hàng.');
      return false;
    }
    if (form.weightKg <= 0) {
      alert('Cân nặng phải lớn hơn 0.');
      return false;
    }
    if (form.volumeCbm <= 0) {
      alert('Thể tích phải lớn hơn 0.');
      return false;
    }
    return true;
  };

  /* ─── Submit ──────────────────────────────────────────────────────── */

  const submit = async () => {
    if (!validateStep3()) return;

    setLoading(true);
    try {
      const res = await createExternalShipment(form);
      setResult(res);
      setToast({ message: 'Tạo đơn hàng thành công!', type: 'success' });
      onCreated?.(res.shipmentId, res.proposalId);
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi khi tạo đơn hàng.', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  /* ─── Render Steps ──────────────────────────────────────────────── */

  const renderStep1 = () => (
    <div className="space-y-5">
      <h3 className="text-title-md font-semibold text-on-surface flex items-center gap-2">
        <span className="material-symbols-outlined text-primary">person</span>
        Thông tin người gửi
      </h3>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <label className="block text-label-md text-on-surface-variant mb-1.5">Tên người gửi *</label>
          <input
            type="text"
            value={form.senderName}
            onChange={(e) => update('senderName', e.target.value)}
            placeholder="Nguyễn Văn A"
            className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md placeholder:text-on-surface-variant/40 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all"
          />
        </div>
        <div>
          <label className="block text-label-md text-on-surface-variant mb-1.5">SĐT người gửi *</label>
          <input
            type="tel"
            value={form.senderPhone}
            onChange={(e) => update('senderPhone', e.target.value)}
            placeholder="0901234567"
            className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md placeholder:text-on-surface-variant/40 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all"
          />
        </div>
      </div>

      <div>
        <label className="block text-label-md text-on-surface-variant mb-1.5">Địa chỉ nhận hàng *</label>
        <input
          type="text"
          value={form.pickupAddress}
          onChange={(e) => update('pickupAddress', e.target.value)}
          placeholder="123 Đường ABC, Quận 1, TP.HCM"
          className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md placeholder:text-on-surface-variant/40 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all"
        />
      </div>
    </div>
  );

  const renderStep2 = () => (
    <div className="space-y-5">
      <h3 className="text-title-md font-semibold text-on-surface flex items-center gap-2">
        <span className="material-symbols-outlined text-primary">local_shipping</span>
        Thông tin người nhận
      </h3>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div>
          <label className="block text-label-md text-on-surface-variant mb-1.5">Tên người nhận *</label>
          <input
            type="text"
            value={form.receiverName}
            onChange={(e) => update('receiverName', e.target.value)}
            placeholder="Trần Thị B"
            className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md placeholder:text-on-surface-variant/40 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all"
          />
        </div>
        <div>
          <label className="block text-label-md text-on-surface-variant mb-1.5">SĐT người nhận *</label>
          <input
            type="tel"
            value={form.receiverPhone}
            onChange={(e) => update('receiverPhone', e.target.value)}
            placeholder="0912345678"
            className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md placeholder:text-on-surface-variant/40 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all"
          />
        </div>
      </div>

      <div>
        <label className="block text-label-md text-on-surface-variant mb-1.5">Địa chỉ giao hàng *</label>
        <input
          type="text"
          value={form.destAddress}
          onChange={(e) => update('destAddress', e.target.value)}
          placeholder="456 Đường XYZ, Quận 7, TP.HCM"
          className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md placeholder:text-on-surface-variant/40 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all"
        />
      </div>
    </div>
  );

  const renderStep3 = () => (
    <div className="space-y-5">
      <h3 className="text-title-md font-semibold text-on-surface flex items-center gap-2">
        <span className="material-symbols-outlined text-primary">inventory_2</span>
        Thông tin hàng hóa
      </h3>

      <div>
        <label className="block text-label-md text-on-surface-variant mb-1.5">Loại hàng *</label>
        <select
          value={form.category}
          onChange={(e) => update('category', e.target.value)}
          className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all cursor-pointer"
        >
          <option value="">Chọn loại hàng</option>
          {CATEGORIES.map((cat) => (
            <option key={cat.value} value={cat.value}>{cat.label}</option>
          ))}
        </select>
      </div>

      <div>
        <label className="block text-label-md text-on-surface-variant mb-1.5">Mô tả hàng hóa</label>
        <textarea
          value={form.description ?? ''}
          onChange={(e) => update('description', e.target.value)}
          placeholder="Mô tả ngắn gọn về hàng hóa..."
          rows={3}
          className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md placeholder:text-on-surface-variant/40 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all resize-none"
        />
      </div>

      <div className="grid grid-cols-3 gap-4">
        <div>
          <label className="block text-label-md text-on-surface-variant mb-1.5">Khối lượng (kg) *</label>
          <input
            type="number"
            min="0.1"
            step="0.1"
            value={form.weightKg || ''}
            onChange={(e) => update('weightKg', parseFloat(e.target.value) || 0)}
            placeholder="0.0"
            className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md placeholder:text-on-surface-variant/40 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all"
          />
        </div>
        <div>
          <label className="block text-label-md text-on-surface-variant mb-1.5">Thể tích (m³) *</label>
          <input
            type="number"
            min="0.01"
            step="0.01"
            value={form.volumeCbm || ''}
            onChange={(e) => update('volumeCbm', parseFloat(e.target.value) || 0)}
            placeholder="0.00"
            className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md placeholder:text-on-surface-variant/40 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all"
          />
        </div>
        <div>
          <label className="block text-label-md text-on-surface-variant mb-1.5">Số kiện</label>
          <input
            type="number"
            min="1"
            step="1"
            value={form.quantity}
            onChange={(e) => update('quantity', parseInt(e.target.value) || 1)}
            className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all"
          />
        </div>
      </div>

      <div className="flex items-center gap-3">
        <label className="flex items-center gap-2 cursor-pointer">
          <input
            type="checkbox"
            checked={form.codRequired}
            onChange={(e) => update('codRequired', e.target.checked)}
            className="w-5 h-5 rounded accent-primary cursor-pointer"
          />
          <span className="text-body-md text-on-surface">Thu hộ (COD)</span>
        </label>
      </div>

      <div>
        <label className="block text-label-md text-on-surface-variant mb-1.5">Ghi chú</label>
        <textarea
          value={form.note ?? ''}
          onChange={(e) => update('note', e.target.value)}
          placeholder="Ghi chú đặc biệt (VD: hàng dễ vỡ, cần cẩn thận...)"
          rows={2}
          className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface-container-lowest text-on-surface text-body-md placeholder:text-on-surface-variant/40 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-all resize-none"
        />
      </div>
    </div>
  );

  /* ─── Success View ──────────────────────────────────────────────── */

  const renderSuccess = () => (
    <div className="text-center py-10 space-y-6">
      <div className="w-20 h-20 mx-auto rounded-full bg-emerald-100 flex items-center justify-center">
        <span className="material-symbols-outlined text-[40px] text-emerald-600">check_circle</span>
      </div>

      <div>
        <h2 className="text-title-lg font-semibold text-on-surface mb-2">Tạo đơn hàng thành công!</h2>
        <p className="text-body-md text-on-surface-variant">
          Đơn hàng đã được gửi đến Staff để xem xét và định giá.
        </p>
      </div>

      <div className="mx-auto max-w-sm bg-surface-container-low rounded-2xl p-5 text-left space-y-3">
        <div className="flex justify-between">
          <span className="text-label-md text-on-surface-variant">Mã đơn hàng</span>
          <span className="text-body-md font-medium text-on-surface">{result?.shipmentCode}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-label-md text-on-surface-variant">Trạng thái đề xuất</span>
          <span className="inline-flex px-2.5 py-0.5 rounded-full text-label-sm font-medium bg-amber-50 text-amber-700 border border-amber-200">
            Chờ xét duyệt
          </span>
        </div>
        <div className="flex justify-between">
          <span className="text-label-md text-on-surface-variant">Ngày tạo</span>
          <span className="text-body-md text-on-surface">
            {result?.createdAt
              ? new Date(result.createdAt).toLocaleDateString('vi-VN', {
                  day: '2-digit', month: '2-digit', year: 'numeric',
                  hour: '2-digit', minute: '2-digit',
                })
              : '-'}
          </span>
        </div>
      </div>

      <div className="flex items-center justify-center gap-3 pt-2">
        <button
          onClick={() => {
            setResult(null);
            setStep(1);
            setForm({
              senderName: '',
              senderPhone: '',
              pickupAddress: '',
              receiverName: '',
              receiverPhone: '',
              destAddress: '',
              category: '',
              description: '',
              weightKg: 0,
              volumeCbm: 0,
              quantity: 1,
              codRequired: false,
              note: '',
            });
          }}
          className="px-5 py-2.5 rounded-xl text-label-lg font-medium text-primary border border-primary hover:bg-primary/8 transition-colors"
        >
          Tạo đơn mới
        </button>
        <button
          onClick={() => onNavigate?.('driver-external-history')}
          className="px-5 py-2.5 rounded-xl text-label-lg font-medium text-white bg-primary hover:bg-primary-dark transition-colors"
        >
          Xem danh sách
        </button>
      </div>
    </div>
  );

  /* ─── Main Render ───────────────────────────────────────────────── */

  return (
    <div className="min-h-screen bg-surface text-on-surface">
      <AppHeader
        onLogout={onLogout}
        pages={[
          { label: 'Tạo đơn hàng ngoài hệ thống', onClick: () => {}, active: true },
        ]}
      />

      <div className="max-w-2xl mx-auto px-4 py-6">
        {result ? (
          renderSuccess()
        ) : (
          <>
            {/* Step Indicators */}
            <div className="flex items-center justify-center gap-2 mb-8">
              {[1, 2, 3].map((s) => (
                <div key={s} className="flex items-center gap-2">
                  <div
                    className={`w-8 h-8 rounded-full flex items-center justify-center text-label-sm font-semibold transition-all ${
                      step >= s
                        ? 'bg-primary text-white'
                        : 'bg-surface-container-high text-on-surface-variant'
                    }`}
                  >
                    {step > s ? (
                      <span className="material-symbols-outlined text-[18px]">check</span>
                    ) : (
                      s
                    )}
                  </div>
                  {s < 3 && (
                    <div className={`w-10 h-0.5 ${step > s ? 'bg-primary' : 'bg-outline-variant'}`} />
                  )}
                </div>
              ))}
            </div>

            <div className="bg-surface-container-low rounded-2xl p-6 shadow-sm">
              {step === 1 && renderStep1()}
              {step === 2 && renderStep2()}
              {step === 3 && renderStep3()}

              {/* Navigation Buttons */}
              <div className="flex items-center justify-between mt-8 pt-5 border-t border-outline-variant">
                <button
                  onClick={() => setStep((prev) => Math.max(1, prev - 1))}
                  disabled={step === 1}
                  className="px-5 py-2.5 rounded-xl text-label-lg font-medium text-on-surface-variant hover:bg-surface-container-high disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  Quay lại
                </button>

                {step < 3 ? (
                  <button
                    onClick={() => {
                      if (step === 1 && validateStep1()) setStep(2);
                      else if (step === 2 && validateStep2()) setStep(3);
                    }}
                    className="px-6 py-2.5 rounded-xl text-label-lg font-medium text-white bg-primary hover:bg-primary-dark transition-colors flex items-center gap-2"
                  >
                    Tiếp tục
                    <span className="material-symbols-outlined text-[18px]">arrow_forward</span>
                  </button>
                ) : (
                  <button
                    onClick={submit}
                    disabled={loading}
                    className="px-6 py-2.5 rounded-xl text-label-lg font-medium text-white bg-primary hover:bg-primary-dark disabled:opacity-60 disabled:cursor-not-allowed transition-colors flex items-center gap-2"
                  >
                    {loading ? (
                      <>
                        <span className="material-symbols-outlined animate-spin text-[18px]">sync</span>
                        Đang gửi...
                      </>
                    ) : (
                      <>
                        <span className="material-symbols-outlined text-[18px]">send</span>
                        Gửi đề xuất
                      </>
                    )}
                  </button>
                )}
              </div>
            </div>
          </>
        )}
      </div>

      {toast && (
        <Toast
          message={toast.message}
          type={toast.type}
          onClose={() => setToast(null)}
        />
      )}
    </div>
  );
}
