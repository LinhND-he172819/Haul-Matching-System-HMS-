import { useState } from "react";
import { QRCodeCanvas } from "qrcode.react";
import {
    createDraftShipment,
    geocodeAddress,
    type DraftShipmentResponse,
} from "../api/shipmentsApi";
import { createProposal } from "../api/proposalApi";
import type { PublicTripPost } from "../api/tripPostApi";

interface CreateProposalPageProps {
    trip: PublicTripPost;
    tripPostId: string;
    onBack: () => void;
    onLogout?: () => void;
}

/* ────────────────────────────────────────────────────── */
/*  Success Screen                                       */
/* ────────────────────────────────────────────────────── */
function SuccessScreen({ qrCode, tripTitle, onBack }: { qrCode: string; tripTitle: string; onBack: () => void }) {
    return (
        <main className="min-h-screen bg-gradient-to-br from-[#f0f4ff] via-[#f8f9ff] to-[#f0f4ff] flex items-center justify-center p-4 font-sans">
            <div className="w-full max-w-lg bg-white rounded-2xl shadow-[0_8px_30px_rgba(0,40,142,0.08)] p-8 flex flex-col items-center gap-4 text-center animate-fade-in">
                {/* Success Icon */}
                <div className="w-20 h-20 rounded-full bg-gradient-to-br from-[#6cf8bb] to-[#3ddfa0] flex items-center justify-center mb-2 shadow-lg shadow-emerald-200">
                    <span
                        className="material-symbols-outlined text-white text-[44px]"
                        style={{ fontVariationSettings: "'FILL' 1" }}
                    >
                        check_circle
                    </span>
                </div>

                <h1 className="text-3xl font-bold text-[#0b1c30]">Đề xuất đã gửi!</h1>

                <div className="inline-flex items-center gap-2 px-4 py-1.5 bg-emerald-50 rounded-full border border-emerald-200">
                    <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
                    <span className="text-xs font-semibold text-emerald-700">Đề xuất ghép chuyến đã được gửi đến tài xế</span>
                </div>

                {/* QR Code */}
                <div className="w-full bg-[#f8f9ff] p-6 rounded-xl border border-gray-100 flex flex-col items-center gap-3 mt-2">
                    <p className="text-sm font-semibold text-gray-500">Mã đơn hàng</p>
                    <p className="text-xl font-bold text-[#00288e] tracking-wide">{qrCode}</p>
                    <div className="p-4 bg-white rounded-xl shadow-sm border border-gray-100">
                        <QRCodeCanvas value={qrCode} size={180} />
                    </div>
                </div>

                {/* Next Steps */}
                <div className="w-full text-left mt-2">
                    <h2 className="text-lg font-bold text-[#0b1c30] mb-3">Các bước tiếp theo</h2>
                    <ul className="flex flex-col gap-2.5">
                        <li className="flex items-start gap-3 p-3 rounded-xl bg-blue-50/50">
                            <span className="material-symbols-outlined text-[#00288e] text-[20px] mt-0.5">send</span>
                            <div>
                                <p className="text-sm font-bold text-[#0b1c30]">1. Chờ tài xế xác nhận</p>
                                <p className="text-sm text-gray-500 mt-0.5">Tài xế sẽ xem và chấp nhận/từ chối đề xuất ghép chuyến.</p>
                            </div>
                        </li>
                        <li className="flex items-start gap-3 p-3 rounded-xl bg-blue-50/50">
                            <span className="material-symbols-outlined text-[#00288e] text-[20px] mt-0.5">inventory_2</span>
                            <div>
                                <p className="text-sm font-bold text-[#0b1c30]">2. Đóng gói hàng cẩn thận</p>
                                <p className="text-sm text-gray-500 mt-0.5">Hàng hóa sẽ được tài xế đến nhận trực tiếp tại địa chỉ của bạn.</p>
                            </div>
                        </li>
                        <li className="flex items-start gap-3 p-3 rounded-xl bg-blue-50/50">
                            <span className="material-symbols-outlined text-[#00288e] text-[20px] mt-0.5">qr_code_scanner</span>
                            <div>
                                <p className="text-sm font-bold text-[#0b1c30]">3. Giao hàng cho tài xế</p>
                                <p className="text-sm text-gray-500 mt-0.5">Đưa mã QR cho tài xế khi giao nhận hàng.</p>
                            </div>
                        </li>
                    </ul>
                </div>

                {/* Back Button */}
                <button
                    onClick={onBack}
                    className="mt-4 w-full py-3 rounded-xl bg-[#00288e] text-white font-bold text-sm hover:bg-[#001f6e] transition-all shadow-md shadow-blue-200"
                >
                    Quay lại trang chủ
                </button>
            </div>
        </main>
    );
}

/* ────────────────────────────────────────────────────── */
/*  Main Proposal Creation Form                          */
/* ────────────────────────────────────────────────────── */
export default function CreateProposalPage({ trip, tripPostId, onBack, onLogout }: CreateProposalPageProps) {
    const [loading, setLoading] = useState(false);
    const [result, setResult] = useState<DraftShipmentResponse | null>(null);
    const [geocoding, setGeocoding] = useState(false);
    const [destResolved, setDestResolved] = useState(false);

    const [form, setForm] = useState(() => ({
        // Sender info (DirectPickup)
        senderName: "",
        senderPhone: "",
        pickupAddress: "",
        pickupLatitude: "",
        pickupLongitude: "",
        pickupNote: "",
        // Receiver info
        receiverName: "",
        receiverPhone: "",
        destAddress: "",
        destLat: "",
        destLng: "",
        // Cargo info
        cargoType: "",
        weightKg: "",
        volumeCbm: "",
        specialHandlingNote: "",
    }));

    const update = (key: string, value: string) => {
        setForm((prev) => ({ ...prev, [key]: value }));
        if (key === "destAddress") setDestResolved(false);
    };

    /* ── Geocode destination ── */
    const handleGeocode = async () => {
        if (!form.destAddress.trim()) {
            alert("Vui lòng nhập địa chỉ giao hàng.");
            return;
        }
        try {
            setGeocoding(true);
            const result = await geocodeAddress(form.destAddress);
            setForm((prev) => ({
                ...prev,
                destLat: String(result.lat),
                destLng: String(result.lng),
            }));
            setDestResolved(true);
        } catch {
            alert("Không tìm thấy địa chỉ. Vui lòng nhập địa chỉ rõ hơn.");
        } finally {
            setGeocoding(false);
        }
    };

    /* ── Submit ── */
    const handleSubmit = async () => {
        // Validate
        if (!form.senderName.trim()) { alert("Vui lòng nhập tên người gửi."); return; }
        if (!form.senderPhone.trim()) { alert("Vui lòng nhập số điện thoại người gửi."); return; }
        if (!form.pickupAddress.trim()) { alert("Vui lòng nhập địa chỉ nhận hàng."); return; }
        if (!form.receiverName.trim()) { alert("Vui lòng nhập tên người nhận."); return; }
        if (!form.receiverPhone.trim()) { alert("Vui lòng nhập số điện thoại người nhận."); return; }
        if (!form.destAddress.trim()) { alert("Vui lòng nhập địa chỉ giao hàng."); return; }
        if (!form.cargoType.trim()) { alert("Vui lòng nhập loại hàng."); return; }
        if (Number(form.weightKg) <= 0) { alert("Cân nặng phải lớn hơn 0."); return; }
        if (Number(form.volumeCbm) <= 0) { alert("Thể tích phải lớn hơn 0."); return; }
        if (!form.destLat || !form.destLng) { alert("Vui lòng xác định vị trí giao hàng trước khi gửi."); return; }

        const customerId = localStorage.getItem('userId') ?? localStorage.getItem('customerId') ?? '';

        try {
            setLoading(true);

            // Step 1: Create draft shipment
            const shipment = await createDraftShipment({
                customerId,
                senderName: form.senderName,
                senderPhone: form.senderPhone,
                pickupAddress: form.pickupAddress,
                pickupLatitude: form.pickupLatitude ? Number(form.pickupLatitude) : undefined,
                pickupLongitude: form.pickupLongitude ? Number(form.pickupLongitude) : undefined,
                pickupNote: form.pickupNote || undefined,
                cargoType: form.cargoType,
                weightKg: Number(form.weightKg),
                volumeCbm: Number(form.volumeCbm),
                receiverName: form.receiverName,
                receiverPhone: form.receiverPhone,
                destAddress: form.destAddress,
                destLat: Number(form.destLat),
                destLng: Number(form.destLng),
                specialHandlingNote: form.specialHandlingNote || undefined,
            });

            // Step 2: Create proposal
            await createProposal(tripPostId, {
                shipmentId: shipment.id,
                senderName: form.senderName,
                senderPhone: form.senderPhone,
                pickupAddress: form.pickupAddress,
                pickupLatitude: form.pickupLatitude ? Number(form.pickupLatitude) : undefined,
                pickupLongitude: form.pickupLongitude ? Number(form.pickupLongitude) : undefined,
                pickupNote: form.pickupNote || undefined,
            });

            setResult(shipment);
        } catch (err: any) {
            alert(err.message ?? "Gửi đề xuất thất bại.");
        } finally {
            setLoading(false);
        }
    };

    /* ── Render success screen ── */
    if (result) {
        return (
            <SuccessScreen
                qrCode={result.qrCode}
                tripTitle={trip.title}
                onBack={onBack}
            />
        );
    }

    /* ── Main Form ── */
    const remainingWeight = trip.remainingWeightKg;
    const remainingVolume = trip.remainingVolumeCbm;

    return (
        <main className="min-h-screen bg-gradient-to-br from-[#f0f4ff] via-[#f8f9ff] to-[#f0f4ff] font-sans">
            {/* Header */}
            <header className="bg-white/80 backdrop-blur-md border-b border-gray-100 sticky top-0 z-30">
                <div className="max-w-3xl mx-auto px-4 py-3 flex items-center justify-between">
                    <button
                        onClick={onBack}
                        className="flex items-center gap-2 text-gray-600 hover:text-[#00288e] transition-colors text-sm font-medium"
                    >
                        <span className="material-symbols-outlined text-[20px]">arrow_back</span>
                        Quay lại
                    </button>
                    <h1 className="text-sm font-bold text-gray-800">Tạo đề xuất ghép chuyến</h1>
                    {onLogout && (
                        <button onClick={onLogout} className="text-gray-500 hover:text-red-600 transition-colors">
                            <span className="material-symbols-outlined text-[22px]">logout</span>
                        </button>
                    )}
                </div>
            </header>

            <div className="max-w-3xl mx-auto px-4 py-6 space-y-5 pb-24">
                {/* ── Trip Info Card ── */}
                <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
                    <div className="bg-gradient-to-r from-[#00288e] to-[#0041c4] px-5 py-4">
                        <div className="flex items-center gap-2 mb-1">
                            <span className="material-symbols-outlined text-white/80 text-[18px]">route</span>
                            <span className="text-white/80 text-xs font-semibold uppercase tracking-wider">Chuyến xe</span>
                        </div>
                        <h2 className="text-white font-bold text-lg">{trip.title}</h2>
                    </div>
                    <div className="px-5 py-4">
                        <div className="flex items-center gap-3 mb-3">
                            <div className="flex-1 text-center">
                                <p className="text-[10px] text-gray-400 uppercase tracking-wider font-semibold">Điểm đi</p>
                                <p className="font-bold text-gray-800 text-sm mt-0.5">{trip.originHubName}</p>
                            </div>
                            <span className="material-symbols-outlined text-[#00288e] text-[20px]">arrow_forward</span>
                            <div className="flex-1 text-center">
                                <p className="text-[10px] text-gray-400 uppercase tracking-wider font-semibold">Điểm đến</p>
                                <p className="font-bold text-gray-800 text-sm mt-0.5">{trip.destinationHubName}</p>
                            </div>
                        </div>
                        <div className="grid grid-cols-2 gap-3 pt-3 border-t border-gray-100">
                            <div className="text-center">
                                <p className="text-[10px] text-gray-400 uppercase tracking-wider font-semibold">Dung tích còn</p>
                                <p className="font-bold text-emerald-600 text-sm">{remainingWeight} kg</p>
                            </div>
                            <div className="text-center">
                                <p className="text-[10px] text-gray-400 uppercase tracking-wider font-semibold">Thể tích còn</p>
                                <p className="font-bold text-emerald-600 text-sm">{remainingVolume} m³</p>
                            </div>
                        </div>
                    </div>
                </div>

                {/* ── Sender Info ── */}
                <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
                    <div className="flex items-center gap-2.5 mb-4">
                        <div className="w-8 h-8 bg-amber-50 rounded-lg flex items-center justify-center">
                            <span className="material-symbols-outlined text-amber-600 text-[18px]">person_pin_circle</span>
                        </div>
                        <div>
                            <h3 className="text-sm font-bold text-gray-800">Thông tin người gửi</h3>
                            <p className="text-[11px] text-amber-600 font-medium">Tài xế sẽ đến nhận hàng tại địa chỉ của bạn</p>
                        </div>
                    </div>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                        <div>
                            <label className="block text-xs font-semibold text-gray-500 mb-1">Họ tên người gửi *</label>
                            <input
                                type="text"
                                value={form.senderName}
                                onChange={(e) => update("senderName", e.target.value)}
                                placeholder="Nguyễn Văn A"
                                className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#00288e]/30 focus:border-[#00288e] transition-all"
                            />
                        </div>
                        <div>
                            <label className="block text-xs font-semibold text-gray-500 mb-1">Số điện thoại *</label>
                            <input
                                type="tel"
                                value={form.senderPhone}
                                onChange={(e) => update("senderPhone", e.target.value)}
                                placeholder="0912 345 678"
                                className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#00288e]/30 focus:border-[#00288e] transition-all"
                            />
                        </div>
                        <div className="sm:col-span-2">
                            <label className="block text-xs font-semibold text-gray-500 mb-1">Địa chỉ nhận hàng *</label>
                            <input
                                type="text"
                                value={form.pickupAddress}
                                onChange={(e) => update("pickupAddress", e.target.value)}
                                placeholder="123 Đường ABC, Quận XYZ, TP.HCM"
                                className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#00288e]/30 focus:border-[#00288e] transition-all"
                            />
                        </div>
                        <div className="sm:col-span-2">
                            <label className="block text-xs font-semibold text-gray-500 mb-1">Ghi chú nhận hàng</label>
                            <input
                                type="text"
                                value={form.pickupNote}
                                onChange={(e) => update("pickupNote", e.target.value)}
                                placeholder="Ghi chú thêm cho tài xế (tùy chọn)"
                                className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#00288e]/30 focus:border-[#00288e] transition-all"
                            />
                        </div>
                    </div>
                </div>

                {/* ── Receiver Info ── */}
                <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
                    <div className="flex items-center gap-2.5 mb-4">
                        <div className="w-8 h-8 bg-blue-50 rounded-lg flex items-center justify-center">
                            <span className="material-symbols-outlined text-blue-600 text-[18px]">where_to_vote</span>
                        </div>
                        <h3 className="text-sm font-bold text-gray-800">Thông tin người nhận</h3>
                    </div>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                        <div>
                            <label className="block text-xs font-semibold text-gray-500 mb-1">Họ và tên *</label>
                            <input
                                type="text"
                                value={form.receiverName}
                                onChange={(e) => update("receiverName", e.target.value)}
                                placeholder="Trần Thị B"
                                className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#00288e]/30 focus:border-[#00288e] transition-all"
                            />
                        </div>
                        <div>
                            <label className="block text-xs font-semibold text-gray-500 mb-1">Số điện thoại *</label>
                            <input
                                type="tel"
                                value={form.receiverPhone}
                                onChange={(e) => update("receiverPhone", e.target.value)}
                                placeholder="0987 654 321"
                                className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#00288e]/30 focus:border-[#00288e] transition-all"
                            />
                        </div>
                        <div className="sm:col-span-2">
                            <label className="block text-xs font-semibold text-gray-500 mb-1">Địa chỉ giao hàng *</label>
                            <div className="flex gap-2">
                                <input
                                    type="text"
                                    value={form.destAddress}
                                    onChange={(e) => update("destAddress", e.target.value)}
                                    placeholder="456 Đường DEF, Quận UVW, Đà Nẵng"
                                    className="flex-1 px-3.5 py-2.5 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#00288e]/30 focus:border-[#00288e] transition-all"
                                />
                                <button
                                    type="button"
                                    onClick={handleGeocode}
                                    disabled={geocoding}
                                    className="px-4 py-2.5 rounded-xl bg-[#00288e] text-white text-sm font-bold hover:bg-[#001f6e] transition-all disabled:opacity-50 whitespace-nowrap"
                                >
                                    {geocoding ? (
                                        <span className="material-symbols-outlined animate-spin text-[18px]">sync</span>
                                    ) : (
                                        "Tìm vị trí"
                                    )}
                                </button>
                            </div>
                            {destResolved && (
                                <p className="text-xs text-emerald-600 font-medium mt-1.5 flex items-center gap-1">
                                    <span className="material-symbols-outlined text-[14px]">check_circle</span>
                                    Đã xác định vị trí giao hàng.
                                </p>
                            )}
                        </div>
                    </div>
                </div>

                {/* ── Cargo Info ── */}
                <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
                    <div className="flex items-center gap-2.5 mb-4">
                        <div className="w-8 h-8 bg-purple-50 rounded-lg flex items-center justify-center">
                            <span className="material-symbols-outlined text-purple-600 text-[18px]">inventory_2</span>
                        </div>
                        <h3 className="text-sm font-bold text-gray-800">Thông tin hàng hóa</h3>
                    </div>
                    <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                        <div className="sm:col-span-3">
                            <label className="block text-xs font-semibold text-gray-500 mb-1">Loại hàng *</label>
                            <input
                                type="text"
                                value={form.cargoType}
                                onChange={(e) => update("cargoType", e.target.value)}
                                placeholder="Ví dụ: Nội thất, Quần áo, Thực phẩm..."
                                className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#00288e]/30 focus:border-[#00288e] transition-all"
                            />
                        </div>
                        <div>
                            <label className="block text-xs font-semibold text-gray-500 mb-1">Cân nặng (kg) *</label>
                            <input
                                type="number"
                                min="0.1"
                                step="0.1"
                                value={form.weightKg}
                                onChange={(e) => update("weightKg", e.target.value)}
                                placeholder="0"
                                className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#00288e]/30 focus:border-[#00288e] transition-all"
                            />
                            {form.weightKg && Number(form.weightKg) > remainingWeight && (
                                <p className="text-[11px] text-amber-600 font-medium mt-1">Vượt quá dung tích còn lại ({remainingWeight} kg)</p>
                            )}
                        </div>
                        <div>
                            <label className="block text-xs font-semibold text-gray-500 mb-1">Thể tích (m³) *</label>
                            <input
                                type="number"
                                min="0.01"
                                step="0.01"
                                value={form.volumeCbm}
                                onChange={(e) => update("volumeCbm", e.target.value)}
                                placeholder="0"
                                className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#00288e]/30 focus:border-[#00288e] transition-all"
                            />
                            {form.volumeCbm && Number(form.volumeCbm) > remainingVolume && (
                                <p className="text-[11px] text-amber-600 font-medium mt-1">Vượt quá thể tích còn lại ({remainingVolume} m³)</p>
                            )}
                        </div>
                        <div>
                            <label className="block text-xs font-semibold text-gray-500 mb-1">Ghi chú đặc biệt</label>
                            <input
                                type="text"
                                value={form.specialHandlingNote}
                                onChange={(e) => update("specialHandlingNote", e.target.value)}
                                placeholder="Hàng dễ vỡ, cần cẩn thận..."
                                className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#00288e]/30 focus:border-[#00288e] transition-all"
                            />
                        </div>
                    </div>
                </div>

                {/* ── Submit Button (Fixed bottom bar) ── */}
                <div className="fixed bottom-0 left-0 right-0 bg-white/90 backdrop-blur-md border-t border-gray-100 px-4 py-3 z-30">
                    <div className="max-w-3xl mx-auto flex gap-3">
                        <button
                            onClick={onBack}
                            className="px-5 py-3 rounded-xl border border-gray-200 text-gray-600 text-sm font-bold hover:bg-gray-50 transition-all"
                        >
                            Hủy
                        </button>
                        <button
                            onClick={handleSubmit}
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
                                    Gửi đề xuất ghép chuyến
                                </>
                            )}
                        </button>
                    </div>
                </div>
            </div>
        </main>
    );
}
