import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import TripCapacitySummary from '../components/matching/TripCapacitySummary';
import MatchingSuggestionList from '../components/matching/MatchingSuggestionList';
import ActionsBar from '../components/matching/ActionsBar';
import ConfirmDialog from '../components/matching/ConfirmDialog';
import Toast, { type ToastType } from '../components/matching/Toast';
import {
    fetchMatchingSuggestions,
    postAcceptAll,
    postRejectAll,
    postAcceptSelected,
    postRejectSelected,
    type MatchingSuggestionsResponse
} from '../api/matchingApi';
import { offlineDb } from '../services/offlineDb';

const apiBaseUrl =
    import.meta.env.VITE_API_BASE_URL ??
    import.meta.env.VITE_API_URL ??
    'http://localhost:5104';

interface MatchingSuggestionPageProps {
    onBackToAdmin?: () => void;
    onLogout?: () => void;
}

export default function MatchingSuggestionPage({ onBackToAdmin, onLogout }: MatchingSuggestionPageProps) {
    const [data, setData] = useState<MatchingSuggestionsResponse | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [confirmOpen, setConfirmOpen] = useState(false);
    const [confirmAction, setConfirmAction] = useState<(() => Promise<void>) | null>(null);
    const [toasts, setToasts] = useState<Array<{ id: string; message: string; type: ToastType }>>([]);

    const selectedCount = selected.size;
    const totalCount = data?.shipments?.length ?? 0;

    // --- CÁC STATE GIẢ LẬP ĐỂ DEMO ---
    const [isDriving, setIsDriving] = useState(false);
    const [isMockOffline, setIsMockOffline] = useState(false);
    const [gps, setGps] = useState({ lat: 10.7800, lng: 106.7000 });
    const [syncLog, setSyncLog] = useState<string[]>([]);
    
    // Hàm thêm log màn hình cho dễ nhìn lúc Demo
    const addLog = (msg: string) => setSyncLog(prev => [`[${new Date().toLocaleTimeString()}] ${msg}`, ...prev.slice(0, 5)]);

    // --- LUỒNG HOẠT ĐỘNG GIẢ LẬP GPS ---
    useEffect(() => {
        let interval: any;
        if (isDriving) {
            interval = setInterval(async () => {
                // Giả lập xe nhích tọa độ tiến lên
                const nextGps = { lat: gps.lat + 0.0004, lng: gps.lng + 0.0002 };
                setGps(nextGps);

                const mockPayload = {
                    idempotencyKey: `sim-key-${crypto.randomUUID()}`, // Dùng luôn hàm UUID có sẵn của trình duyệt
                    actionType: 'GpsPing',
                    payload: { tripId: "3fa85f64-5717-4562-b3fc-2c963f66afa6", lat: nextGps.lat, lng: nextGps.lng },
                    deviceTimestamp: new Date().toISOString()
                };

                if (isMockOffline) {
                    // TÌNH HUỐNG OFFLINE (Task EX-04): Lưu thẳng vào Dexie.js thông qua offlineDb
                    try {
                        await offlineDb.table('offlineActions').add(mockPayload);
                        addLog("📦 Mất mạng! Đã găm 1 tọa độ GPS vào Dexie.js");
                    } catch (e) {
                        console.error("Lỗi ghi Dexie:", e);
                    }
                } else {
                    // TÌNH HUỐNG ONLINE: Tương lai sẽ gọi SignalR đẩy lên Server tại đây
                    addLog("🚀 Online! Đã gửi tọa độ GPS trực tiếp lên Server");
                }
            }, 3000); // Cứ 3 giây sinh 1 tọa độ
        }
        return () => clearInterval(interval);
    }, [isDriving, isMockOffline, gps]);

    // --- LUỒNG TỰ ĐỘNG ĐỒNG BỘ KHI CÓ MẠNG LẠI (Task EX-04c) ---
    useEffect(() => {
        if (!isMockOffline) {
            const triggerAutoSync = async () => {
                try {
                    const records = await offlineDb.table('offlineActions').toArray();
                    if (records.length > 0) {
                        addLog(`🔄 Có mạng lại! Đang đẩy ${records.length} gói tin từ Dexie lên Server...`);
                        
                        // Giả lập hoặc gọi API POST thật của bạn tại đây:
                        // await axios.post('/api/transport/sync-offline', records);
                        
                        await offlineDb.table('offlineActions').clear();
                        addLog("✅ Đồng bộ thành công! Bộ nhớ Dexie.js đã trống.");
                    }
                } catch (e) {
                    console.error("Lỗi đồng bộ Dexie:", e);
                }
            };
            triggerAutoSync();
        }
    }, [isMockOffline]);
    /// hết demo GPS

    function pushToast(message: string, type: ToastType = 'info') {
        const id = crypto.randomUUID();
        setToasts(prev => [...prev, { id, message, type }]);
    }

    useEffect(() => {
        load();

        const conn = new signalR.HubConnectionBuilder()
            .withUrl(apiBaseUrl + '/hub/fleet')
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        conn.on('NewMatchingSuggestion', () => {
            // simple: re-fetch list
            load();
            pushToast('Có gợi ý ghép chuyến mới', 'info');
        });

        conn.start().catch(err => console.error('Hub start error', err));
        return () => { conn.stop(); };
    }, []);

    async function load() {
        setLoading(true);
        setError(null);
        try {
            const res = await fetchMatchingSuggestions();
            setData(res);
            setSelected(new Set());
        } catch (ex: any) {
            const msg = ex?.message ?? 'Lỗi tải dữ liệu';
            setError(msg);
            pushToast(msg, 'error');
        } finally { setLoading(false); }
    }

    function handleSelect(id: string, sel: boolean) {
        setSelected(prev => {
            const next = new Set(prev);
            if (sel) next.add(id); else next.delete(id);
            return next;
        });
    }

    async function handleAcceptAll() {
        setConfirmAction(() => async () => {
            await postAcceptAll();
            await load();
            pushToast('Đã xác nhận nhận hàng', 'success');
        });
        setConfirmOpen(true);
    }

    async function handleRejectAll() {
        setConfirmAction(() => async () => {
            await postRejectAll();
            await load();
            pushToast('Đã từ chối tất cả', 'success');
        });
        setConfirmOpen(true);
    }

    async function handleAcceptSelected() {
        const ids = Array.from(selected);
        setConfirmAction(() => async () => {
            await postAcceptSelected(ids);
            await load();
            pushToast(`Đã chọn ghép ${ids.length} kiện`, 'success');
        });
        setConfirmOpen(true);
    }

    async function handleRejectSelected() {
        const ids = Array.from(selected);
        setConfirmAction(() => async () => {
            await postRejectSelected(ids);
            await load();
            pushToast(`Đã từ chối ${ids.length} kiện`, 'success');
        });
        setConfirmOpen(true);
    }

    async function handleAcceptOne(id: string) {
        try {
            await postAcceptSelected([id]);
            await load();
            pushToast('Đã chọn ghép 1 kiện', 'success');
        } catch (e: any) {
            pushToast(e?.message ?? 'Không thể chọn ghép', 'error');
        }
    }

    async function handleRejectOne(id: string) {
        try {
            await postRejectSelected([id]);
            await load();
            pushToast('Đã từ chối 1 kiện', 'success');
        } catch (e: any) {
            pushToast(e?.message ?? 'Không thể từ chối', 'error');
        }
    }

    return (
        <div className="app-shell">
            <aside className="panel flex flex-col justify-between p-6">
                <div>
                    <div className="mb-8">
                        <div className="text-xl font-semibold text-[#1b2b65]">Driver Portal</div>
                        <div className="text-xs text-slate-500">ID: LOG-8842</div>
                    </div>

                    <nav className="space-y-3 text-sm font-semibold">
                        {onBackToAdmin && (
                            <button
                                onClick={onBackToAdmin}
                                className="w-full flex items-center gap-3 px-4 py-3 rounded-xl border border-dashed border-[#1b39b7] text-[#1b39b7] hover:bg-white transition-all mb-4"
                            >
                                <span className="material-symbols-outlined text-lg">admin_panel_settings</span>
                                Admin Console
                            </button>
                        )}
                        {[
                            { label: 'Route Map', icon: 'map' },
                            { label: 'Packages', icon: 'inventory_2' },
                            { label: 'Capacity', icon: 'speed' },
                            { label: 'Incidents', icon: 'report_problem' },
                            { label: 'Settings', icon: 'settings' }
                        ].map((item, idx) => (
                            <button
                                key={item.label}
                                className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl border ${idx === 0 ? 'bg-[#1b39b7] text-white border-transparent' : 'border-transparent text-slate-600 hover:bg-white'}`}
                            >
                                <span className="material-symbols-outlined text-lg">{item.icon}</span>
                                {item.label}
                            </button>
                        ))}
                    </nav>
                </div>

                <button 
                    onClick={onLogout}
                    className="btn-ghost flex items-center gap-2 justify-center w-full"
                >
                    <span className="material-symbols-outlined text-base">power_settings_new</span>
                    Go Offline (Đăng xuất)
                </button>
            </aside>

            <main className="grid grid-cols-1 xl:grid-cols-[minmax(0,1fr)_360px] gap-6">
                <section className="space-y-4">
                    {/* 🚨 HIỂN THỊ OFFLINE BADGE (Yêu cầu Task EX-04) */}
                    {isMockOffline && (
                        <div className="bg-red-600 text-white p-3 rounded-xl font-bold flex items-center gap-2 animate-pulse shadow-sm text-sm">
                            <span className="material-symbols-outlined text-base">wifi_off</span>
                            ỨNG DỤNG ĐANG CHẠY Ở CHẾ ĐỘ NGOẠI TUYẾN (OFFLINE MODE)
                        </div>
                    )}
                    {/* 🛠️ PANEL ĐIỀU KHIỂN GIẢ LẬP CHO BUỔI DEMO */}
                    <div className="p-4 bg-white rounded-xl border border-slate-200 shadow-sm space-y-3">
                        <div className="text-sm font-bold text-[#1b39b7] flex items-center gap-2">
                            <span className="material-symbols-outlined text-base">tune</span> Bảng điều khiển Giả lập GPS & Mạng ngoại tuyến
                        </div>
                        <div className="flex gap-3">
                            <button 
                                onClick={() => setIsDriving(!isDriving)}
                                className={`px-4 py-1.5 rounded-lg text-xs font-bold text-white shadow-sm transition-all ${isDriving ? 'bg-red-500' : 'bg-emerald-600'}`}
                            >
                                {isDriving ? "🛑 Dừng xe tải" : "🚚 Bắt đầu di chuyển"}
                            </button>
                            <button 
                                disabled={!isDriving}
                                onClick={() => setIsMockOffline(!isMockOffline)}
                                className={`px-4 py-1.5 rounded-lg text-xs font-bold text-white shadow-sm transition-all disabled:opacity-40 ${isMockOffline ? 'bg-teal-600' : 'bg-amber-500'}`}
                            >
                                {isMockOffline ? "📶 Khôi phục mạng" : "🚇 Đi vào hầm (Ngắt mạng)"}
                            </button>
                        </div>
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-3 pt-1">
                            <div className="text-xs font-mono bg-slate-900 text-emerald-400 p-2 rounded-lg">
                                GPS: {gps.lat.toFixed(5)} , {gps.lng.toFixed(5)}
                            </div>
                            <div className="text-[11px] font-mono bg-slate-100 p-2 rounded-lg text-slate-600 h-8 overflow-hidden whitespace-nowrap text-ellipsis">
                                {syncLog[0] ?? "Chưa có hoạt động log..."}
                            </div>
                        </div>
                    </div>
                    {/* --- END DEMO PANEL --- */}
                    <div className="grid grid-cols-1 md:grid-cols-[minmax(0,1.4fr)_minmax(0,0.6fr)] gap-4">
                        <div className="soft-card p-4 flex items-center justify-between">
                            <div>
                                <div className="flex items-center gap-2 text-sm font-semibold text-slate-700">
                                    <span className="material-symbols-outlined text-base text-[#1b39b7]">fork_right</span>
                                    Chuyến Đi Hiện Tại
                                </div>
                                <div className="mt-2 text-xs text-slate-500">Kho Gò Vấp, HCMC</div>
                                <div className="mt-2 flex items-center gap-3 text-sm">
                                    <span className="h-2 w-2 rounded-full bg-[#1b39b7]" />
                                    <span className="text-slate-600">KCN Tân Bình, HCMC</span>
                                </div>
                            </div>
                            <div className="text-right text-xs text-slate-500">Đang di chuyển</div>
                        </div>

                        <div className="soft-card p-4 flex flex-col gap-3">
                            <div className="text-xs uppercase tracking-widest text-slate-500">Khoảng cách</div>
                            <div className="text-2xl font-semibold text-[#1b39b7]">12.5 km</div>
                            <div className="pill">
                                <span className="material-symbols-outlined text-sm">schedule</span>
                                ~28 phút
                            </div>
                        </div>
                    </div>

                    <div className="map-panel h-[560px]">
                        {/* Interactive Google Map */}
                        <iframe 
                            src="https://maps.google.com/maps?q=Go%20Vap,%20Ho%20Chi%20Minh,%20Vietnam&t=&z=13&ie=UTF8&iwloc=&output=embed" 
                            className="w-full h-full border-0 absolute inset-0 z-0" 
                            allowFullScreen 
                            loading="lazy"
                            title="Google Maps Route"
                        ></iframe>

                        {/* Translucent overlay matching dashboard color scheme */}
                        <div className="map-overlay pointer-events-none z-10 opacity-30" />

                        {/* Routing overlay */}
                        <svg className="absolute inset-0 z-20 pointer-events-none" viewBox="0 0 800 500" preserveAspectRatio="none">
                            <path d="M120 320 L260 240 L520 260 L680 200" stroke="#1b39b7" strokeWidth="6" fill="none" opacity="0.8" strokeLinecap="round" />
                            <path d="M160 380 L300 330 L520 320 L700 360" stroke="#4c9ef7" strokeWidth="4" fill="none" opacity="0.6" strokeLinecap="round" strokeDasharray="5,5" />
                            <circle cx="260" cy="240" r="10" fill="#1b39b7" stroke="#ffffff" strokeWidth="4" />
                            <circle cx="120" cy="320" r="6" fill="#1b39b7" stroke="#ffffff" strokeWidth="3" />
                            <circle cx="680" cy="200" r="6" fill="#1b39b7" stroke="#ffffff" strokeWidth="3" />
                        </svg>

                        {/* Navigation controls helper */}
                        <div className="absolute right-6 bottom-6 flex flex-col gap-2 z-30 pointer-events-none">
                            <div className="bg-white rounded-xl px-3 py-1.5 shadow-md text-[10px] font-bold text-primary flex items-center gap-1 border border-outline-variant/30">
                                <span className="material-symbols-outlined text-xs">navigation</span> Interactive Google Map
                            </div>
                        </div>
                    </div>
                </section>

                <section className="panel p-5 flex flex-col gap-4">
                    <div className="flex items-center justify-between">
                        <div>
                            <div className="text-lg font-semibold">Quản Lý Ghép Chuyến</div>
                            <div className="text-xs text-slate-500">Theo dõi đề xuất phù hợp cho tài xế</div>
                        </div>
                    </div>

                    {loading && <div className="p-3 text-sm">Đang tải...</div>}
                    {error && <div className="p-3 text-sm text-error">{error}</div>}

                    {data && (
                        <>
                            <TripCapacitySummary
                                currentWeight={data.currentLoadWeight}
                                currentVolume={data.currentLoadVolume}
                                remainingWeight={data.remainingWeightCapacity}
                                remainingVolume={data.remainingVolumeCapacity}
                            />

                            <div className="flex items-center justify-between">
                                <div className="text-sm font-semibold">Kiện Hàng Đề Xuất</div>
                                <span className="pill">{totalCount} tìm thấy</span>
                            </div>

                            <div className="space-y-4 max-h-[420px] overflow-auto pr-1 custom-scrollbar">
                                <MatchingSuggestionList
                                    shipments={data.shipments}
                                    selectedIds={selected}
                                    onSelect={handleSelect}
                                    onAccept={handleAcceptOne}
                                    onReject={handleRejectOne}
                                />
                            </div>

                            <ActionsBar
                                onAcceptAll={handleAcceptAll}
                                onRejectAll={handleRejectAll}
                                onAcceptSelected={handleAcceptSelected}
                                onRejectSelected={handleRejectSelected}
                                selectedCount={selectedCount}
                            />
                        </>
                    )}
                </section>
            </main>

            <ConfirmDialog title="Xác nhận hành động" open={confirmOpen} onConfirm={async () => { setConfirmOpen(false); if (confirmAction) await confirmAction(); }} onCancel={() => setConfirmOpen(false)}>
                Bạn có chắc muốn thực hiện hành động này?
            </ConfirmDialog>

            <div className="fixed right-6 top-6 z-50 flex flex-col gap-3">
                {toasts.map(t => (
                    <Toast key={t.id} message={t.message} type={t.type} onClose={() => setToasts(prev => prev.filter(x => x.id !== t.id))} />
                ))}
            </div>
        </div>
    );
}


