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

export default function MatchingSuggestionPage() {
    const [data, setData] = useState<MatchingSuggestionsResponse | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [confirmOpen, setConfirmOpen] = useState(false);
    const [confirmAction, setConfirmAction] = useState<(() => Promise<void>) | null>(null);
    const [toasts, setToasts] = useState<Array<{ id: string; message: string; type: ToastType }>>([]);

    const selectedCount = selected.size;
    const totalCount = data?.shipments?.length ?? 0;

    function pushToast(message: string, type: ToastType = 'info') {
        const id = crypto.randomUUID();
        setToasts(prev => [...prev, { id, message, type }]);
    }

    useEffect(() => {
        load();

        const conn = new signalR.HubConnectionBuilder()
            .withUrl((import.meta.env.VITE_API_URL ?? 'https://localhost:7059') + '/hub/fleet')
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

                <button className="btn-ghost flex items-center gap-2 justify-center">
                    <span className="material-symbols-outlined text-base">power_settings_new</span>
                    Go Offline
                </button>
            </aside>

            <main className="grid grid-cols-1 xl:grid-cols-[minmax(0,1fr)_360px] gap-6">
                <section className="space-y-4">
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
                        <div className="map-overlay" />
                        <svg className="absolute inset-0" viewBox="0 0 800 500" preserveAspectRatio="none">
                            <path d="M120 320 L260 240 L520 260 L680 200" stroke="#4c9ef7" strokeWidth="4" fill="none" opacity="0.7" />
                            <path d="M160 380 L300 330 L520 320 L700 360" stroke="#2f80ed" strokeWidth="4" fill="none" opacity="0.7" />
                            <circle cx="260" cy="240" r="10" fill="#1b39b7" stroke="#ffffff" strokeWidth="4" />
                            <circle cx="120" cy="320" r="6" fill="#1b39b7" stroke="#ffffff" strokeWidth="3" />
                            <circle cx="680" cy="200" r="6" fill="#1b39b7" stroke="#ffffff" strokeWidth="3" />
                        </svg>
                        <div className="absolute right-6 bottom-6 flex flex-col gap-2">
                            <button className="bg-white rounded-xl p-2 shadow-md">
                                <span className="material-symbols-outlined text-[#1b39b7]">my_location</span>
                            </button>
                            <button className="bg-white rounded-xl p-2 shadow-md">+</button>
                            <button className="bg-white rounded-xl p-2 shadow-md">-</button>
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
