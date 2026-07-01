import { useState } from "react";
import {
  getShipmentByQrCode,
  confirmShipmentIntake,
  type ShipmentQrLookupResponse,
} from "../api/shipmentsApi";

export default function HubIntakePage() {
  const [qrCode, setQrCode] = useState("");
  const [shipment, setShipment] = useState<ShipmentQrLookupResponse | null>(null);
  const [actualWeightKg, setActualWeightKg] = useState("");
  const [actualVolumeCbm, setActualVolumeCbm] = useState("");
  const [loading, setLoading] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [message, setMessage] = useState("");

  const searchShipment = async () => {
    if (!qrCode.trim()) {
      alert("Vui lòng nhập mã QR.");
      return;
    }

    try {
      setLoading(true);
      setMessage("");

      const data = await getShipmentByQrCode(qrCode.trim());

      setShipment(data);
      setActualWeightKg(String(data.weightKg));
      setActualVolumeCbm(String(data.volumeCbm));
    } catch (err: any) {
      setShipment(null);
      alert(err.message ?? "Không tìm thấy đơn hàng.");
    } finally {
      setLoading(false);
    }
  };

  const confirmIntake = async () => {
    if (!shipment) return;

    if (shipment.status !== "Draft") {
      alert("Chỉ đơn ở trạng thái Draft mới được nhập kho.");
      return;
    }

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

      setMessage("Nhập kho thành công. Kiện hàng đã chuyển sang In_Warehouse.");
    } catch (err: any) {
      alert(err.message ?? "Nhập kho thất bại.");
    } finally {
      setConfirming(false);
    }
  };

  return (
    <main className="min-h-screen bg-[#f8f9ff] text-[#0b1c30] font-sans p-6">
      <div className="max-w-7xl mx-auto">
        <div className="mb-6">
          <h1 className="text-2xl font-bold text-[#0b1c30]">
            Nhập kho
          </h1>
          <p className="text-sm text-[#444653] mt-1">
            Quét hoặc nhập mã QR của đơn, đối chiếu thông tin và xác nhận
            hàng vào Hub.
          </p>
        </div>

        <Stepper status={shipment?.status} />

        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 mt-6">
          <section className="lg:col-span-8 space-y-6">
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

            {shipment && (
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

                <div className="grid grid-cols-1 md:grid-cols-2 gap-5 mt-6">
                  <InfoItem label="Loại hàng" value={shipment.cargoType} />
                  <InfoItem
                    label="Ghi chú"
                    value={shipment.specialHandlingNote || "Không có"}
                  />
                  <InfoItem label="Người nhận" value={shipment.receiverName} />
                  <InfoItem
                    label="SĐT người nhận"
                    value={shipment.receiverPhone}
                  />
                  <InfoItem
                    label="Địa chỉ giao hàng"
                    value={shipment.destAddress}
                    className="md:col-span-2"
                  />
                </div>

                <div className="mt-6 bg-[#eff4ff] rounded-xl border border-[#c4c5d5] p-5">
                  <div className="flex items-center justify-between mb-4">
                    <div>
                      <h3 className="font-semibold text-[#0b1c30]">
                        Cân đo thực tế tại Hub
                      </h3>
                      <p className="text-sm text-[#444653]">
                        Nhân viên có thể cập nhật lại nếu số liệu thực tế khác
                        số liệu khách khai báo.
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

                  <button
                    onClick={confirmIntake}
                    disabled={confirming || shipment.status !== "Draft"}
                    className="w-full mt-5 py-4 bg-[#00288e] text-white rounded-lg font-semibold hover:bg-[#1e40af] disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
                  >
                    <span className="material-symbols-outlined">
                      check_circle
                    </span>
                    {confirming
                      ? "Đang xác nhận..."
                      : shipment.status === "Draft"
                      ? "Xác nhận & chuyển vào kho"
                      : "Đơn đã được xử lý"}
                  </button>

                  {message && (
                    <p className="mt-4 text-sm text-[#006c49] font-semibold">
                      {message}
                    </p>
                  )}
                </div>
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
                    {shipment ? "1" : "0"}
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

            {/* <div className="bg-white rounded-xl shadow-sm border border-[#c4c5d5] p-5">
              <h3 className="font-semibold flex items-center gap-2 text-[#0b1c30]">
                <span className="material-symbols-outlined text-[#ba1a1a]">
                  warning
                </span>
                Lưu ý nghiệp vụ
              </h3>

              <ul className="mt-4 space-y-3 text-sm text-[#444653]">
                <li>• Chỉ đơn ở trạng thái Draft mới được nhập kho.</li>
                <li>
                  • Khi xác nhận, hệ thống tự lấy Hub từ tài khoản nhân viên.
                </li>
                <li>
                  • Shipment sẽ chuyển sang trạng thái In_Warehouse và sẵn sàng
                  cho Matching Engine.
                </li>
              </ul>
            </div> */}
          </aside>
        </div>
      </div>
    </main>
  );
}

function Stepper({ status }: { status?: string }) {
  const activeStep = status === "In_Warehouse" ? 4 : status ? 3 : 1;

  const steps = [
    "Quét mã",
    "Đối chiếu",
    "Cân đo",
    "Hoàn tất",
  ];

  return (
    <div className="bg-white rounded-xl shadow-sm border border-[#c4c5d5] p-5">
      <div className="flex items-center justify-between">
        {steps.map((label, index) => {
          const step = index + 1;
          const active = step <= activeStep;

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
                  {active && step < activeStep ? (
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
                    step < activeStep ? "bg-[#00288e]" : "bg-[#c4c5d5]"
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
    <div className={`bg-[#f8f9ff] rounded-lg border border-[#c4c5d5] p-4 ${className}`}>
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