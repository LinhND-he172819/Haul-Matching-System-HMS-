import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { createHub, fetchHubs, updateHub, type Hub, type HubPayload } from '../api/hubsApi';

type HubFormState = {
    name: string;
    address: string;
    latitude: string;
    longitude: string;
};

type NominatimPlace = {
    place_id: number;
    display_name: string;
    lat: string;
    lon: string;
    type?: string;
};

const defaultCenter = {
    latitude: 10.7769,
    longitude: 106.7009
};

const emptyForm: HubFormState = {
    name: '',
    address: '',
    latitude: '',
    longitude: ''
};

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5104';

function toCoordinate(value: string, fallback: number) {
    const parsed = Number(value);

    return Number.isFinite(parsed) ? parsed : fallback;
}

function formatCoordinate(value: number) {
    return value.toFixed(6);
}

function formatDateTime(value: string) {
    return new Date(value).toLocaleString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

interface AdminHubsPageProps {
    sidebar?: React.ReactNode;
}

export default function AdminHubsPage({ sidebar: _sidebar }: AdminHubsPageProps) {
    const [hubs, setHubs] = useState<Hub[]>([]);
    const [search, setSearch] = useState('');
    const [form, setForm] = useState<HubFormState>(emptyForm);
    const [editingHubId, setEditingHubId] = useState<string | null>(null);
    const [locationConfirmed, setLocationConfirmed] = useState(false);
    const [flyToVersion, setFlyToVersion] = useState(0);
    const [places, setPlaces] = useState<NominatimPlace[]>([]);
    const [isLoadingHubs, setIsLoadingHubs] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [isSearchingPlaces, setIsSearchingPlaces] = useState(false);
    const [status, setStatus] = useState('Sẵn sàng');
    const [message, setMessage] = useState(`Kết nối với ${apiBaseUrl}/api/hubs`);

    const latitude = toCoordinate(form.latitude, defaultCenter.latitude);
    const longitude = toCoordinate(form.longitude, defaultCenter.longitude);
    const selectedHub = useMemo(
        () => hubs.find((hub) => hub.id === editingHubId) ?? null,
        [editingHubId, hubs]
    );

    const loadHubs = useCallback(async (query = search) => {
        try {
            setIsLoadingHubs(true);
            const data = await fetchHubs(query);
            setHubs(data);
            setStatus('Đã kết nối');
            setMessage(`${data.length} hub đã tải`);
        } catch (error) {
            console.error(error);
            setHubs([]);
            setStatus('API ngoại tuyến');
            setMessage(error instanceof Error ? error.message : 'Không thể tải danh sách hub.');
        } finally {
            setIsLoadingHubs(false);
        }
    }, [search]);

    useEffect(() => {
        const timer = window.setTimeout(() => {
            void loadHubs(search);
        }, 250);

        return () => window.clearTimeout(timer);
    }, [loadHubs, search]);

    useEffect(() => {
        if (form.address.trim().length < 3) {
            setPlaces([]);
            return;
        }

        const controller = new AbortController();
        const timer = window.setTimeout(async () => {
            try {
                setIsSearchingPlaces(true);
                const params = new URLSearchParams({
                    format: 'jsonv2',
                    q: form.address.trim(),
                    limit: '5',
                    addressdetails: '1',
                    countrycodes: 'vn'
                });

                const response = await fetch(`https://nominatim.openstreetmap.org/search?${params}`, {
                    signal: controller.signal
                });

                if (!response.ok) {
                    throw new Error(`Nominatim returned ${response.status}`);
                }

                const data = await response.json() as NominatimPlace[];
                setPlaces(data);
            } catch (error) {
                if (!controller.signal.aborted) {
                    console.error(error);
                    setPlaces([]);
                }
            } finally {
                if (!controller.signal.aborted) {
                    setIsSearchingPlaces(false);
                }
            }
        }, 450);

        return () => {
            controller.abort();
            window.clearTimeout(timer);
        };
    }, [form.address]);

    const updateForm = (field: keyof HubFormState, value: string) => {
        setForm((current) => ({ ...current, [field]: value }));

        if (field === 'address') {
            setLocationConfirmed(false);
        }
    };

    const selectPlace = (place: NominatimPlace) => {
        setForm((current) => ({
            ...current,
            address: place.display_name,
            latitude: formatCoordinate(Number(place.lat)),
            longitude: formatCoordinate(Number(place.lon))
        }));
        setPlaces([]);
        setLocationConfirmed(false);
        setFlyToVersion((current) => current + 1);
        setStatus('Đã chọn địa điểm');
        setMessage('Kiểm tra ghim hoặc kéo điều chỉnh trước khi xác nhận.');
    };

    const handleMapLocationChange = useCallback((nextLatitude: number, nextLongitude: number) => {
        setForm((current) => ({
            ...current,
            latitude: formatCoordinate(nextLatitude),
            longitude: formatCoordinate(nextLongitude)
        }));
        setLocationConfirmed(false);
        setStatus('Đã di chuyển ghim');
        setMessage('Xác nhận vị trí hub đã điều chỉnh trước khi lưu.');
    }, []);

    const confirmLocation = () => {
        if (!form.latitude || !form.longitude) {
            setStatus('Thiếu vị trí');
            setMessage('Tìm kiếm địa chỉ hoặc kéo ghim trước.');
            return;
        }

        setLocationConfirmed(true);
        setStatus('Đã xác nhận vị trí');
        setMessage(`${form.latitude}, ${form.longitude}`);
    };

    const editHub = (hub: Hub) => {
        setEditingHubId(hub.id);
        setForm({
            name: hub.name,
            address: hub.address,
            latitude: formatCoordinate(hub.latitude),
            longitude: formatCoordinate(hub.longitude)
        });
        setLocationConfirmed(true);
        setPlaces([]);
        setFlyToVersion((current) => current + 1);
        setStatus('Đang chỉnh sửa hub');
        setMessage(hub.name);
    };

    const resetForm = () => {
        setEditingHubId(null);
        setForm(emptyForm);
        setPlaces([]);
        setLocationConfirmed(false);
        setFlyToVersion((current) => current + 1);
        setStatus('Sẵn sàng');
        setMessage('Tạo hub mới hoặc chọn hub để chỉnh sửa.');
    };
    
    const buildPayload = (): HubPayload | null => {
        const latitudeValue = Number(form.latitude);
        const longitudeValue = Number(form.longitude);

        if (!form.name.trim()) {
            setStatus('Lỗi xác thực');
            setMessage('Tên hub là bắt buộc.');
            return null;
        }

        if (!form.address.trim()) {
            setStatus('Lỗi xác thực');
            setMessage('Địa chỉ là bắt buộc.');
            return null;
        }

        if (!Number.isFinite(latitudeValue) || !Number.isFinite(longitudeValue)) {
            setStatus('Lỗi xác thực');
            setMessage('Tọa độ hub là bắt buộc.');
            return null;
        }

        if (!locationConfirmed) {
            setStatus('Chưa xác nhận vị trí');
            setMessage('Xác nhận ghim trên bản đồ trước khi lưu.');
            return null;
        }

        return {
            name: form.name.trim(),
            address: form.address.trim(),
            latitude: latitudeValue,
            longitude: longitudeValue
        };
    };

    const submitForm = async () => {
        const payload = buildPayload();
        if (!payload) {
            return;
        }

        try {
            setIsSaving(true);
            if (editingHubId) {
                await updateHub(editingHubId, payload);
            } else {
                await createHub(payload);
            }

            await loadHubs(search);
            setStatus(editingHubId ? 'Đã cập nhật hub' : 'Đã tạo hub');
            setMessage(payload.name);
            resetForm();
        } catch (error) {
            console.error(error);
            setStatus('Lưu thất bại');
            setMessage(error instanceof Error ? error.message : 'Không thể lưu hub.');
        } finally {
            setIsSaving(false);
        }
    };

    const activeHubs = hubs.length;

    return (
        <div className="min-h-screen bg-surface text-on-surface font-body-md">
            <header className="sticky top-0 z-20 border-b border-outline-variant/40 bg-surface-container-lowest/95 backdrop-blur">
                <div className="mx-auto flex max-w-[1440px] items-center justify-between gap-4 px-4 py-4 md:px-8">
                    <div className="flex items-center gap-3">
                        <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary text-on-primary">
                            <span className="material-symbols-outlined text-[22px]">warehouse</span>
                        </div>
                        <div>
                            <h1 className="font-headline-md text-2xl font-semibold text-primary">Quản lý Hub</h1>
                            <p className="text-sm text-on-surface-variant">Thao tác quản trị</p>
                        </div>
                    </div>

                    <div className="hidden items-center gap-3 rounded-lg border border-outline-variant/50 bg-surface-container-low px-4 py-2 md:flex">
                        <span className="material-symbols-outlined text-[20px] text-secondary">database</span>
                        <div>
                            <p className="text-xs font-semibold uppercase text-on-surface-variant">{status}</p>
                            <p className="max-w-[420px] truncate text-sm text-on-surface">{message}</p>
                        </div>
                    </div>
                </div>
            </header>

            <main className="mx-auto grid max-w-[1440px] grid-cols-1 gap-4 px-4 py-4 md:px-8 xl:grid-cols-[minmax(0,1fr)_480px]">
                <section className="space-y-4 xl:contents">
                    <div className="grid grid-cols-1 gap-4 md:grid-cols-3 xl:col-start-1 xl:row-start-1">
                        <MetricCard icon="hub" label="Hub đang hoạt động" value={String(activeHubs)} />
                        <MetricCard icon="pin_drop" label="Điểm PostGIS" value={String(activeHubs)} />
                        <MetricCard icon="travel_explore" label="Geocoder" value="Nominatim" />
                    </div>

                    <section className="rounded-xl border border-outline-variant/30 bg-surface-container-lowest p-card-padding card-shadow xl:col-start-1 xl:row-start-2">
                        <div className="mb-4 flex flex-col justify-between gap-3 md:flex-row md:items-center">
                            <div>
                                <h2 className="font-headline-md text-xl font-semibold text-on-surface">Bản đồ Hub</h2>
                                <p className="text-sm text-on-surface-variant">
                                    {form.latitude && form.longitude
                                        ? `${form.latitude}, ${form.longitude}`
                                        : 'Tìm địa chỉ để đặt ghim'}
                                </p>
                            </div>

                            <div className="flex items-center gap-2 rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 py-2 text-sm text-on-surface-variant">
                                <span className="material-symbols-outlined text-[20px] text-primary">open_with</span>
                                Kéo ghim để điều chỉnh
                            </div>
                        </div>

                        <div className="overflow-hidden rounded-lg border border-outline-variant/40">
                            <HubMapPicker
                                flyToVersion={flyToVersion}
                                latitude={latitude}
                                longitude={longitude}
                                onLocationChange={handleMapLocationChange}
                            />
                        </div>
                    </section>

                    <section className="rounded-xl border border-outline-variant/30 bg-surface-container-lowest p-card-padding card-shadow xl:col-span-2 xl:row-start-3">
                        <div className="mb-4 flex flex-col justify-between gap-3 md:flex-row md:items-center">
                            <div>
                                <h2 className="font-headline-md text-xl font-semibold text-on-surface">Danh sách Hub</h2>
                                <p className="text-sm text-on-surface-variant">{isLoadingHubs ? 'Đang tải...' : `${hubs.length} bản ghi`}</p>
                            </div>

                            <label className="flex w-full items-center gap-2 rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 py-2 md:max-w-sm">
                                <span className="material-symbols-outlined text-[20px] text-on-surface-variant">search</span>
                                <input
                                    className="w-full bg-transparent text-sm outline-none"
                                    onChange={(event) => setSearch(event.target.value)}
                                    placeholder="Tìm kiếm hub"
                                    value={search}
                                />
                            </label>
                        </div>

                        <div className="overflow-x-auto custom-scrollbar">
                            <table className="w-full min-w-[860px] text-left">
                                <thead>
                                    <tr className="border-b border-outline-variant/30 text-sm font-semibold text-on-surface-variant">
                                        <th className="py-3 pr-4">Hub</th>
                                        <th className="py-3 pr-4">Địa chỉ</th>
                                        <th className="py-3 pr-4">Tọa độ</th>
                                        <th className="py-3 pr-4">Cập nhật</th>
                                        <th className="py-3 pr-4 text-right">Thao tác</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {hubs.map((hub) => (
                                        <tr className="border-b border-outline-variant/20 last:border-0" key={hub.id}>
                                            <td className="py-4 pr-4">
                                                <div className="font-semibold text-on-surface">{hub.name}</div>
                                                <div className="text-xs text-on-surface-variant">{hub.id}</div>
                                            </td>
                                            <td className="max-w-[320px] py-4 pr-4 text-sm text-on-surface-variant">
                                                <span className="line-clamp-2">{hub.address}</span>
                                            </td>
                                            <td className="py-4 pr-4 text-sm text-on-surface">
                                                {formatCoordinate(hub.latitude)}, {formatCoordinate(hub.longitude)}
                                            </td>
                                            <td className="py-4 pr-4 text-sm text-on-surface-variant">{formatDateTime(hub.updatedAt)}</td>
                                            <td className="py-4 pr-4">
                                                <div className="flex justify-end">
                                                    <button
                                                        className="flex h-9 w-9 items-center justify-center rounded-lg border border-outline-variant/40 bg-surface-container-low text-primary transition-colors hover:bg-surface-container"
                                                        onClick={() => editHub(hub)}
                                                        title="Chỉnh sửa hub"
                                                        type="button"
                                                    >
                                                        <span className="material-symbols-outlined text-[20px]">edit</span>
                                                    </button>
                                                </div>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>

                            {!isLoadingHubs && hubs.length === 0 && (
                                <div className="flex min-h-[220px] flex-col items-center justify-center text-center">
                                    <span className="material-symbols-outlined mb-2 text-[36px] text-on-surface-variant">warehouse</span>
                                    <p className="font-semibold text-on-surface">Chưa có hub nào</p>
                                    <p className="mt-1 text-sm text-on-surface-variant">{message}</p>
                                </div>
                            )}
                        </div>
                    </section>
                </section>

                <aside className="space-y-4 xl:col-start-2 xl:row-span-2 xl:row-start-1">
                    <section className="rounded-xl border border-outline-variant/30 bg-surface-container-lowest p-card-padding card-shadow">
                        <div className="mb-4 flex items-start justify-between gap-3">
                            <div>
                                <h2 className="font-headline-md text-xl font-semibold text-on-surface">
                                    {editingHubId ? 'Cập nhật Hub' : 'Tạo Hub'}
                                </h2>
                                <p className="text-sm text-on-surface-variant">
                                    {selectedHub ? selectedHub.id : 'Tạo hub mới'}
                                </p>
                            </div>
                            <button
                                className="flex h-9 w-9 items-center justify-center rounded-lg border border-outline-variant/40 bg-surface-container-low text-on-surface-variant hover:bg-surface-container"
                                onClick={resetForm}
                                title="Đặt lại form"
                                type="button"
                            >
                                <span className="material-symbols-outlined text-[20px]">refresh</span>
                            </button>
                        </div>

                        <div className="space-y-4">
                            <TextField
                                label="Tên hub"
                                onChange={(value) => updateForm('name', value)}
                                placeholder="VD: Hub HCM"
                                value={form.name}
                            />

                            <div className="relative">
                                <TextField
                                    label="Địa chỉ"
                                    onChange={(value) => updateForm('address', value)}
                                    placeholder="Nhập địa chỉ"
                                    value={form.address}
                                />

                                {(places.length > 0 || isSearchingPlaces) && (
                                    <div className="absolute left-0 right-0 top-full z-30 mt-2 overflow-hidden rounded-lg border border-outline-variant/50 bg-surface-container-lowest shadow-xl">
                                        {isSearchingPlaces && (
                                            <div className="flex items-center gap-2 px-3 py-3 text-sm text-on-surface-variant">
                                                <span className="material-symbols-outlined animate-spin text-[18px]">progress_activity</span>
                                                Đang tìm kiếm
                                            </div>
                                        )}
                                        {places.map((place) => (
                                            <button
                                                className="block w-full border-t border-outline-variant/20 px-3 py-3 text-left text-sm hover:bg-surface-container-low"
                                                key={place.place_id}
                                                onClick={() => selectPlace(place)}
                                                type="button"
                                            >
                                                <span className="font-medium text-on-surface">{place.display_name.split(',')[0]}</span>
                                                <span className="mt-1 block text-xs text-on-surface-variant">{place.display_name}</span>
                                            </button>
                                        ))}
                                    </div>
                                )}
                            </div>

                            <div className="grid grid-cols-2 gap-3">
                                <TextField
                                    label="Vĩ độ"
                                    onChange={(value) => {
                                        updateForm('latitude', value);
                                        setLocationConfirmed(false);
                                    }}
                                    value={form.latitude}
                                />
                                <TextField
                                    label="Kinh độ"
                                    onChange={(value) => {
                                        updateForm('longitude', value);
                                        setLocationConfirmed(false);
                                    }}
                                    value={form.longitude}
                                />
                            </div>

                            <div className="flex flex-col gap-3 sm:flex-row">
                                <button
                                    className={`flex flex-1 items-center justify-center gap-2 rounded-lg border px-4 py-3 text-sm font-semibold transition-colors ${
                                        locationConfirmed
                                            ? 'border-secondary bg-secondary-container text-on-secondary-container'
                                            : 'border-primary bg-surface-container-lowest text-primary hover:bg-surface-container-low'
                                    }`}
                                    onClick={confirmLocation}
                                    type="button"
                                >
                                    <span className="material-symbols-outlined text-[20px]">
                                        {locationConfirmed ? 'check_circle' : 'add_location_alt'}
                                    </span>
                                    {locationConfirmed ? 'Đã xác nhận vị trí' : 'Xác nhận vị trí'}
                                </button>

                                <button
                                    className="flex flex-1 items-center justify-center gap-2 rounded-lg bg-primary px-4 py-3 text-sm font-semibold text-on-primary transition-colors hover:bg-primary-container disabled:opacity-60"
                                    disabled={isSaving}
                                    onClick={() => void submitForm()}
                                    type="button"
                                >
                                    <span className="material-symbols-outlined text-[20px]">save</span>
                                    {isSaving ? 'Đang lưu...' : editingHubId ? 'Cập nhật Hub' : 'Tạo Hub'}
                                </button>
                            </div>
                        </div>
                    </section>
                </aside>
            </main>
        </div>
    );
}

function MetricCard({ icon, label, value }: { icon: string; label: string; value: string }) {
    return (
        <div className="rounded-xl border border-outline-variant/30 bg-surface-container-lowest p-4 card-shadow">
            <div className="mb-3 flex h-9 w-9 items-center justify-center rounded-lg bg-surface-container-low text-primary">
                <span className="material-symbols-outlined text-[20px]">{icon}</span>
            </div>
            <p className="text-sm text-on-surface-variant">{label}</p>
            <p className="mt-1 font-headline-md text-2xl font-semibold text-on-surface">{value}</p>
        </div>
    );
}

function TextField({
    label,
    onChange,
    placeholder,
    value
}: {
    label: string;
    onChange: (value: string) => void;
    placeholder?: string;
    value: string;
}) {
    return (
        <label className="flex flex-col gap-2">
            <span className="text-sm font-semibold text-on-surface-variant">{label}</span>
            <input
                className="rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 py-2 text-sm text-on-surface outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20"
                onChange={(event) => onChange(event.target.value)}
                placeholder={placeholder}
                value={value}
            />
        </label>
    );
}

function HubMapPicker({
    flyToVersion,
    latitude,
    longitude,
    onLocationChange
}: {
    flyToVersion: number;
    latitude: number;
    longitude: number;
    onLocationChange: (latitude: number, longitude: number) => void;
}) {
    const containerRef = useRef<HTMLDivElement | null>(null);
    const mapRef = useRef<L.Map | null>(null);
    const markerRef = useRef<L.Marker | null>(null);
    const onLocationChangeRef = useRef(onLocationChange);
    const flyToVersionRef = useRef(flyToVersion);

    useEffect(() => {
        onLocationChangeRef.current = onLocationChange;
    }, [onLocationChange]);

    useEffect(() => {
        if (!containerRef.current || mapRef.current) {
            return;
        }

        const map = L.map(containerRef.current, {
            center: [latitude, longitude],
            zoom: 13,
            zoomControl: true
        });

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap contributors'
        }).addTo(map);

        const marker = L.marker([latitude, longitude], {
            draggable: true,
            icon: L.divIcon({
                className: 'hms-hub-marker',
                html: '<div style="width:30px;height:30px;border-radius:50% 50% 50% 0;background:#00288e;border:3px solid #ffffff;box-shadow:0 10px 22px rgba(0,40,142,.32);transform:rotate(-45deg);"></div>',
                iconAnchor: [15, 30],
                popupAnchor: [0, -30]
            })
        }).addTo(map);

        marker.on('dragend', () => {
            const next = marker.getLatLng();
            onLocationChangeRef.current(next.lat, next.lng);
        });

        mapRef.current = map;
        markerRef.current = marker;

        window.setTimeout(() => map.invalidateSize(), 0);

        return () => {
            map.remove();
            mapRef.current = null;
            markerRef.current = null;
        };
    }, []);

    useEffect(() => {
        const nextLatLng = L.latLng(latitude, longitude);
        markerRef.current?.setLatLng(nextLatLng);

        if (mapRef.current && flyToVersionRef.current !== flyToVersion) {
            mapRef.current.flyTo(nextLatLng, Math.max(mapRef.current.getZoom(), 15), {
                duration: 0.6
            });
            flyToVersionRef.current = flyToVersion;
        }
    }, [flyToVersion, latitude, longitude]);

    return <div className="h-[420px] w-full bg-surface-container-low" ref={containerRef} />;
}
