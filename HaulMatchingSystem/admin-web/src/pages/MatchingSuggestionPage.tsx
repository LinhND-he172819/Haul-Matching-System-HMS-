import { useEffect, useMemo, useRef, useState } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import * as signalR from '@microsoft/signalr';
import TripCapacitySummary from '../components/matching/TripCapacitySummary';
import MatchingSuggestionList from '../components/matching/MatchingSuggestionList';
import ActionsBar from '../components/matching/ActionsBar';
import ConfirmDialog from '../components/matching/ConfirmDialog';
import Toast, { type ToastType } from '../components/matching/Toast';
import {
    fetchMatchingSuggestions,
    fetchTripById,
    fetchHubById,
    postAcceptAll,
    postRejectAll,
    postAcceptSelected,
    postRejectSelected,
    type MatchingSuggestionsResponse,
    type TripResponse,
    type HubResponse
} from '../api/matchingApi';

const apiBaseUrl =
    import.meta.env.VITE_API_BASE_URL ??
    import.meta.env.VITE_API_URL ??
    'http://localhost:5104';

type RouteCoordinate = [number, number];

function parseLineStringCoordinates(routeLineString: string): RouteCoordinate[] {
    return routeLineString
        .replace(/^LINESTRING\s*\(/i, '')
        .replace(/\)$/, '')
        .split(',')
        .map((point) => point.trim().split(/\s+/).map(Number))
        .filter((point): point is RouteCoordinate => point.length === 2 && point.every(Number.isFinite));
}

function toRadians(value: number) {
    return value * (Math.PI / 180);
}

function estimateDistanceKm(routeLineString: string) {
    const coordinates = parseLineStringCoordinates(routeLineString);

    if (coordinates.length < 2) {
        return 0;
    }

    let total = 0;
    for (let index = 1; index < coordinates.length; index++) {
        const [fromLng, fromLat] = coordinates[index - 1];
        const [toLng, toLat] = coordinates[index];
        const earthRadiusKm = 6371;
        const latDelta = toRadians(toLat - fromLat);
        const lngDelta = toRadians(toLng - fromLng);
        const a =
            Math.sin(latDelta / 2) ** 2 +
            Math.cos(toRadians(fromLat)) * Math.cos(toRadians(toLat)) * Math.sin(lngDelta / 2) ** 2;

        total += 2 * earthRadiusKm * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    }

    return Math.round(total * 10) / 10;
}

function shortId(id: string) {
    return id.slice(0, 8);
}

function getRouteSummary(trip: TripResponse | null) {
    if (!trip) {
        return { distanceKm: 0, routeDistanceLabel: '0 km' };
    }

    const distanceKm = estimateDistanceKm(trip.routeLineString);
    return {
        distanceKm,
        routeDistanceLabel: `${distanceKm.toLocaleString('vi-VN')} km`
    };
}

function RouteMap({ trip, originHub, destHub }: { trip: TripResponse; originHub?: HubResponse | null; destHub?: HubResponse | null }) {
    const coordinates = useMemo(() => parseLineStringCoordinates(trip.routeLineString), [trip.routeLineString]);
    const mapContainerRef = useRef<HTMLDivElement | null>(null);
    const mapRef = useRef<L.Map | null>(null);

    useEffect(() => {
        if (!mapContainerRef.current || coordinates.length < 2) {
            return;
        }

        if (mapRef.current) {
            mapRef.current.remove();
            mapRef.current = null;
        }

        const routeLatLngs = coordinates.map(([lng, lat]) => L.latLng(lat, lng));
        const map = L.map(mapContainerRef.current, {
            scrollWheelZoom: false,
            zoomControl: false
        });
        mapRef.current = map;

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap contributors'
        }).addTo(map);

        const routeLine = L.polyline(routeLatLngs, {
            color: '#1b39b7',
            weight: 6,
            opacity: 0.92,
            lineCap: 'round',
            lineJoin: 'round'
        }).addTo(map);

        L.circleMarker(routeLatLngs[0], {
            radius: 8,
            color: '#ffffff',
            fillColor: '#14b8a6',
            fillOpacity: 1,
            weight: 3
        })
            .bindTooltip(originHub?.name ?? 'Điểm xuất phát')
            .addTo(map);

        L.circleMarker(routeLatLngs[routeLatLngs.length - 1], {
            radius: 8,
            color: '#ffffff',
            fillColor: '#f97316',
            fillOpacity: 1,
            weight: 3
        })
            .bindTooltip(destHub?.name ?? 'Điểm đến')
            .addTo(map);

        map.fitBounds(routeLine.getBounds(), {
            padding: [28, 28],
            maxZoom: 12
        });

        window.setTimeout(() => map.invalidateSize(), 0);

        return () => {
            map.remove();
            mapRef.current = null;
        };
    }, [coordinates, destHub?.name, originHub?.name]);

    if (coordinates.length < 2) {
        return (
            <div className="absolute inset-0 flex items-center justify-center bg-surface-container-low text-center px-6">
                <div>
                    <span className="material-symbols-outlined text-[36px] text-on-surface-variant">route</span>
                    <p className="mt-2 text-label-lg font-bold text-on-surface">Không có dữ liệu tuyến đường</p>
                    <p className="mt-1 text-body-md text-on-surface-variant">RouteLineString chưa đủ điểm để vẽ bản đồ.</p>
                </div>
            </div>
        );
    }

    return <div ref={mapContainerRef} className="absolute inset-0 z-0" />;
}

interface MatchingSuggestionPageProps {
    onBackToAdmin?: () => void;
    onLogout?: () => void;
}

interface TripContext {
    trip: TripResponse;
    originHub: HubResponse | null;
    destHub: HubResponse | null;
}

export default function MatchingSuggestionPage({ onBackToAdmin, onLogout }: MatchingSuggestionPageProps) {
    const [data, setData] = useState<MatchingSuggestionsResponse | null>(null);
    const [tripContext, setTripContext] = useState<TripContext | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [confirmOpen, setConfirmOpen] = useState(false);
    const [confirmAction, setConfirmAction] = useState<(() => Promise<void>) | null>(null);
    const [toasts, setToasts] = useState<Array<{ id: string; message: string; type: ToastType }>>([]);

    const selectedCount = selected.size;
    const totalCount = data?.shipments?.length ?? 0;
    const accessToken = localStorage.getItem('accessToken');
    const currentRole = localStorage.getItem('role');

    function pushToast(message: string, type: ToastType = 'info') {
        const id = crypto.randomUUID();
        setToasts(prev => [...prev, { id, message, type }]);
    }

    useEffect(() => {
        void load();

        if (!accessToken || currentRole !== 'Driver') {
            return;
        }

        const conn = new signalR.HubConnectionBuilder()
            .withUrl(apiBaseUrl + '/hub/fleet', {
                accessTokenFactory: () => localStorage.getItem('accessToken') || ''
            })
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
    }, [accessToken, currentRole]);

    async function load() {
        setLoading(true);
        setError(null);
        try {
            const res = await fetchMatchingSuggestions();
            setData(res);
            setSelected(new Set());

            if (res?.tripId) {
                const trip = await fetchTripById(res.tripId);
                const [originHub, destHub] = await Promise.all([
                    fetchHubById(trip.originHubId).catch(() => null),
                    fetchHubById(trip.destHubId).catch(() => null)
                ]);

                setTripContext({ trip, originHub, destHub });
            } else {
                setTripContext(null);
                setError(null);
            }
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

    const routeSummary = getRouteSummary(tripContext?.trip ?? null);

    return (
        <div className="min-h-screen bg-surface text-on-surface lg:flex lg:overflow-hidden">
            <aside className="hidden lg:flex w-64 flex-col border-r border-outline-variant bg-surface p-4 shadow-sm">
                <div className="mb-8 px-2">
                    <h1 className="text-[24px] font-black text-primary">Cổng tài xế</h1>
                    <p className="mt-1 text-sm text-on-surface-variant">ID: LOG-8842</p>
                </div>

                <div className="flex-1 space-y-2">
                    <button className="flex w-full items-center gap-3 rounded-xl bg-primary-container px-4 py-3 text-left text-on-primary-container font-semibold">
                        <span className="material-symbols-outlined" style={{ fontVariationSettings: "'FILL' 1" }}>map</span>
                        <span>Bản đồ tuyến</span>
                    </button>
                    <button className="flex w-full items-center gap-3 rounded-xl px-4 py-3 text-left text-on-surface-variant hover:bg-surface-container-low">
                        <span className="material-symbols-outlined">package_2</span>
                        <span>Kiện hàng</span>
                    </button>
                    <button className="flex w-full items-center gap-3 rounded-xl px-4 py-3 text-left text-on-surface-variant hover:bg-surface-container-low">
                        <span className="material-symbols-outlined">monitoring</span>
                        <span>Sức chứa</span>
                    </button>
                    <button className="flex w-full items-center gap-3 rounded-xl px-4 py-3 text-left text-on-surface-variant hover:bg-surface-container-low">
                        <span className="material-symbols-outlined">report_problem</span>
                        <span>Sự cố</span>
                    </button>
                    <button className="flex w-full items-center gap-3 rounded-xl px-4 py-3 text-left text-on-surface-variant hover:bg-surface-container-low">
                        <span className="material-symbols-outlined">settings</span>
                        <span>Cài đặt</span>
                    </button>
                </div>

                <div className="mt-auto border-t border-outline-variant pt-4">
                    <button
                        onClick={onLogout}
                        className="flex w-full items-center justify-center gap-2 rounded-xl border border-outline px-4 py-2.5 text-sm font-semibold text-on-surface hover:bg-surface-container"
                    >
                        <span className="material-symbols-outlined text-[18px]">power_settings_new</span>
                        Ngắt kết nối
                    </button>
                </div>
            </aside>

            <div className="flex min-h-screen flex-1 flex-col overflow-hidden">
                <header className="flex items-center justify-between border-b border-outline-variant bg-surface px-4 py-3 lg:hidden">
                    <h1 className="text-[20px] font-bold text-primary">Ghép Chuyến Logistics</h1>
                    <div className="flex gap-3 text-on-surface-variant">
                        <span className="material-symbols-outlined">notifications</span>
                        <span className="material-symbols-outlined">account_circle</span>
                    </div>
                </header>

                <main className="flex-1 overflow-hidden lg:flex lg:flex-row">
                    <section className="relative flex-1 border-b border-outline-variant lg:border-b-0 lg:border-r">
                        <div className="absolute inset-0 bg-surface-container-low">
                            {tripContext?.trip ? (
                                <RouteMap trip={tripContext.trip} originHub={tripContext.originHub} destHub={tripContext.destHub} />
                            ) : (
                                <div className="absolute inset-0 flex items-center justify-center text-on-surface-variant">
                                    <div className="text-center">
                                        <span className="material-symbols-outlined text-[40px]">route</span>
                                        <p className="mt-2 text-sm">Đang tải tuyến đường...</p>
                                    </div>
                                </div>
                            )}

                            <div className="map-overlay pointer-events-none absolute inset-0" />

                            <div className="absolute left-0 top-0 w-full bg-gradient-to-b from-surface/95 to-transparent p-4 lg:p-6 pointer-events-none">
                                <div className="pointer-events-auto flex flex-wrap gap-4 max-w-4xl">
                                    <div className="min-w-[260px] flex-1 rounded-2xl border border-outline-variant/50 bg-surface p-4 shadow-sm">
                                        <div className="mb-3 flex items-center gap-2">
                                            <span className="material-symbols-outlined text-primary">route</span>
                                            <h2 className="text-base font-semibold text-on-surface">Chuyến Đi Hiện Tại</h2>
                                        </div>
                                        <div className="space-y-3">
                                            <div className="flex items-start gap-3">
                                                <div className="flex flex-col items-center pt-1">
                                                    <div className="h-3 w-3 rounded-full border-2 border-primary" />
                                                    <div className="my-1 h-7 w-px bg-outline-variant" />
                                                    <div className="h-3 w-3 rounded-full border-2 border-secondary bg-secondary" />
                                                </div>
                                                <div className="space-y-1">
                                                    <p className="text-sm text-on-surface-variant">{tripContext?.originHub?.name ?? tripContext?.trip.originHubId ?? 'Điểm xuất phát'}</p>
                                                    <p className="text-sm font-semibold text-on-surface">{tripContext?.destHub?.name ?? tripContext?.trip.destHubId ?? 'Điểm đến'}</p>
                                                </div>
                                            </div>
                                            <div className="text-sm text-on-surface-variant">
                                                Chuyến: {tripContext?.trip.id ? shortId(tripContext.trip.id) : '--'}
                                            </div>
                                        </div>
                                    </div>

                                    <div className="flex min-w-[180px] flex-col justify-center rounded-2xl border border-outline-variant/50 bg-surface p-4 shadow-sm">
                                        <p className="text-xs uppercase tracking-[0.18em] text-on-surface-variant">Khoảng cách</p>
                                        <p className="mt-1 text-2xl font-bold text-primary">{routeSummary.routeDistanceLabel}</p>
                                        <div className="mt-2 inline-flex w-fit items-center gap-1 rounded-full bg-surface-container px-3 py-1 text-xs font-semibold text-on-surface">
                                            <span className="material-symbols-outlined text-[14px]">schedule</span>
                                            Dữ liệu từ chuyến đi
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <div className="absolute bottom-6 right-6 z-20 flex flex-col gap-2">
                                <button className="flex h-10 w-10 items-center justify-center rounded-lg border border-outline-variant/50 bg-surface text-on-surface shadow-sm hover:bg-surface-container">
                                    <span className="material-symbols-outlined">my_location</span>
                                </button>
                                <button className="flex h-10 w-10 items-center justify-center rounded-lg border border-outline-variant/50 bg-surface text-on-surface shadow-sm hover:bg-surface-container">
                                    <span className="material-symbols-outlined">add</span>
                                </button>
                                <button className="flex h-10 w-10 items-center justify-center rounded-lg border border-outline-variant/50 bg-surface text-on-surface shadow-sm hover:bg-surface-container">
                                    <span className="material-symbols-outlined">remove</span>
                                </button>
                            </div>
                        </div>
                    </section>

                    <aside className="flex h-[calc(100vh-64px)] flex-col bg-surface-container-low lg:h-screen lg:w-[400px] lg:min-w-[400px]">
                        <div className="shrink-0 border-b border-outline-variant/50 bg-surface p-5">
                            <div className="flex items-start justify-between gap-3">
                                <div>
                                    <h2 className="text-[20px] font-semibold text-on-surface">Quản Lý Ghép Chuyến</h2>
                                    <p className="mt-1 text-sm text-on-surface-variant">Theo dõi đề xuất phù hợp cho tài xế</p>
                                </div>
                                {onBackToAdmin && (
                                    <button
                                        onClick={onBackToAdmin}
                                        className="rounded-lg border border-outline-variant px-3 py-2 text-xs font-semibold text-on-surface hover:bg-surface-container"
                                    >
                                        Bảng quản trị
                                    </button>
                                )}
                            </div>

                            {loading && <div className="mt-4 rounded-xl bg-surface-container-low px-4 py-3 text-sm text-on-surface-variant">Đang tải...</div>}
                            {error && <div className="mt-4 rounded-xl bg-error-container px-4 py-3 text-sm text-on-error-container">{error}</div>}
                            {!loading && !error && !data && (
                                <div className="mt-4 rounded-xl border border-outline-variant bg-surface-container-low px-4 py-3 text-sm text-on-surface-variant">
                                    Chưa có chuyến active cho tài xế này. Hãy tạo hoặc kích hoạt một chuyến để xem gợi ý ghép.
                                </div>
                            )}
                        </div>

                        <div className="flex-1 overflow-y-auto p-5">
                            {data && (
                                <div className="space-y-5">
                                    <TripCapacitySummary
                                        currentWeight={data.currentLoadWeight}
                                        currentVolume={data.currentLoadVolume}
                                        remainingWeight={data.remainingWeightCapacity}
                                        remainingVolume={data.remainingVolumeCapacity}
                                    />

                                    <div className="rounded-2xl border border-outline-variant/50 bg-surface p-4 shadow-sm">
                                        <div className="flex items-end justify-between gap-3">
                                            <div>
                                                <h3 className="text-sm font-semibold text-on-surface">Kiện Hàng Đề Xuất</h3>
                                                
                                            </div>
                                            <span className="rounded-full bg-surface-variant px-3 py-1 text-xs font-semibold text-on-surface-variant">{totalCount} tìm thấy</span>
                                        </div>

                                        <div className="mt-4 space-y-4 max-h-[440px] overflow-auto pr-1 custom-scrollbar">
                                            <MatchingSuggestionList
                                                shipments={data.shipments}
                                                selectedIds={selected}
                                                onSelect={handleSelect}
                                                onAccept={handleAcceptOne}
                                                onReject={handleRejectOne}
                                            />
                                        </div>
                                    </div>

                                    <ActionsBar
                                        onAcceptAll={handleAcceptAll}
                                        onRejectAll={handleRejectAll}
                                        onAcceptSelected={handleAcceptSelected}
                                        onRejectSelected={handleRejectSelected}
                                        selectedCount={selectedCount}
                                    />
                                </div>
                            )}
                            {!loading && !data && !error && (
                                <div className="rounded-2xl border border-dashed border-outline-variant bg-surface p-5 text-sm text-on-surface-variant">
                                    Không có dữ liệu gợi ý để hiển thị.
                                </div>
                            )}
                        </div>
                    </aside>
                </main>
            </div>

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


