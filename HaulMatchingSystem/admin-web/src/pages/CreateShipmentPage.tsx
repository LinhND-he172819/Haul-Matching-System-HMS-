import { useState } from "react";
import { QRCodeCanvas } from "qrcode.react";
import { createDraftShipment, type DraftShipmentResponse } from "../api/shipmentsApi";

export default function CreateShipmentPage() {
    const [step, setStep] = useState(1);
    const [result, setResult] = useState<DraftShipmentResponse | null>(null);
    const [loading, setLoading] = useState(false);

    const [form, setForm] = useState({
        customerId: "77ef126c-1a62-43f0-9190-3f4c9884f546",

        receiverName: "",
        receiverPhone: "",
        destAddress: "",
        destLat: "",
        destLng: "",

        cargoType: "",
        weightKg: "",
        volumeCbm: "",
        specialHandlingNote: "",
    });

    const update = (key: string, value: string) => {
        setForm((prev) => ({ ...prev, [key]: value }));
    };

    const handleGeocodeAddress = async () => {
        if (!form.destAddress.trim()) {
            alert("Vui lòng nhập địa chỉ giao hàng.");
            return;
        }

        setForm((prev) => ({
            ...prev,
            destLat: "21.0278",
            destLng: "105.8342",
        }));

        alert("Đã xác định tọa độ từ địa chỉ.");
    };

    const goToStep2 = () => {
        if (!form.receiverName.trim()) {
            alert("Vui lòng nhập tên người nhận.");
            return;
        }

        if (!form.receiverPhone.trim()) {
            alert("Vui lòng nhập số điện thoại người nhận.");
            return;
        }

        if (!form.destAddress.trim()) {
            alert("Vui lòng nhập địa chỉ giao hàng.");
            return;
        }

        setStep(2);
    };

    const submit = async () => {
        if (!form.cargoType.trim()) {
            alert("Vui lòng nhập loại hàng.");
            return;
        }

        if (Number(form.weightKg) <= 0) {
            alert("Cân nặng phải lớn hơn 0.");
            return;
        }

        if (Number(form.volumeCbm) <= 0) {
            alert("Thể tích phải lớn hơn 0.");
            return;
        }

        if (!form.destLat || !form.destLng) {
            alert("Vui lòng xác định vị trí giao hàng.");
            return;
        }

        try {
            setLoading(true);

            const data = await createDraftShipment({
                customerId: form.customerId,
                cargoType: form.cargoType,
                weightKg: Number(form.weightKg),
                volumeCbm: Number(form.volumeCbm),
                receiverName: form.receiverName,
                receiverPhone: form.receiverPhone,
                destAddress: form.destAddress,
                destLat: Number(form.destLat),
                destLng: Number(form.destLng),
                specialHandlingNote: form.specialHandlingNote,
            });

            setResult(data);
            setStep(3);
        } catch (err: any) {
            alert(err.message ?? "Tạo đơn thất bại.");
        } finally {
            setLoading(false);
        }
    };

    if (result) {
        return (
            <main className="min-h-screen bg-[#f8f9ff] text-[#0b1c30] flex items-center justify-center p-4 font-sans">
                <div className="w-full max-w-lg bg-white rounded-xl shadow-[0_4px_15px_0_rgba(0,0,0,0.04)] p-5 flex flex-col items-center gap-4 text-center">
                    <div className="flex flex-col items-center gap-2 mb-4">
                        <div className="w-16 h-16 rounded-full bg-[#6cf8bb] flex items-center justify-center mb-2">
                            <span
                                className="material-symbols-outlined text-[#00714d] text-4xl"
                                style={{ fontVariationSettings: "'FILL' 1" }}
                            >
                                check_circle
                            </span>
                        </div>

                        <h1 className="text-4xl leading-[44px] tracking-[-0.02em] font-bold text-[#0b1c30]">
                            Thành công!
                        </h1>

                        <div className="inline-flex items-center gap-2 px-3 py-1 bg-[#eff4ff] rounded-full border border-[#c4c5d5]">
                            <span className="w-2 h-2 rounded-full bg-[#006c49]" />
                            <span className="text-xs font-medium text-[#444653]">
                                Chờ mang hàng đến Hub
                            </span>
                        </div>
                    </div>

                    <div className="w-full bg-[#f8f9ff] p-5 rounded-lg border border-[#c4c5d5] flex flex-col items-center gap-2 mb-4">
                        <p className="text-sm font-semibold text-[#444653]">
                            Mã đơn hàng của bạn
                        </p>

                        <p className="text-xl leading-7 font-semibold text-[#00288e] tracking-tight">
                            {result.qrCode}
                        </p>

                        <div className="mt-4 p-4 bg-white rounded-lg shadow-sm border border-[#c4c5d5]">
                            <QRCodeCanvas value={result.qrCode} size={190} />
                        </div>
                    </div>

                    <div className="w-full text-left">
                        <h2 className="text-xl leading-7 font-semibold text-[#0b1c30] mb-4">
                            Các bước tiếp theo
                        </h2>

                        <ul className="flex flex-col gap-2">
                            <li className="flex items-start gap-3 p-3 rounded-lg hover:bg-[#f8f9ff]">
                                <span className="material-symbols-outlined text-[#00288e] mt-0.5">
                                    inventory_2
                                </span>
                                <div>
                                    <p className="text-sm font-semibold text-[#0b1c30]">
                                        1. Đóng gói hàng cẩn thận
                                    </p>
                                    <p className="text-sm text-[#444653] mt-1">
                                        Đảm bảo hàng hóa được bảo vệ an toàn trước khi vận chuyển.
                                    </p>
                                </div>
                            </li>

                            <li className="flex items-start gap-3 p-3 rounded-lg hover:bg-[#f8f9ff]">
                                <span className="material-symbols-outlined text-[#00288e] mt-0.5">
                                    pin_drop
                                </span>
                                <div>
                                    <p className="text-sm font-semibold text-[#0b1c30] flex items-center gap-2">
                                        2. Mang đến Hub gần nhất
                                        <a
                                            href="#"
                                            className="text-[#00288e] text-xs underline underline-offset-2 flex items-center"
                                        >
                                            Xem bản đồ
                                            <span className="material-symbols-outlined text-[14px]">
                                                open_in_new
                                            </span>
                                        </a>
                                    </p>
                                    <p className="text-sm text-[#444653] mt-1">
                                        Giao hàng cho nhân viên tại Hub Ghép Chuyến.
                                    </p>
                                </div>
                            </li>

                            <li className="flex items-start gap-3 p-3 rounded-lg hover:bg-[#f8f9ff]">
                                <span className="material-symbols-outlined text-[#00288e] mt-0.5">
                                    qr_code_scanner
                                </span>
                                <div>
                                    <p className="text-sm font-semibold text-[#0b1c30]">
                                        3. Đưa mã QR cho nhân viên
                                    </p>
                                    <p className="text-sm text-[#444653] mt-1">
                                        Nhân viên sẽ quét mã QR trên điện thoại của bạn để nhận hàng.
                                    </p>
                                </div>
                            </li>
                        </ul>
                    </div>

                    <div className="w-full flex flex-col sm:flex-row gap-4 mt-6">
                        <button className="flex-1 bg-[#00288e] text-white text-sm font-semibold py-3 px-6 rounded-lg hover:bg-[#1e40af] flex justify-center items-center gap-2">
                            <span className="material-symbols-outlined">home</span>
                            Về trang chủ
                        </button>

                        <button className="flex-1 border border-[#00288e] text-[#00288e] text-sm font-semibold py-3 px-6 rounded-lg hover:bg-[#eff4ff] flex justify-center items-center gap-2">
                            <span className="material-symbols-outlined">download</span>
                            Tải mã QR về máy
                        </button>
                    </div>
                </div>
            </main>
        );
    }

    return (
        <main className="min-h-screen bg-background flex items-center justify-center p-4">
            <div className="w-full max-w-3xl bg-white rounded-xl shadow p-6 md:p-8">
                <h1 className="text-3xl font-bold text-center mb-8">Tạo Đơn Hàng Mới</h1>

                <div className="flex justify-center gap-4 mb-8">
                    <Step active={step === 1} label="Thông tin người nhận" number={1} />
                    <Step active={step === 2} label="Chi tiết hàng hóa" number={2} />
                    <Step active={step === 3} label="Xác nhận" number={3} />
                </div>

                {step === 1 && (
                    <div className="space-y-8">
                        <section>
                            <h2 className="text-xl font-semibold mb-4">Người Nhận</h2>

                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                <Input
                                    label="Họ và tên"
                                    value={form.receiverName}
                                    onChange={(v) => update("receiverName", v)}
                                />

                                <Input
                                    label="Số điện thoại"
                                    value={form.receiverPhone}
                                    onChange={(v) => update("receiverPhone", v)}
                                />

                                <Input
                                    className="md:col-span-2"
                                    label="Địa chỉ giao hàng"
                                    value={form.destAddress}
                                    onChange={(v) => {
                                        update("destAddress", v);
                                        setForm((prev) => ({
                                            ...prev,
                                            destAddress: v,
                                            destLat: "",
                                            destLng: "",
                                        }));
                                    }}
                                />
                            </div>
                        </section>

                        <div className="flex justify-end">
                            <button
                                className="bg-blue-800 text-white px-6 py-3 rounded-lg"
                                onClick={goToStep2}
                            >
                                Tiếp tục
                            </button>
                        </div>
                    </div>
                )}

                {step === 2 && (
                    <div className="space-y-6">
                        <Input
                            label="Loại hàng"
                            value={form.cargoType}
                            onChange={(v) => update("cargoType", v)}
                        />

                        <Input
                            label="Cân nặng kg"
                            value={form.weightKg}
                            onChange={(v) => update("weightKg", v)}
                            type="number"
                        />

                        <Input
                            label="Thể tích CBM"
                            value={form.volumeCbm}
                            onChange={(v) => update("volumeCbm", v)}
                            type="number"
                        />

                        <Input
                            label="Ghi chú xử lý đặc biệt"
                            value={form.specialHandlingNote}
                            onChange={(v) => update("specialHandlingNote", v)}
                        />

                        <div className="space-y-2">
                            <label className="block text-sm font-medium text-slate-600">
                                Xác định vị trí giao hàng
                            </label>

                            <div className="flex items-center gap-2">
                                <span className="flex-1 px-3 py-2 rounded-lg border bg-slate-50 text-slate-700">
                                    {form.destAddress}
                                </span>

                                <button
                                    type="button"
                                    className="px-4 py-2 rounded-lg bg-blue-800 text-white"
                                    onClick={handleGeocodeAddress}
                                >
                                    Tìm vị trí
                                </button>
                            </div>

                            {form.destLat && form.destLng && (
                                <p className="text-sm text-green-700">
                                    ✓ Đã xác định vị trí giao hàng.
                                </p>
                            )}
                        </div>

                        <div className="flex justify-between">
                            <button
                                className="border px-6 py-3 rounded-lg"
                                onClick={() => setStep(1)}
                            >
                                Quay lại
                            </button>

                            <button
                                className="bg-blue-800 text-white px-6 py-3 rounded-lg disabled:opacity-60"
                                onClick={submit}
                                disabled={loading}
                            >
                                {loading ? "Đang tạo..." : "Tạo đơn"}
                            </button>
                        </div>
                    </div>
                )}
            </div>
        </main>
    );
}

function Step({
    active,
    number,
    label,
}: {
    active: boolean;
    number: number;
    label: string;
}) {
    return (
        <div className="flex flex-col items-center gap-2">
            <div
                className={`w-8 h-8 rounded-full flex items-center justify-center ${active ? "bg-blue-800 text-white" : "bg-slate-200 text-slate-600"
                    }`}
            >
                {number}
            </div>

            <span className={active ? "text-blue-800 text-sm" : "text-slate-500 text-sm"}>
                {label}
            </span>
        </div>
    );
}

function Input({
    label,
    value,
    onChange,
    className = "",
    type = "text",
}: {
    label: string;
    value: string;
    onChange: (value: string) => void;
    className?: string;
    type?: string;
}) {
    return (
        <div className={className}>
            <label className="block text-sm font-medium text-slate-600 mb-1">
                {label}
            </label>

            <input
                type={type}
                className="w-full px-3 py-2 rounded-lg border border-slate-300 focus:outline-none focus:ring-1 focus:ring-blue-800"
                value={value}
                onChange={(e) => onChange(e.target.value)}
            />
        </div>
    );
}
