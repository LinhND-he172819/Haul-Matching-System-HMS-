import { useEffect, useState } from 'react';
import {
  getDriverTripDetail,
  startTrip,
  completeTrip,
  confirmPickup,
  startTransport,
  confirmDelivery,
  reportIncident,
  type DriverTripDetail,
  type DriverShipmentListItem,
} from '../api/driverTripApi';
import { confirmCodPayment } from '../api/driverPaymentApi';
import Toast from '../components/matching/Toast';

/* ─── Status Badge Mapping ────────────────────────────────────────── */

const TRIP_STATUS_BADGE: Record<string, string> = {
  Scheduled: 'bg-gray-100 text-gray-600 border border-gray-200',
  Ready: 'bg-blue-50 text-blue-700 border border-blue-200',
  InProgress: 'bg-indigo-50 text-indigo-700 border border-indigo-200',
  Active: 'bg-indigo-50 text-indigo-700 border border-indigo-200',
  Completed: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
  Cancelled: 'bg-red-50 text-red-700 border border-red-200',
  Breakdown: 'bg-orange-50 text-orange-700 border border-orange-200',
};

const TRIP_STATUS_LABELS: Record<string, string> = {
  Scheduled: 'Đã lên lịch',
  Ready: 'Sẵn sàng',
  InProgress: 'Đang thực hiện',
  Active: 'Đang hoạt động',
  Completed: 'Hoàn tất',
  Cancelled: 'Đã hủy',
  Breakdown: 'Hỏng xe',
};

const SHIPMENT_STATUS_BADGE: Record<string, string> = {
  Matched: 'bg-purple-50 text-purple-700 border border-purple-200',
  In_Warehouse: 'bg-blue-50 text-blue-700 border border-blue-200',
  In_Transit: 'bg-indigo-50 text-indigo-700 border border-indigo-200',
  Delivered: 'bg-teal-50 text-teal-700 border border-teal-200',
  Completed: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
  Cancelled: 'bg-red-50 text-red-700 border border-red-200',
};

const SHIPMENT_STATUS_LABELS: Record<string, string> = {
  Matched: 'Đã ghép',
  In_Warehouse: 'Tại kho',
  In_Transit: 'Đang giao',
  Delivered: 'Đã giao',
  Completed: 'Hoàn tất',
  Cancelled: 'Đã hủy',
};

/* ─── Props ───────────────────────────────────────────────────────── */

type Props = {
  tripId: string;
  onBack: () => void;
  onLogout: () => void;
};

/* ─── Component ───────────────────────────────────────────────────── */

export default function DriverTripDetailPage({ tripId, onBack, onLogout }: Props) {
  const [detail, setDetail] = useState<DriverTripDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [selectedShipment, setSelectedShipment] = useState<DriverShipmentListItem | null>(null);

  // Dialog states
  const [showPickupDialog, setShowPickupDialog] = useState(false);
  const [showDeliveryDialog, setShowDeliveryDialog] = useState(false);
  const [showIncidentDialog, setShowIncidentDialog] = useState(false);
  const [showCodDialog, setShowCodDialog] = useState(false);
  const [pickupNote, setPickupNote] = useState('');
  const [deliveryNote, setDeliveryNote] = useState('');
  const [incidentType, setIncidentType] = useState('Delay');
  const [incidentDescription, setIncidentDescription] = useState('');
  const [incidentFiles, setIncidentFiles] = useState<File[]>([]);

  // Part 13: COD state
  const [codConfirming, setCodConfirming] = useState<string | null>(null);
  const [selectedCodPaymentId, setSelectedCodPaymentId] = useState<string | null>(null);

  const loadDetail = async () => {
    setLoading(true);
    try {
      const result = await getDriverTripDetail(tripId);
      setDetail(result);
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi tải dữ liệu', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadDetail();
  }, [tripId]);

  const handleAction = async (action: () => Promise<any>, successMsg: string) => {
    setActionLoading(true);
    try {
      await action();
      setToast({ message: successMsg, type: 'success' });
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi thực hiện thao tác', type: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleStartTrip = () =>
    handleAction(() => startTrip(tripId), 'Đã bắt đầu chuyến đi.');

  const handleCompleteTrip = () =>
    handleAction(() => completeTrip(tripId), 'Đã hoàn thành chuyến đi.');

  const handleConfirmPickup = async () => {
    if (!selectedShipment) return;
    setActionLoading(true);
    try {
      await confirmPickup(selectedShipment.id, pickupNote);
      setToast({ message: 'Đã xác nhận nhận hàng.', type: 'success' });
      setShowPickupDialog(false);
      setPickupNote('');
      setSelectedShipment(null);
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi', type: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleStartTransport = async () => {
    if (!selectedShipment) return;
    setActionLoading(true);
    try {
      await startTransport(selectedShipment.id);
      setToast({ message: 'Đã bắt đầu vận chuyển.', type: 'success' });
      setSelectedShipment(null);
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi', type: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleConfirmDelivery = async () => {
    if (!selectedShipment) return;
    setActionLoading(true);
    try {
      await confirmDelivery(selectedShipment.id, deliveryNote);
      setToast({ message: 'Đã xác nhận giao hàng thành công.', type: 'success' });
      setShowDeliveryDialog(false);
      setDeliveryNote('');
      setSelectedShipment(null);
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi', type: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleReportIncident = async () => {
    setActionLoading(true);
    try {
      await reportIncident(tripId, {
        shipmentId: selectedShipment?.id,
        incidentType,
        description: incidentDescription,
        files: incidentFiles.length > 0 ? incidentFiles : undefined,
      });
      setToast({ message: 'Đã báo cáo sự cố.', type: 'success' });
      setShowIncidentDialog(false);
      setIncidentDescription('');
      setSelectedShipment(null);
      setIncidentFiles([]);
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi', type: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleIncidentFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (!files) return;
    const maxSize = 5 * 1024 * 1024; // 5 MB
    const allowedExts = ['.jpg', '.jpeg', '.png', '.webp'];
    const validFiles: File[] = [];
    for (const file of Array.from(files)) {
      const ext = '.' + file.name.split('.').pop()?.toLowerCase();
      if (!allowedExts.includes(ext)) {
        setToast({ message: `File '${file.name}' không phải hình ảnh hợp lệ.`, type: 'error' });
        continue;
      }
      if (file.size > maxSize) {
        setToast({ message: `File '${file.name}' vượt quá 5 MB.`, type: 'error' });
        continue;
      }
      validFiles.push(file);
    }
    setIncidentFiles(prev => [...prev, ...validFiles].slice(0, 5));
    if (e.target) e.target.value = '';
  };

  const removeIncidentFile = (index: number) => {
    setIncidentFiles(prev => prev.filter((_, i) => i !== index));
  };

  // ─── Part 13: COD Confirmation ───
  const handleConfirmCod = async () => {
    if (!selectedCodPaymentId) return;
    setCodConfirming(selectedCodPaymentId);
    try {
      await confirmCodPayment(selectedCodPaymentId);
      setToast({ message: 'Xác nhận thanh toán COD thành công.', type: 'success' });
      setShowCodDialog(false);
      setSelectedCodPaymentId(null);
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi xác nhận COD', type: 'error' });
    } finally {
      setCodConfirming(null);
    }
  };

  const formatDate = (s?: string) =>
    s ? new Date(s).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '-';

  const capacityPercent = (current: number, max: number) =>
    max > 0 ? Math.round((current / max) * 100) : 0;

  if (loading) {
    return (
      <div className="min-h-screen bg-surface flex items-center justify-center">
        <div className="flex flex-col items-center gap-3">
          <span className="material-symbols-outlined animate-spin text-[36px] text-primary">sync</span>
          <p className="text-body-md text-on-surface-variant">Đang tải chi tiết chuyến...</p>
        </div>
      </div>
    );
  }

  if (!detail) {
    return (
      <div className="min-h-screen bg-surface flex items-center justify-center">
        <div className="text-center">
          <span className="material-symbols-outlined text-[48px] text-on-surface-variant/40">error</span>
          <p className="text-title-md text-on-surface mt-3">Không tìm thấy chuyến đi</p>
          <button onClick={onBack} className="mt-4 px-4 py-2 rounded-xl bg-primary text-on-primary text-label-md font-bold">Quay lại</button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-surface text-on-surface">
      {/* Header */}
      <div className="bg-surface-container-lowest border-b border-outline-variant sticky top-0 z-10">
        <div className="max-w-4xl mx-auto px-4 py-4 flex items-center gap-3">
          <button onClick={onBack} className="w-9 h-9 rounded-xl border border-outline-variant flex items-center justify-center hover:bg-surface-container-low transition-colors">
            <span className="material-symbols-outlined text-[20px]">arrow_back</span>
          </button>
          <div className="flex-1">
            <h1 className="text-headline-sm font-bold text-on-surface">{detail.tripCode}</h1>
            <p className="text-body-sm text-on-surface-variant">{detail.vehiclePlate} • {detail.originName} → {detail.destinationName}</p>
          </div>
          <span className={`text-label-sm font-medium px-2.5 py-1 rounded-lg ${TRIP_STATUS_BADGE[detail.status] || 'bg-gray-100 text-gray-600'}`}>
            {TRIP_STATUS_LABELS[detail.status] || detail.status}
          </span>
          <button onClick={onLogout} className="flex items-center gap-2 px-3 py-2 rounded-xl border border-outline-variant text-on-surface-variant hover:bg-surface-container-low transition-colors text-label-md">
            <span className="material-symbols-outlined text-[18px]">logout</span>
          </button>
        </div>
      </div>

      <div className="max-w-4xl mx-auto px-4 py-6 space-y-4">
        {/* Trip Actions */}
        <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
          <h3 className="text-label-lg font-bold text-on-surface mb-3 flex items-center gap-2">
            <span className="material-symbols-outlined text-[18px] text-primary">play_circle</span>
            Thao tác chuyến đi
          </h3>
          <div className="flex gap-3">
            {detail.allowedActions.canStart && (
              <button
                onClick={handleStartTrip}
                disabled={actionLoading}
                className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-primary text-on-primary hover:bg-primary/90 transition-colors text-label-md font-bold disabled:opacity-50"
              >
                <span className="material-symbols-outlined text-[18px]">play_arrow</span>
                Bắt đầu chuyến
              </button>
            )}
            {detail.allowedActions.canComplete && (
              <button
                onClick={handleCompleteTrip}
                disabled={actionLoading}
                className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-emerald-600 text-white hover:bg-emerald-700 transition-colors text-label-md font-bold disabled:opacity-50"
              >
                <span className="material-symbols-outlined text-[18px]">check_circle</span>
                Hoàn thành chuyến
              </button>
            )}
            <button
              onClick={() => setShowIncidentDialog(true)}
              className="flex items-center justify-center gap-2 px-4 py-3 rounded-xl border border-error text-error hover:bg-error/5 transition-colors text-label-md font-bold"
            >
              <span className="material-symbols-outlined text-[18px]">report</span>
              Báo cáo sự cố
            </button>
          </div>
        </div>

        {/* Capacity */}
        <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
          <h3 className="text-label-lg font-bold text-on-surface mb-3 flex items-center gap-2">
            <span className="material-symbols-outlined text-[18px] text-primary">scale</span>
            Sức chứa
          </h3>
          <div className="grid grid-cols-2 gap-6">
            <div>
              <div className="flex justify-between text-body-sm mb-1">
                <span className="text-on-surface-variant">Trọng lượng</span>
                <span className="text-on-surface font-bold">{capacityPercent(detail.currentWeight, detail.maxWeight)}%</span>
              </div>
              <div className="w-full h-3 bg-surface-container-high rounded-full overflow-hidden">
                <div
                  className="h-full bg-primary rounded-full transition-all"
                  style={{ width: `${capacityPercent(detail.currentWeight, detail.maxWeight)}%` }}
                />
              </div>
              <p className="text-body-sm text-on-surface-variant mt-1">{detail.currentWeight.toFixed(1)} / {detail.maxWeight.toFixed(1)} kg</p>
            </div>
            <div>
              <div className="flex justify-between text-body-sm mb-1">
                <span className="text-on-surface-variant">Thể tích</span>
                <span className="text-on-surface font-bold">{capacityPercent(detail.currentVolume, detail.maxVolume)}%</span>
              </div>
              <div className="w-full h-3 bg-surface-container-high rounded-full overflow-hidden">
                <div
                  className="h-full bg-tertiary rounded-full transition-all"
                  style={{ width: `${capacityPercent(detail.currentVolume, detail.maxVolume)}%` }}
                />
              </div>
              <p className="text-body-sm text-on-surface-variant mt-1">{detail.currentVolume.toFixed(1)} / {detail.maxVolume.toFixed(1)} m³</p>
            </div>
          </div>
        </div>

        {/* Shipment List */}
        <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
          <h3 className="text-label-lg font-bold text-on-surface mb-3 flex items-center gap-2">
            <span className="material-symbols-outlined text-[18px] text-primary">inventory_2</span>
            Danh sách kiện hàng ({detail.totalShipments})
          </h3>
          {detail.shipments.length === 0 ? (
            <p className="text-body-md text-on-surface-variant text-center py-6">Chưa có kiện hàng nào trong chuyến đi.</p>
          ) : (
            <div className="space-y-3">
              {detail.shipments.map((shipment) => (
                <div key={shipment.id} className="rounded-xl border border-outline-variant/50 p-4 hover:bg-surface-container-low transition-colors">
                  <div className="flex items-start justify-between mb-2">
                    <div>
                      <span className="text-label-md font-bold text-on-surface">{shipment.shipmentCode}</span>
                      {shipment.commodity && (
                        <span className="ml-2 text-label-sm text-on-surface-variant">• {shipment.commodity}</span>
                      )}
                    </div>
                    <span className={`text-label-sm font-medium px-2 py-0.5 rounded-lg ${SHIPMENT_STATUS_BADGE[shipment.status] || 'bg-gray-100 text-gray-600'}`}>
                      {SHIPMENT_STATUS_LABELS[shipment.status] || shipment.status}
                    </span>
                  </div>

                  <div className="grid grid-cols-2 gap-2 text-body-sm text-on-surface-variant mb-2">
                    <div className="flex items-center gap-1">
                      <span className="material-symbols-outlined text-[12px]">scale</span>
                      {shipment.weight} kg • {shipment.volume} m³
                    </div>
                    {shipment.senderName && (
                      <div className="flex items-center gap-1">
                        <span className="material-symbols-outlined text-[12px]">upload</span>
                        {shipment.senderName}
                      </div>
                    )}
                    {shipment.pickupAddress && (
                      <div className="flex items-center gap-1 truncate">
                        <span className="material-symbols-outlined text-[12px]">location_on</span>
                        {shipment.pickupAddress}
                      </div>
                    )}
                    {shipment.receiverName && (
                      <div className="flex items-center gap-1">
                        <span className="material-symbols-outlined text-[12px]">download</span>
                        {shipment.receiverName}
                      </div>
                    )}
                  </div>

                  {/* Action buttons per shipment */}
                  <div className="flex gap-2 mt-2 pt-2 border-t border-outline-variant/30">
                    {shipment.allowedActions.canConfirmPickup && (
                      <button
                        onClick={() => { setSelectedShipment(shipment); setShowPickupDialog(true); }}
                        className="flex items-center gap-1 px-3 py-1.5 rounded-lg bg-primary/10 text-primary text-label-sm font-bold hover:bg-primary/20 transition-colors"
                      >
                        <span className="material-symbols-outlined text-[14px]">inventory</span>
                        Nhận hàng
                      </button>
                    )}
                    {shipment.allowedActions.canStartTransport && (
                      <button
                        onClick={() => { setSelectedShipment(shipment); handleStartTransport(); }}
                        className="flex items-center gap-1 px-3 py-1.5 rounded-lg bg-indigo-50 text-indigo-700 text-label-sm font-bold hover:bg-indigo-100 transition-colors"
                      >
                        <span className="material-symbols-outlined text-[14px]">local_shipping</span>
                        Vận chuyển
                      </button>
                    )}
                    {shipment.allowedActions.canConfirmDelivery && (
                      <button
                        onClick={() => { setSelectedShipment(shipment); setShowDeliveryDialog(true); }}
                        className="flex items-center gap-1 px-3 py-1.5 rounded-lg bg-emerald-50 text-emerald-700 text-label-sm font-bold hover:bg-emerald-100 transition-colors"
                      >
                        <span className="material-symbols-outlined text-[14px]">check_circle</span>
                        Giao hàng
                      </button>
                    )}
                    {shipment.allowedActions.canConfirmCod && shipment.pendingCodPaymentId && (
                      <button
                        onClick={() => {
                          setSelectedShipment(shipment);
                          setSelectedCodPaymentId(shipment.pendingCodPaymentId!);
                          setShowCodDialog(true);
                        }}
                        className="flex items-center gap-1 px-3 py-1.5 rounded-lg bg-amber-50 text-amber-700 text-label-sm font-bold hover:bg-amber-100 transition-colors"
                      >
                        <span className="material-symbols-outlined text-[14px]">payments</span>
                        Xác nhận COD
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Timeline */}
        {detail.timeline.length > 0 && (
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
            <h3 className="text-label-lg font-bold text-on-surface mb-3 flex items-center gap-2">
              <span className="material-symbols-outlined text-[18px] text-primary">timeline</span>
              Tiến trình
            </h3>
            <div className="space-y-3">
              {detail.timeline.map((entry, idx) => (
                <div key={idx} className="flex items-start gap-3">
                  <div className="flex flex-col items-center">
                    <div className={`w-3 h-3 rounded-full ${entry.isCompleted ? 'bg-primary' : 'bg-outline-variant'}`}></div>
                    {idx < detail.timeline.length - 1 && <div className="w-0.5 h-6 bg-outline-variant/30 mt-1"></div>}
                  </div>
                  <div className="flex-1 pb-2">
                    <p className="text-body-md text-on-surface font-medium">{entry.label}</p>
                    {entry.timestamp && (
                      <p className="text-body-sm text-on-surface-variant">{formatDate(entry.timestamp)}</p>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Pickup Dialog */}
      {showPickupDialog && selectedShipment && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-6 w-full max-w-md card-shadow">
            <h3 className="text-title-lg font-bold text-on-surface mb-2">Xác nhận nhận hàng</h3>
            <p className="text-body-md text-on-surface-variant mb-4">
              Xác nhận đã nhận kiện hàng <strong>{selectedShipment.shipmentCode}</strong>?
            </p>
            <textarea
              value={pickupNote}
              onChange={(e) => setPickupNote(e.target.value)}
              placeholder="Ghi chú nhận hàng (tùy chọn)..."
              className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md placeholder:text-on-surface-variant/50 focus:outline-none focus:border-primary resize-none"
              rows={2}
            />
            <div className="flex gap-3 mt-4">
              <button
                onClick={() => { setShowPickupDialog(false); setPickupNote(''); setSelectedShipment(null); }}
                className="flex-1 px-4 py-3 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low transition-colors text-label-md font-bold"
              >
                Đóng
              </button>
              <button
                onClick={handleConfirmPickup}
                disabled={actionLoading}
                className="flex-1 px-4 py-3 rounded-xl bg-primary text-on-primary hover:bg-primary/90 transition-colors text-label-md font-bold disabled:opacity-50"
              >
                {actionLoading ? 'Đang xử lý...' : 'Xác nhận'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delivery Dialog */}
      {showDeliveryDialog && selectedShipment && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-6 w-full max-w-md card-shadow">
            <h3 className="text-title-lg font-bold text-on-surface mb-2">Xác nhận giao hàng</h3>
            <p className="text-body-md text-on-surface-variant mb-4">
              Xác nhận đã giao kiện hàng <strong>{selectedShipment.shipmentCode}</strong> thành công?
            </p>
            <textarea
              value={deliveryNote}
              onChange={(e) => setDeliveryNote(e.target.value)}
              placeholder="Ghi chú giao hàng (tùy chọn)..."
              className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md placeholder:text-on-surface-variant/50 focus:outline-none focus:border-primary resize-none"
              rows={2}
            />
            <div className="flex gap-3 mt-4">
              <button
                onClick={() => { setShowDeliveryDialog(false); setDeliveryNote(''); setSelectedShipment(null); }}
                className="flex-1 px-4 py-3 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low transition-colors text-label-md font-bold"
              >
                Đóng
              </button>
              <button
                onClick={handleConfirmDelivery}
                disabled={actionLoading}
                className="flex-1 px-4 py-3 rounded-xl bg-emerald-600 text-white hover:bg-emerald-700 transition-colors text-label-md font-bold disabled:opacity-50"
              >
                {actionLoading ? 'Đang xử lý...' : 'Xác nhận giao'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Incident Dialog */}
      {showIncidentDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-6 w-full max-w-md card-shadow">
            <h3 className="text-title-lg font-bold text-on-surface mb-2">Báo cáo sự cố</h3>
            <p className="text-body-md text-on-surface-variant mb-4">
              Mô tả sự cố bạn gặp phải trong chuyến đi.
            </p>
            <div className="space-y-3">
              <div>
                <label className="text-label-md font-medium text-on-surface mb-1 block">Loại sự cố</label>
                <select
                  value={incidentType}
                  onChange={(e) => setIncidentType(e.target.value)}
                  className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md focus:outline-none focus:border-primary"
                >
                  <option value="Delay">Trễ hạn</option>
                  <option value="VehicleBreakdown">Hỏng xe</option>
                  <option value="Accident">Tai nạn</option>
                  <option value="CargoDamage">Hư hỏng hàng hóa</option>
                  <option value="CargoLost">Mất hàng</option>
                  <option value="DeliveryProblem">Sự cố giao hàng</option>
                  <option value="RouteProblem">Sự cố tuyến đường</option>
                  <option value="Weather">Thời tiết</option>
                  <option value="Other">Khác</option>
                </select>
              </div>
              <div>
                <label className="text-label-md font-medium text-on-surface mb-1 block">Mô tả</label>
                <textarea
                  value={incidentDescription}
                  onChange={(e) => setIncidentDescription(e.target.value)}
                  placeholder="Mô tả chi tiết sự cố..."
                  className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md placeholder:text-on-surface-variant/50 focus:outline-none focus:border-primary resize-none"
                  rows={3}
                />
              </div>
              <div>
                <label className="text-label-md font-medium text-on-surface mb-1 block">Hình ảnh minh chứng</label>
                <p className="text-caption text-on-surface-variant/70 mb-2">
                  Hỗ trợ: <strong>.jpg</strong>, <strong>.jpeg</strong>, <strong>.png</strong>, <strong>.webp</strong> — tối đa <strong>5 MB</strong>/file, tối đa <strong>5</strong> ảnh
                </p>
                <input
                  type="file"
                  accept=".jpg,.jpeg,.png,.webp"
                  multiple
                  onChange={handleIncidentFileChange}
                  className="w-full px-4 py-2 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md file:mr-3 file:py-1 file:px-3 file:rounded-lg file:border-0 file:bg-primary file:text-on-primary file:text-label-md file:cursor-pointer hover:file:bg-primary/90"
                />
                {incidentFiles.length > 0 && (
                  <div className="mt-2 flex flex-wrap gap-2">
                    {incidentFiles.map((file, idx) => (
                      <div key={idx} className="relative group">
                        <img
                          src={URL.createObjectURL(file)}
                          alt={file.name}
                          className="w-16 h-16 object-cover rounded-lg border border-outline-variant"
                        />
                        <button
                          type="button"
                          onClick={() => removeIncidentFile(idx)}
                          className="absolute -top-2 -right-2 w-5 h-5 bg-error text-on-error rounded-full text-xs flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity"
                        >
                          ✕
                        </button>
                        <p className="text-caption text-on-surface-variant truncate w-16 text-center" title={file.name}>{file.name}</p>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
            <div className="flex gap-3 mt-4">
              <button
                onClick={() => { setShowIncidentDialog(false); setIncidentDescription(''); setIncidentFiles([]); }}
                className="flex-1 px-4 py-3 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low transition-colors text-label-md font-bold"
              >
                Đóng
              </button>
              <button
                onClick={handleReportIncident}
                disabled={!incidentDescription.trim() || actionLoading}
                className="flex-1 px-4 py-3 rounded-xl bg-error text-on-error hover:bg-error/90 transition-colors text-label-md font-bold disabled:opacity-50"
              >
                {actionLoading ? 'Đang gửi...' : 'Gửi báo cáo'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* COD Confirmation Dialog */}
      {showCodDialog && selectedShipment && selectedCodPaymentId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-6 w-full max-w-md card-shadow">
            <h3 className="text-title-lg font-bold text-on-surface mb-2">Xác nhận thu COD</h3>
            <p className="text-body-md text-on-surface-variant mb-4">
              Xác nhận đã thu tiền COD cho kiện hàng <strong>{selectedShipment.shipmentCode}</strong>?
            </p>
            <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 mb-4 space-y-2">
              <div className="flex justify-between text-body-md">
                <span className="text-on-surface-variant">Mã thanh toán:</span>
                <span className="font-bold text-on-surface">{selectedShipment.pendingCodPaymentCode || '-'}</span>
              </div>
              <div className="flex justify-between text-body-md">
                <span className="text-on-surface-variant">Số tiền COD:</span>
                <span className="font-bold text-amber-700 text-title-md">
                  {selectedShipment.pendingCodAmount != null
                    ? `${selectedShipment.pendingCodAmount.toLocaleString('vi-VN')} ${selectedShipment.pendingCodCurrency || 'VND'}`
                    : '-'}
                </span>
              </div>
            </div>
            <div className="flex gap-3 mt-4">
              <button
                onClick={() => { setShowCodDialog(false); setSelectedCodPaymentId(null); setSelectedShipment(null); }}
                className="flex-1 px-4 py-3 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low transition-colors text-label-md font-bold"
              >
                Đóng
              </button>
              <button
                onClick={handleConfirmCod}
                disabled={codConfirming !== null}
                className="flex-1 px-4 py-3 rounded-xl bg-amber-600 text-white hover:bg-amber-700 transition-colors text-label-md font-bold disabled:opacity-50"
              >
                {codConfirming ? 'Đang xác nhận...' : 'Xác nhận đã thu'}
              </button>
            </div>
          </div>
        </div>
      )}

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
