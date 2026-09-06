import { useState } from 'react';
import {
  createExternalShipment,
  type CreateExternalShipmentRequest,
  type CreateExternalShipmentResponse,
} from '../api/driverExternalShipmentApi';
import { geocodeAddress } from '../api/shipmentsApi';
import Toast from '../components/matching/Toast';
import AppHeader from '../components/AppHeader';
import ShipmentFormFields from '../components/ShipmentFormFields';
import type { ShipmentFormData } from '../components/ShipmentFormFields';

/* ─── Props ────────────────────────────────────────────────────────── */

type Props = {
  onLogout: () => void;
  onNavigate?: (page: string) => void;
  onCreated?: (shipmentId: string, proposalId: string) => void;
};

/* ─── Component ───────────────────────────────────────────────────── */

export default function DriverExternalShipmentForm({ onLogout, onNavigate, onCreated }: Props) {
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<CreateExternalShipmentResponse | null>(null);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  /* ── Geocode state ── */
  const [geocoding, setGeocoding] = useState(false);
  const [destResolved, setDestResolved] = useState(false);
  const [destResolvedName, setDestResolvedName] = useState('');
  const [_destLat, setDestLat] = useState('');
  const [_destLng, setDestLng] = useState('');

  /* ── Form data — identical shape to customer proposal form ── */
  const [form, setForm] = useState<ShipmentFormData>({
    senderName: '',
    senderPhone: '',
    pickupAddress: '',
    receiverName: '',
    receiverPhone: '',
    destAddress: '',
    cargoType: '',
    weightKg: '',
    volumeCbm: '',
  });

  /* ── Ghi chú (specialHandlingNote) ── */
  const [specialHandlingNote, setSpecialHandlingNote] = useState('');

  const handleFieldChange = <K extends keyof ShipmentFormData>(key: K, value: string | number) => {
    setForm((prev) => ({ ...prev, [key]: value }));
    if (key === 'destAddress') { setDestResolved(false); setDestResolvedName(''); }
  };

  /* ── Geocode destination ── */
  const handleGeocode = async () => {
    if (!form.destAddress.trim()) {
      alert('Vui lòng nhập địa chỉ giao hàng.');
      return;
    }
    try {
      setGeocoding(true);
      const result = await geocodeAddress(form.destAddress);
      setDestLat(String(result.lat));
      setDestLng(String(result.lng));
      setDestResolvedName(result.displayName);
      setDestResolved(true);
    } catch {
      alert('Không tìm thấy địa chỉ. Vui lòng nhập địa chỉ rõ hơn.');
    } finally {
      setGeocoding(false);
    }
  };

  /* ─── Validate ──────────────────────────────────────────────────── */

  const validate = (): boolean => {
    if (!form.senderName.trim()) { alert('Vui lòng nhập tên người gửi.'); return false; }
    if (!form.senderPhone.trim()) { alert('Vui lòng nhập số điện thoại người gửi.'); return false; }
    if (!form.pickupAddress.trim()) { alert('Vui lòng nhập địa chỉ nhận hàng.'); return false; }
    if (!form.receiverName.trim()) { alert('Vui lòng nhập tên người nhận.'); return false; }
    if (!form.receiverPhone.trim()) { alert('Vui lòng nhập số điện thoại người nhận.'); return false; }
    if (!form.destAddress.trim()) { alert('Vui lòng nhập địa chỉ giao hàng.'); return false; }
    if (!form.cargoType.trim()) { alert('Vui lòng nhập loại hàng.'); return false; }
    if (Number(form.weightKg) <= 0) { alert('Cân nặng phải lớn hơn 0.'); return false; }
    if (Number(form.volumeCbm) <= 0) { alert('Thể tích phải lớn hơn 0.'); return false; }
    return true;
  };

  /* ─── Submit ──────────────────────────────────────────────────────── */

  const submit = async () => {
    if (!validate()) return;

    setLoading(true);
    try {
      const payload: CreateExternalShipmentRequest = {
        senderName: form.senderName,
        senderPhone: form.senderPhone,
        pickupAddress: form.pickupAddress,
        receiverName: form.receiverName,
        receiverPhone: form.receiverPhone,
        destAddress: form.destAddress,
        category: form.cargoType,
        weightKg: Number(form.weightKg),
        volumeCbm: Number(form.volumeCbm),
        specialHandlingNote: specialHandlingNote || undefined,
      };

      const res = await createExternalShipment(payload);
      setResult(res);
      setToast({ message: 'Tạo đơn hàng thành công!', type: 'success' });
      onCreated?.(res.shipmentId, res.proposalId);
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi khi tạo đơn hàng.', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  const handleReset = () => {
    setResult(null);
    setForm({
      senderName: '',
      senderPhone: '',
      pickupAddress: '',
      receiverName: '',
      receiverPhone: '',
      destAddress: '',
      cargoType: '',
      weightKg: '',
      volumeCbm: '',
    });
    setSpecialHandlingNote('');
  };

  /* ─── Success View ──────────────────────────────────────────────── */

  if (result) {
    return (
      <div className="min-h-screen bg-[#f8f9ff]">
        <AppHeader
          onLogout={onLogout}
          pages={[{ label: 'Tạo đơn hàng ngoài hệ thống', onClick: () => {}, active: true }]}
        />
        <div className="max-w-2xl mx-auto px-4 py-10 space-y-6">
          <div className="text-center space-y-4">
            <div className="w-20 h-20 mx-auto rounded-full bg-emerald-100 flex items-center justify-center">
              <span className="material-symbols-outlined text-[40px] text-emerald-600">check_circle</span>
            </div>
            <h2 className="text-xl font-bold text-gray-800">Tạo đơn hàng thành công!</h2>
            <p className="text-sm text-gray-500">
              Đơn hàng đã được gửi đến Staff để xem xét và định giá.
            </p>
          </div>

          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5 space-y-3">
            <div className="flex justify-between">
              <span className="text-sm text-gray-500">Mã đơn hàng</span>
              <span className="text-sm font-medium text-gray-800">{result.shipmentCode}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-sm text-gray-500">Trạng thái đề xuất</span>
              <span className="inline-flex px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-50 text-amber-700 border border-amber-200">
                Chờ xét duyệt
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-sm text-gray-500">Ngày tạo</span>
              <span className="text-sm text-gray-800">
                {result.createdAt
                  ? new Date(result.createdAt).toLocaleDateString('vi-VN', {
                      day: '2-digit', month: '2-digit', year: 'numeric',
                      hour: '2-digit', minute: '2-digit',
                    })
                  : '-'}
              </span>
            </div>
          </div>

          <div className="flex items-center justify-center gap-3">
            <button
              onClick={handleReset}
              className="px-5 py-2.5 rounded-xl text-sm font-medium text-[#00288e] border border-[#00288e] hover:bg-blue-50 transition-colors"
            >
              Tạo đơn mới
            </button>
            <button
              onClick={() => onNavigate?.('driver-external-history')}
              className="px-5 py-2.5 rounded-xl text-sm font-medium text-white bg-[#00288e] hover:bg-[#001f6e] transition-colors"
            >
              Xem danh sách
            </button>
          </div>
        </div>

        {toast && (
          <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />
        )}
      </div>
    );
  }

  /* ─── Main Form ─────────────────────────────────────────────────── */

  return (
    <div className="min-h-screen bg-[#f8f9ff]">
      <AppHeader
        onLogout={onLogout}
        pages={[{ label: 'Tạo đơn hàng ngoài hệ thống', onClick: () => {}, active: true }]}
      />

      <div className="max-w-2xl mx-auto px-4 py-6 space-y-5 pb-24">
        {/* ── Page Title ── */}
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
          <div className="bg-gradient-to-r from-[#00288e] to-[#0041c4] px-5 py-4">
            <div className="flex items-center gap-2 mb-1">
              <span className="material-symbols-outlined text-white/80 text-[18px]">add_box</span>
              <span className="text-white/80 text-xs font-semibold uppercase tracking-wider">Đơn hàng ngoài hệ thống</span>
            </div>
            <h2 className="text-white font-bold text-lg">Tạo đơn hàng mới</h2>
          </div>
          <div className="px-5 py-3">
            <p className="text-sm text-gray-500">
              Nhập thông tin đơn hàng để gửi đến Staff xem xét và định giá.
            </p>
          </div>
        </div>

        {/* ── Shared Form Fields (same as customer proposal form) ── */}
        <ShipmentFormFields
          data={form}
          onChange={handleFieldChange}
          showSpecialHandlingNote
          specialHandlingNote={specialHandlingNote}
          onSpecialHandlingNoteChange={setSpecialHandlingNote}
          showGeocode
          geocoding={geocoding}
          destResolved={destResolved}
          onGeocode={handleGeocode}
          destResolvedName={destResolvedName}
        />

        {/* ── Submit Button (Fixed bottom bar) ── */}
        <div className="fixed bottom-0 left-0 right-0 bg-white/90 backdrop-blur-md border-t border-gray-100 px-4 py-3 z-30">
          <div className="max-w-2xl mx-auto flex gap-3">
            <button
              onClick={() => onNavigate?.('driver-external-history')}
              className="px-5 py-3 rounded-xl border border-gray-200 text-gray-600 text-sm font-bold hover:bg-gray-50 transition-all"
            >
              Hủy
            </button>
            <button
              onClick={submit}
              disabled={loading}
              className="flex-1 py-3 rounded-xl bg-[#00288e] text-white font-bold text-sm hover:bg-[#001f6e] transition-all disabled:opacity-50 flex items-center justify-center gap-2 shadow-md shadow-blue-200"
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
          </div>
        </div>
      </div>

      {toast && (
        <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />
      )}
    </div>
  );
}