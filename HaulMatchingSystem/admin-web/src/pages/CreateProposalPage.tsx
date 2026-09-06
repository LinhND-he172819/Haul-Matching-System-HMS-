import { useState } from "react";
import {
    createDraftShipment,
    geocodeAddress,
    type DraftShipmentResponse,
} from "../api/shipmentsApi";
import { createProposal } from "../api/proposalApi";
import type { PublicTripPost } from "../api/tripPostApi";
import ShipmentFormFields from "../components/ShipmentFormFields";
import type { ShipmentFormData } from "../components/ShipmentFormFields";

interface CreateProposalPageProps {
    trip: PublicTripPost;
    tripPostId: string;
    onBack: () => void;
    onLogout?: () => void;
}

/* ────────────────────────────────────────────────────── */
/*  Success Screen                                       */
/* ────────────────────────────────────────────────────── */
function SuccessScreen({ tripTitle: _tripTitle, onBack }: { tripTitle: string; onBack: () => void }) {
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
                    <span className="text-xs font-semibold text-emerald-700">Đề xuất ghép chuyến đã được gửi đến warehouse staff</span>
                </div>

                {/* Next Steps */}
                <div className="w-full text-left mt-2">
                    <h2 className="text-lg font-bold text-[#0b1c30] mb-3">Các bước tiếp theo</h2>
                    <ul className="flex flex-col gap-2.5">
                        <li className="flex items-start gap-3 p-3 rounded-xl bg-blue-50/50">
                            <span className="material-symbols-outlined text-[#00288e] text-[20px] mt-0.5">send</span>
                            <div>
                                <p className="text-sm font-bold text-[#0b1c30]">1. Đề xuất đã được gửi</p>
                                <p className="text-sm text-gray-500 mt-0.5">Warehouse staff sẽ xem và duyệt/từ chối đề xuất ghép chuyến của bạn.</p>
                            </div>
                        </li>
                        <li className="flex items-start gap-3 p-3 rounded-xl bg-blue-50/50">
                            <span className="material-symbols-outlined text-[#00288e] text-[20px] mt-0.5">inventory_2</span>
                            <div>
                                <p className="text-sm font-bold text-[#0b1c30]">2. Đóng gói hàng cẩn thận</p>
                                <p className="text-sm text-gray-500 mt-0.5">Đảm bảo hàng hóa được bảo vệ an toàn trước khi vận chuyển.</p>
                            </div>
                        </li>
                        <li className="flex items-start gap-3 p-3 rounded-xl bg-blue-50/50">
                            <span className="material-symbols-outlined text-[#00288e] text-[20px] mt-0.5">local_shipping</span>
                            <div>
                                <p className="text-sm font-bold text-[#0b1c30]">3. Giao hàng cho tài xế</p>
                                <p className="text-sm text-gray-500 mt-0.5">Tài xế sẽ đến nhận hàng trực tiếp tại địa chỉ của bạn.</p>
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

    /* ── Shared shipment form data ── */
    const [form, setForm] = useState<ShipmentFormData>({
        senderName: "",
        senderPhone: "",
        pickupAddress: "",
        receiverName: "",
        receiverPhone: "",
        destAddress: "",
        cargoType: "",
        weightKg: "",
        volumeCbm: "",
    });

    /* ── Customer-only extra fields ── */
    const [pickupNote, setPickupNote] = useState("");
    const [pickupLatitude, _setPickupLatitude] = useState("");
    const [pickupLongitude, _setPickupLongitude] = useState("");
    const [destLat, setDestLat] = useState("");
    const [destLng, setDestLng] = useState("");
    const [destResolvedName, setDestResolvedName] = useState("");
    const [specialHandlingNote, setSpecialHandlingNote] = useState("");

    const handleFieldChange = <K extends keyof ShipmentFormData>(key: K, value: string | number) => {
        setForm((prev) => ({ ...prev, [key]: value }));
        if (key === "destAddress") { setDestResolved(false); setDestResolvedName(""); }
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
            setDestLat(String(result.lat));
            setDestLng(String(result.lng));
            setDestResolvedName(result.displayName);
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
        if (!destLat || !destLng) { alert("Vui lòng xác định vị trí giao hàng trước khi gửi."); return; }

        const customerId = localStorage.getItem('userId') ?? localStorage.getItem('customerId') ?? '';

        try {
            setLoading(true);

            // Step 1: Create draft shipment
            const shipment = await createDraftShipment({
                customerId,
                senderName: form.senderName,
                senderPhone: form.senderPhone,
                pickupAddress: form.pickupAddress,
                pickupLatitude: pickupLatitude ? Number(pickupLatitude) : undefined,
                pickupLongitude: pickupLongitude ? Number(pickupLongitude) : undefined,
                pickupNote: pickupNote || undefined,
                cargoType: form.cargoType,
                weightKg: Number(form.weightKg),
                volumeCbm: Number(form.volumeCbm),
                receiverName: form.receiverName,
                receiverPhone: form.receiverPhone,
                destAddress: form.destAddress,
                destLat: Number(destLat),
                destLng: Number(destLng),
                specialHandlingNote: specialHandlingNote || undefined,
            });

            // Step 2: Create proposal
            await createProposal(tripPostId, {
                shipmentId: shipment.id,
                senderName: form.senderName,
                senderPhone: form.senderPhone,
                pickupAddress: form.pickupAddress,
                pickupLatitude: pickupLatitude ? Number(pickupLatitude) : undefined,
                pickupLongitude: pickupLongitude ? Number(pickupLongitude) : undefined,
                pickupNote: pickupNote || undefined,
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

                {/* ── Shared Form Fields ── */}
                <ShipmentFormFields
                    data={form}
                    onChange={handleFieldChange}
                    showPickupNote
                    pickupNote={pickupNote}
                    onPickupNoteChange={setPickupNote}
                    showSpecialHandlingNote
                    specialHandlingNote={specialHandlingNote}
                    onSpecialHandlingNoteChange={setSpecialHandlingNote}
                    showGeocode
                    geocoding={geocoding}
                    destResolved={destResolved}
                    onGeocode={handleGeocode}
                    remainingWeight={remainingWeight}
                    remainingVolume={remainingVolume}
                    destResolvedName={destResolvedName}
                />

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