import { useState } from "react";
import {
  getShipmentByQrCode,
  confirmShipmentIntake,
  type ShipmentQrLookupResponse,
} from "../api/shipmentsApi";

export default function HubIntakePage() {
  const [currentStep, setCurrentStep] = useState<1 | 2 | 3 | 4>(1);
  const [qrCode, setQrCode] = useState("");
  const [shipment, setShipment] = useState<ShipmentQrLookupResponse | null>(null);
  const [actualWeightKg, setActualWeightKg] = useState("");
  const [actualVolumeCbm, setActualVolumeCbm] = useState("");
  const [loading, setLoading] = useState(false);
  const [confirming, setConfirming] = useState(false);

  const searchShipment = async () => {
    if (!qrCode.trim()) {
      alert("Vui lòng nhập mã QR.");
      return;
    }

    try {
      setLoading(true);

      const data = await getShipmentByQrCode(qrCode.trim());

      setShipment(data);
      setActualWeightKg(String(data.weightKg));
      setActualVolumeCbm(String(data.volumeCbm));
      setCurrentStep(2);
    } catch (err: any) {
      setShipment(null);
      setCurrentStep(1);
      alert(err.message ?? "Không tìm thấy đơn hàng.");
    } finally {
      setLoading(false);
    }
  };

  const confirmIntake = async () => {
    if (!shipment) return;

    if (Number(actualWeightKg) <= 0 || Number(actualVolumeCbm) <= 0) {
      alert("Cân nặng và thể tích thực tế phải lớn hơn 0.");
      return;
    }

    try {
      setConfirming(true);

      await confirmShipmentIntake(shipment.id, {
        actualWeightKg: Number(actualWeightKg),
        actualVolumeCbm: Number(actualVolumeCbm),
      });

      setShipment({
        ...shipment,
        status: "In_Warehouse",
        weightKg: Number(actualWeightKg),
        volumeCbm: Number(actualVolumeCbm),
      });

      setCurrentStep(4);
    } catch (err: any) {
      alert(err.message ?? "Nhập kho thất bại.");
    } finally {
      setConfirming(false);
    }
  };

  const resetForNextShipment = () => {
    setQrCode("");
    setShipment(null);
    setActualWeightKg("");
    setActualVolumeCbm("");
    setCurrentStep(1);
  };

  return (
    <main className="min-h-screen bg-[#f8f9ff] text-[#0b1c30] font-sans p-6">
      <div className="max-w-7xl mx-auto">
        <div className="mb-6">
          <h1 className="text-2xl font-bold text-[#0b1c30]">Nhập kho</h1>
          <p className="text-sm text-[#444653] mt-1">
            Quét hoặc nhập mã QR của đơn, đối chiếu thông tin và xác nhận
            hàng vào Hub.
          </p>
        </div>

        <Stepper currentStep={currentStep} />

        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 mt-6">
          <section className="lg:col-span-8 space-y-6">
            {/* ═══ Step 1: Quét mã ═══ */}
            {currentStep === 1 && (
              <div className="bg-white rounded-xl shadow-sm border border-[#c4c5d5] p-6">
                <div className="flex flex-col items-center text-center">
                  <div className="w-20 h-20 rounded-full bg-[#eff4ff] text-[#00288e] flex items-center justify-center mb-4">
                    <span className="material-symbols-outlined text-5xl">
                      qr_code_scanner
                    </span>
                  </div>

                  <h2 className="text-xl font-semibold">Quét mã QR kiện hàng</h2>
                  <p className="text-sm text-[#444653] mt-1 mb-5">
                    Có thể dùng máy quét QR hoặc nhập mã thủ công.
                  </p>

                  <div className="w-full max-w-xl flex">
                    <input
                      value={qrCode}
                      onChange={(e) => setQrCode(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter") searchShipment();
                      }}
                      className="flex-1 px-4 py-3 rounded-l-lg border border-[#c4c5d5] focus:outline-none focus:ring-1 focus:ring-[#00288e] uppercase"
                      placeholder="VD: GC-20260627-355E5692"
                    />

                    <button
                      onClick={searchShipment}
                      disabled={loading}
                      className="px-5 py-3 rounded-r-lg bg-[#00288e] text-white font-semibold hover:bg-[#1e40af] disabled:opacity-60 flex items-center gap-2"
                    >
                      <span className="material-symbols-outlined text-[20px]">
                        search
                      </span>
                      {loading ? "Đang tìm..." : "Tìm"}
                    </button>
                  </div>
                </div>
              </div>
            )}

            {/* ═══ Step 2: Đối chiếu ═══ */}
            {currentStep === 2 && shipment && (
              <ShipmentComparisonCard
                shipment={shipment}
                onConfirm={() => setCurrentStep(3)}
              />
            )}

            {/* ═══ Step 3: Cân đo ═══ */}
            {currentStep === 3 && shipment && (
              <div className="bg-white rounded-xl shadow-sm border border-[#c4c5d5] p-6">
                <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 pb-4 border-b border-[#c4c5d5]">
                  <div>
                    <p className="text-xs uppercase tracking-wider text-[#444653] font-semibold">
                      Cân đo thực tế tại Hub
                    </p>
                    <h2 className="text-2xl font-bold text-[#00288e] mt-1">
                      {shipment.qrCode}
                    </h2>
                  </div>
                  <StatusBadge status={shipment.status} />
                </div>

                {/* Customer declared values */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-6">
                  <InfoItem
                    label="Cân nặng khách khai báo"
                    value={`${shipment.weightKg} kg`}
                  />
                  <InfoItem
                    label="Thể tích khách khai báo"
                    value={`${shipment.volumeCbm} CBM`}
                  />
                </div>

                <div className="mt-6 bg-[#eff4ff] rounded-xl border border-[#c4c5d5] p-5">
                  <div className="flex items-center justify-between mb-4">
                    <div>
                      <h3 className="font-semibold text-[#0b1c30]">
                        Cân đo thực tế tại Hub
                      </h3>
                      <p className="text-sm text-[#444653]">
                        Nhân viên cập nhật số liệu thực tế khi khác với khách
                        khai báo.
                      </p>
                    </div>

                    <span className="hidden sm:flex items-center gap-1 text-sm text-[#006c49] font-semibold">
                      <span className="material-symbols-outlined text-[18px]">
                        scale
                      </span>
                      Manual input
                    </span>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <NumberInput
                      label="Cân nặng thực tế (kg)"
                      value={actualWeightKg}
                      onChange={setActualWeightKg}
                    />
                    <NumberInput
                      label="Thể tích thực tế (CBM)"
                      value={actualVolumeCbm}
                      onChange={setActualVolumeCbm}
                    />
                  </div>

                  <div className="flex gap-3 mt-5">
                    <button
                      onClick={() => setCurrentStep(2)}
                      className="flex-1 py-3 border border-[#c4c5d5] rounded-lg font-semibold text-[#444653] hover:bg-[#f8f9ff] flex items-center justify-center gap-2"
                    >
                      <span className="material-symbols-outlined text-[20px]">
                        arrow_back
                      </span>
                      Quay lại đối chiếu
                    </button>

                    <button
                      onClick={confirmIntake}
                      disabled={
                        confirming ||
                        Number(actualWeightKg) <= 0 ||
                        Number(actualVolumeCbm) <= 0
                      }
                      className="flex-1 py-3 bg-[#00288e] text-white rounded-lg font-semibold hover:bg-[#1e40af] disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
                    >
                      <span className="material-symbols-outlined">
                        check_circle
                      </span>
                      {confirming
                        ? "Đang xác nhận..."
                        : "Xác nhận & chuyển vào kho"}
                    </button>
                  </div>
                </div>
              </div>
            )}

            {/* ═══ Step 4: Hoàn tất ═══ */}
            {currentStep === 4 && shipment && (
              <div className="bg-white rounded-xl shadow-sm border border-[#c4c5d5] p-8 text-center">
                <div className="w-20 h-20 rounded-full bg-[#d4f5e2] text-[#006c49] flex items-center justify-center mx-auto mb-5">
                  <span className="material-symbols-outlined text-5xl">
                    task_alt
                  </span>
                </div>

                <h2 className="text-2xl font-bold text-[#0b1c30]">
                  Nhập kho thành công
                </h2>
                <p className="text-sm text-[#444653] mt-2 mb-6">
                  Kiện hàng đã được chuyển vào Hub và sẵn sàng cho Matching.
                </p>

                <div className="bg-[#f8f9ff] rounded-xl border border-[#c4c5d5] p-5 max-w-md mx-auto space-y-3 text-left">
                  <InfoItem label="Mã QR" value={shipment.qrCode} />
                  <InfoItem label="Trạng thái" value={shipment.status} />
                  <InfoItem
                    label="Cân nặng thực tế"
                    value={`${shipment.weightKg} kg`}
                  />
                  <InfoItem
                    label="Thể tích thực tế"
                    value={`${shipment.volumeCbm} CBM`}
                  />
                </div>

                <p className="text-sm text-[#006c49] font-semibold mt-5 mb-6">
                  Hàng đã được nhập vào Hub thành công.
                </p>

                <button
                  onClick={resetForNextShipment}
                  className="px-6 py-3 bg-[#00288e] text-white rounded-lg font-semibold hover:bg-[#1e40af] flex items-center gap-2 mx-auto"
                >
                  <span className="material-symbols-outlined text-[20px]">
                    add_box
                  </span>
                  Nhập kiện tiếp theo
                </button>
              </div>
            )}
          </section>

          <aside className="lg:col-span-4 space-y-6">
            <div className="bg-white rounded-xl shadow-sm border border-[#c4c5d5] p-5">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-xs uppercase tracking-wider text-[#444653] font-semibold">
                    Trạng thái phiên nhập kho
                  </p>
                  <p className="text-3xl font-bold text-[#0b1c30] mt-2">
                    {currentStep === 4 ? "1" : "0"}
                    <span className="text-base text-[#444653] font-normal ml-1">
                      kiện
                    </span>
                  </p>
                </div>

                <div className="w-12 h-12 rounded-full bg-[#6cf8bb] text-[#00714d] flex items-center justify-center">
                  <span className="material-symbols-outlined">inventory_2</span>
                </div>
              </div>
            </div>

            
          </aside>
        </div>
      </div>
    </main>
  );
}

/* ────────────────────────────────────────────────
   Step 2 — Shipment Comparison Card
   ──────────────────────────────────────────────── */
function ShipmentComparisonCard({
  shipment,
  onConfirm,
}: {
  shipment: ShipmentQrLookupResponse;
  onConfirm: () => void;
}) {
  const isDraft = shipment.status === "Draft";

  return (
    <div className="bg-white rounded-xl shadow-sm border border-[#c4c5d5] p-6">
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 pb-4 border-b border-[#c4c5d5]">
        <div>
          <p className="text-xs uppercase tracking-wider text-[#444653] font-semibold">
            Thông tin kiện hàng
          </p>
          <h2 className="text-2xl font-bold text-[#00288e] mt-1">
            {shipment.qrCode}
          </h2>
        </div>
        <StatusBadge status={shipment.status} />
      </div>

      {!isDraft && (
        <div className="mt-4 bg-[#fff3e0] border border-[#ffcc80] rounded-lg p-4 flex items-start gap-3">
          <span className="material-symbols-outlined text-[#e65100] text-[22px] mt-0.5">
            warning
          </span>
          <div>
            <p className="text-sm font-semibold text-[#e65100]">
              Đơn không ở trạng thái Draft
            </p>
            <p className="text-sm text-[#bf360c] mt-1">
              Hiện tại trạng thái là <strong>{shipment.status}</strong>. Chỉ đơn
              ở trạng thái Draft mới có thể nhập kho.
            </p>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-5 mt-6">
        <InfoItem label="Loại hàng" value={shipment.cargoType} />
        <InfoItem
          label="Ghi chú"
          value={shipment.specialHandlingNote || "Không có"}
        />
        <InfoItem label="Người nhận" value={shipment.receiverName} />
        <InfoItem label="SĐT người nhận" value={shipment.receiverPhone} />
        <InfoItem
          label="Địa chỉ giao hàng"
          value={shipment.destAddress}
          className="md:col-span-2"
        />
        <InfoItem
          label="Cân nặng khách khai báo"
          value={`${shipment.weightKg} kg`}
        />
        <InfoItem
          label="Thể tích khách khai báo"
          value={`${shipment.volumeCbm} CBM`}
        />
      </div>

      <button
        onClick={onConfirm}
        disabled={!isDraft}
        className="w-full mt-6 py-4 bg-[#00288e] text-white rounded-lg font-semibold hover:bg-[#1e40af] disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
      >
        <span className="material-symbols-outlined">check</span>
        Thông tin đúng – Tiếp tục cân đo
      </button>
    </div>
  );
}

/* ────────────────────────────────────────────────
   Stepper Component
   ──────────────────────────────────────────────── */
function Stepper({ currentStep }: { currentStep: 1 | 2 | 3 | 4 }) {
  const steps = ["Quét mã", "Đối chiếu", "Cân đo", "Hoàn tất"];

  return (
    <div className="bg-white rounded-xl shadow-sm border border-[#c4c5d5] p-5">
      <div className="flex items-center justify-between">
        {steps.map((label, index) => {
          const step = index + 1;
          const active = step <= currentStep;

          return (
            <div key={label} className="flex items-center flex-1 last:flex-none">
              <div className="flex flex-col items-center gap-2">
                <div
                  className={`w-9 h-9 rounded-full flex items-center justify-center font-bold ${
                    active
                      ? "bg-[#00288e] text-white"
                      : "bg-[#e5eeff] text-[#444653]"
                  }`}
                >
                  {active && step < currentStep ? (
                    <span className="material-symbols-outlined text-[20px]">
                      check
                    </span>
                  ) : (
                    step
                  )}
                </div>
                <span
                  className={`text-xs font-semibold ${
                    active ? "text-[#00288e]" : "text-[#444653]"
                  }`}
                >
                  {label}
                </span>
              </div>

              {index < steps.length - 1 && (
                <div
                  className={`h-px flex-1 mx-3 ${
                    step < currentStep ? "bg-[#00288e]" : "bg-[#c4c5d5]"
                  }`}
                />
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

/* ────────────────────────────────────────────────
   Shared UI Components
   ──────────────────────────────────────────────── */
function StatusBadge({ status }: { status: string }) {
  const isDraft = status === "Draft";

  return (
    <span
      className={`px-3 py-1 rounded-full text-sm font-semibold border ${
        isDraft
          ? "bg-[#eff4ff] text-[#00288e] border-[#a8b8ff]"
          : "bg-[#6cf8bb] text-[#00714d] border-[#4edea3]"
      }`}
    >
      {status}
    </span>
  );
}

function InfoItem({
  label,
  value,
  className = "",
}: {
  label: string;
  value: string;
  className?: string;
}) {
  return (
    <div
      className={`bg-[#f8f9ff] rounded-lg border border-[#c4c5d5] p-4 ${className}`}
    >
      <p className="text-xs uppercase tracking-wider text-[#444653] font-semibold mb-1">
        {label}
      </p>
      <p className="text-sm font-medium text-[#0b1c30]">{value}</p>
    </div>
  );
}

function NumberInput({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <div>
      <label className="block text-sm font-semibold text-[#444653] mb-1">
        {label}
      </label>
      <input
        type="number"
        min="0"
        step="0.01"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full px-3 py-3 rounded-lg border border-[#c4c5d5] bg-white focus:outline-none focus:ring-1 focus:ring-[#00288e]"
      />
    </div>
  );
}