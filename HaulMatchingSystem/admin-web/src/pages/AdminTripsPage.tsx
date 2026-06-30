import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { fetchUsers, type UserDto } from '../api/identityApi';
import { fetchHubs, type Hub } from '../api/hubsApi';
import { fetchVehicles, type Vehicle } from '../api/vehiclesApi';
import {
    changeTripStatus,
    createTrip,
    deleteTrip,
    fetchTrips,
    updateTrip,
    type Trip,
    type TripPayload,
    type TripStatus
} from '../api/tripsApi';

interface AdminTripsPageProps {
    sidebar?: ReactNode;
}

interface TripForm {
    driverId: string;
    vehicleId: string;
    originHubId: string;
    destHubId: string;
    currentLoadWeightKg: string;
    currentLoadVolumeCbm: string;
    routeLineString: string;
}

const statusStyle: Record<TripStatus, string> = {
    Active: 'border-secondary/30 bg-secondary-container text-on-secondary-container',
    Completed: 'border-primary/20 bg-primary-fixed text-on-primary-fixed',
    Breakdown: 'border-error/20 bg-error-container text-error'
};

const emptyForm = (
    driverId = '',
    vehicleId = '',
    originHubId = '',
    destHubId = ''
): TripForm => ({
    driverId,
    vehicleId,
    originHubId,
    destHubId,
    currentLoadWeightKg: '0',
    currentLoadVolumeCbm: '0',
    routeLineString: ''
});

export default function AdminTripsPage({ sidebar }: AdminTripsPageProps) {
    const [trips, setTrips] = useState<Trip[]>([]);
    const [drivers, setDrivers] = useState<UserDto[]>([]);
    const [vehicles, setVehicles] = useState<Vehicle[]>([]);
    const [hubs, setHubs] = useState<Hub[]>([]);
    const [form, setForm] = useState<TripForm>(emptyForm());
    const [editingId, setEditingId] = useState<string | null>(null);
    const [selectedTripId, setSelectedTripId] = useState<string | null>(null);
    const [search, setSearch] = useState('');
    const [statusFilter, setStatusFilter] = useState('');
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [message, setMessage] = useState('');

    const loadData = async () => {
        setLoading(true);
        try {
            const [tripData, userData, vehicleData, hubData] = await Promise.all([
                fetchTrips(),
                fetchUsers(),
                fetchVehicles(),
                fetchHubs()
            ]);
            const driverData = userData.filter(user => user.role === 'Driver');

            setTrips(tripData);
            setDrivers(driverData);
            setVehicles(vehicleData);
            setHubs(hubData);
            setSelectedTripId(current => current && tripData.some(trip => trip.id === current)
                ? current
                : tripData[0]?.id || null);
            setForm(current => ({
                ...current,
                driverId: current.driverId || driverData[0]?.id || '',
                vehicleId: current.vehicleId || vehicleData[0]?.id || '',
                originHubId: current.originHubId || hubData[0]?.id || '',
                destHubId: current.destHubId || hubData[1]?.id || hubData[0]?.id || ''
            }));
            setMessage('');
        } catch (error) {
            setMessage(error instanceof Error ? error.message : 'Không thể tải dữ liệu chuyến đi.');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        void loadData();
    }, []);

    const driverNames = useMemo(
        () => Object.fromEntries(drivers.map(driver => [driver.id, driver.fullName])),
        [drivers]
    );
    const vehicleNames = useMemo(
        () => Object.fromEntries(vehicles.map(vehicle => [vehicle.id, `${vehicle.code} - ${vehicle.licensePlate}`])),
        [vehicles]
    );
    const hubNames = useMemo(
        () => Object.fromEntries(hubs.map(hub => [hub.id, hub.name])),
        [hubs]
    );

    const selectedTrip = trips.find(trip => trip.id === selectedTripId) || null;
    const selectedVehicle = vehicles.find(vehicle => vehicle.id === form.vehicleId) || null;

    const filteredTrips = useMemo(() => {
        const keyword = search.trim().toLowerCase();
        return trips.filter(trip => {
            if (statusFilter && trip.status !== statusFilter) return false;
            if (!keyword) return true;

            return trip.id.toLowerCase().includes(keyword) ||
                (driverNames[trip.driverId] || '').toLowerCase().includes(keyword) ||
                (vehicleNames[trip.vehicleId] || '').toLowerCase().includes(keyword) ||
                (hubNames[trip.originHubId] || '').toLowerCase().includes(keyword) ||
                (hubNames[trip.destHubId] || '').toLowerCase().includes(keyword);
        });
    }, [driverNames, hubNames, search, statusFilter, trips, vehicleNames]);

    const stats = useMemo(() => ({
        active: trips.filter(trip => trip.status === 'Active').length,
        completed: trips.filter(trip => trip.status === 'Completed').length,
        breakdown: trips.filter(trip => trip.status === 'Breakdown').length
    }), [trips]);

    const updateForm = (field: keyof TripForm, value: string) => {
        setForm(current => ({
            ...current,
            [field]: value,
            ...((field === 'originHubId' || field === 'destHubId') ? { routeLineString: '' } : {})
        }));
    };

    const resetForm = () => {
        setEditingId(null);
        setForm(emptyForm(
            drivers[0]?.id || '',
            vehicles[0]?.id || '',
            hubs[0]?.id || '',
            hubs[1]?.id || hubs[0]?.id || ''
        ));
        setMessage('');
    };

    const editTrip = (trip: Trip) => {
        setEditingId(trip.id);
        setSelectedTripId(trip.id);
        setForm({
            driverId: trip.driverId,
            vehicleId: trip.vehicleId,
            originHubId: trip.originHubId,
            destHubId: trip.destHubId,
            currentLoadWeightKg: String(trip.currentLoadWeightKg),
            currentLoadVolumeCbm: String(trip.currentLoadVolumeCbm),
            routeLineString: trip.routeLineString
        });
        window.scrollTo({ top: 0, behavior: 'smooth' });
    };

    const buildPayload = (): TripPayload | null => {
        const weight = Number(form.currentLoadWeightKg);
        const volume = Number(form.currentLoadVolumeCbm);

        if (!form.driverId) return setValidationMessage('Vui lòng chọn tài xế.');
        if (!form.vehicleId) return setValidationMessage('Vui lòng chọn xe.');
        if (!form.originHubId || !form.destHubId) return setValidationMessage('Vui lòng chọn Hub đi và Hub đến.');
        if (form.originHubId === form.destHubId) return setValidationMessage('Hub đi và Hub đến phải khác nhau.');
        if (!Number.isFinite(weight) || weight < 0) return setValidationMessage('Khối lượng hiện tại không hợp lệ.');
        if (!Number.isFinite(volume) || volume < 0) return setValidationMessage('Thể tích hiện tại không hợp lệ.');
        if (selectedVehicle && weight > selectedVehicle.maxWeightKg) return setValidationMessage('Khối lượng vượt tải trọng xe.');
        if (selectedVehicle && volume > selectedVehicle.maxVolumeCbm) return setValidationMessage('Thể tích vượt dung tích xe.');

        return {
            driverId: form.driverId,
            vehicleId: form.vehicleId,
            originHubId: form.originHubId,
            destHubId: form.destHubId,
            routeLineString: form.routeLineString || null,
            currentLoadWeightKg: weight,
            currentLoadVolumeCbm: volume
        };
    };

    const setValidationMessage = (value: string): null => {
        setMessage(value);
        return null;
    };

    const saveTrip = async () => {
        const payload = buildPayload();
        if (!payload) return;

        try {
            setSaving(true);
            const saved = editingId
                ? await updateTrip(editingId, payload)
                : await createTrip(payload);

            await loadData();
            setSelectedTripId(saved.id);
            resetForm();
            setMessage(editingId ? 'Đã cập nhật chuyến đi.' : 'Đã tạo và gán chuyến cho tài xế.');
        } catch (error) {
            setMessage(error instanceof Error ? error.message : 'Không thể lưu chuyến đi.');
        } finally {
            setSaving(false);
        }
    };

    const updateStatus = async (trip: Trip, status: Exclude<TripStatus, 'Active'>) => {
        try {
            await changeTripStatus(trip.id, status);
            await loadData();
            setMessage(status === 'Completed' ? 'Đã hoàn thành chuyến đi.' : 'Đã đánh dấu sự cố xe.');
        } catch (error) {
            setMessage(error instanceof Error ? error.message : 'Không thể đổi trạng thái.');
        }
    };

    const removeTrip = async (trip: Trip) => {
        if (!window.confirm(`Xóa chuyến ${trip.id.slice(0, 8).toUpperCase()}?`)) return;
        try {
            await deleteTrip(trip.id);
            if (editingId === trip.id) resetForm();
            await loadData();
            setMessage('Đã xóa chuyến đi.');
        } catch (error) {
            setMessage(error instanceof Error ? error.message : 'Không thể xóa chuyến đi.');
        }
    };

    return (
        <div className="min-h-screen bg-surface text-on-surface font-body-md flex overflow-x-hidden">
            {sidebar}
            <div className="flex min-w-0 flex-1 flex-col xl:ml-64">
                <header className="sticky top-0 z-20 flex min-h-16 items-center border-b border-outline-variant bg-surface-container-lowest px-5 md:px-8">
                    <div className="flex items-center gap-2 text-headline-md font-bold text-primary">
                        <span className="material-symbols-outlined">route</span>
                        Quản lý Chuyến Đi
                    </div>
                </header>

                <main className="mx-auto w-full max-w-[1500px] space-y-4 p-4 md:p-8">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                        <Metric icon="route" label="Đang hoạt động" value={stats.active} />
                        <Metric icon="task_alt" label="Đã hoàn thành" value={stats.completed} />
                        <Metric icon="warning" label="Sự cố" value={stats.breakdown} />
                    </div>

                    <div className="grid grid-cols-1 gap-4 xl:grid-cols-[minmax(0,1fr)_430px]">
                        <section className="order-2 min-w-0 space-y-4 xl:order-1">
                            <div className="rounded-lg border border-outline-variant/30 bg-surface-container-lowest p-4 card-shadow md:p-5">
                                <div className="mb-4 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                                    <div><h1 className="text-xl font-bold">Danh sách chuyến đi</h1><p className="mt-1 text-sm text-on-surface-variant">{filteredTrips.length} / {trips.length} chuyến</p></div>
                                    <div className="flex flex-col gap-2 sm:flex-row">
                                        <label className="flex h-10 items-center rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 sm:w-72"><span className="material-symbols-outlined mr-2 text-[19px] text-on-surface-variant">search</span><input className="w-full bg-transparent text-sm outline-none" onChange={event => setSearch(event.target.value)} placeholder="Tìm tài xế, xe, Hub, mã chuyến" type="search" value={search} /></label>
                                        <select className="h-10 rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 text-sm outline-none" onChange={event => setStatusFilter(event.target.value)} value={statusFilter}><option value="">Tất cả trạng thái</option><option value="Active">Đang hoạt động</option><option value="Completed">Đã hoàn thành</option><option value="Breakdown">Sự cố</option></select>
                                        <button aria-label="Tải lại" className="flex h-10 w-10 items-center justify-center rounded-lg border border-outline-variant/50 text-primary hover:bg-surface-container-low" onClick={() => void loadData()} title="Tải lại" type="button"><span className={`material-symbols-outlined text-[20px] ${loading ? 'animate-spin' : ''}`}>refresh</span></button>
                                    </div>
                                </div>

                                <div className="overflow-x-auto rounded-lg border border-outline-variant/20">
                                    <table className="w-full min-w-[900px] text-left text-sm">
                                        <thead className="bg-surface-container-low text-xs text-on-surface-variant"><tr><th className="px-4 py-3">Mã chuyến</th><th className="px-4 py-3">Tài xế / Xe</th><th className="px-4 py-3">Lộ trình</th><th className="px-4 py-3">Trạng thái</th><th className="px-4 py-3">Ngày tạo</th><th className="px-4 py-3 text-center">Thao tác</th></tr></thead>
                                        <tbody>
                                            {loading ? <tr><td className="px-4 py-12 text-center text-on-surface-variant" colSpan={6}>Đang tải chuyến đi...</td></tr> : filteredTrips.length === 0 ? <tr><td className="px-4 py-12 text-center text-on-surface-variant" colSpan={6}>Không tìm thấy chuyến đi.</td></tr> : filteredTrips.map(trip => (
                                                <tr className={`cursor-pointer border-t border-outline-variant/15 hover:bg-surface-container-low/50 ${selectedTripId === trip.id ? 'bg-primary/5' : ''}`} key={trip.id} onClick={() => setSelectedTripId(trip.id)}>
                                                    <td className="px-4 py-3 font-bold text-primary">{trip.id.slice(0, 8).toUpperCase()}</td>
                                                    <td className="px-4 py-3"><p className="font-bold">{driverNames[trip.driverId] || trip.driverId.slice(0, 8)}</p><p className="mt-0.5 text-xs text-on-surface-variant">{vehicleNames[trip.vehicleId] || trip.vehicleId.slice(0, 8)}</p></td>
                                                    <td className="px-4 py-3"><p className="font-medium">{hubNames[trip.originHubId] || trip.originHubId.slice(0, 8)}</p><p className="mt-0.5 text-xs text-on-surface-variant">đến {hubNames[trip.destHubId] || trip.destHubId.slice(0, 8)}</p></td>
                                                    <td className="px-4 py-3"><span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-bold ${statusStyle[trip.status]}`}>{trip.status}</span></td>
                                                    <td className="px-4 py-3 text-on-surface-variant">{new Date(trip.createdAt).toLocaleDateString('vi-VN')}</td>
                                                    <td className="px-4 py-3 text-center"><div className="flex justify-center gap-1"><button aria-label="Xem chi tiết" className="flex h-8 w-8 items-center justify-center rounded-lg text-on-surface-variant hover:bg-surface-container" onClick={event => { event.stopPropagation(); setSelectedTripId(trip.id); }} title="Xem chi tiết" type="button"><span className="material-symbols-outlined text-[19px]">visibility</span></button>{trip.status === 'Active' && <button aria-label="Sửa chuyến" className="flex h-8 w-8 items-center justify-center rounded-lg text-primary hover:bg-primary/10" onClick={event => { event.stopPropagation(); editTrip(trip); }} title="Sửa chuyến" type="button"><span className="material-symbols-outlined text-[19px]">edit</span></button>}<button aria-label="Xóa chuyến" className="flex h-8 w-8 items-center justify-center rounded-lg text-error hover:bg-error/10" onClick={event => { event.stopPropagation(); void removeTrip(trip); }} title="Xóa chuyến" type="button"><span className="material-symbols-outlined text-[19px]">delete</span></button></div></td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                </div>
                            </div>

                            {selectedTrip && <TripDetail trip={selectedTrip} driverName={driverNames[selectedTrip.driverId] || selectedTrip.driverId} hubNames={hubNames} onEdit={() => editTrip(selectedTrip)} onStatusChange={status => void updateStatus(selectedTrip, status)} vehicleName={vehicleNames[selectedTrip.vehicleId] || selectedTrip.vehicleId} />}
                        </section>

                        <aside className="order-1 xl:order-2">
                            <section className="rounded-lg border border-outline-variant/30 bg-surface-container-lowest p-5 card-shadow xl:sticky xl:top-20">
                                <div className="mb-5 flex items-start justify-between border-b border-outline-variant/20 pb-4"><div><h2 className="text-xl font-bold">{editingId ? 'Cập nhật chuyến đi' : 'Tạo và gán chuyến'}</h2><p className="mt-1 text-sm text-on-surface-variant">Admin chọn tài xế phụ trách chuyến.</p></div>{editingId && <button aria-label="Hủy sửa" className="flex h-9 w-9 items-center justify-center rounded-lg border border-outline-variant/50" onClick={resetForm} title="Hủy sửa" type="button"><span className="material-symbols-outlined">close</span></button>}</div>
                                <div className="space-y-4">
                                    <SelectField icon="badge" label="Tài xế *" onChange={value => updateForm('driverId', value)} options={drivers.map(driver => ({ value: driver.id, label: driver.fullName }))} placeholder="Chưa có tài xế" value={form.driverId} />
                                    <SelectField icon="local_shipping" label="Xe *" onChange={value => updateForm('vehicleId', value)} options={vehicles.map(vehicle => ({ value: vehicle.id, label: `${vehicle.code} - ${vehicle.licensePlate}` }))} placeholder="Chưa có xe" value={form.vehicleId} />
                                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-1"><SelectField icon="trip_origin" label="Hub xuất phát *" onChange={value => updateForm('originHubId', value)} options={hubs.map(hub => ({ value: hub.id, label: hub.name }))} placeholder="Chưa có Hub" value={form.originHubId} /><SelectField icon="location_on" label="Hub đích *" onChange={value => updateForm('destHubId', value)} options={hubs.map(hub => ({ value: hub.id, label: hub.name }))} placeholder="Chưa có Hub" value={form.destHubId} /></div>
                                    <div className="grid grid-cols-2 gap-3"><NumberField label="Khối lượng hiện tại (kg)" max={selectedVehicle?.maxWeightKg} onChange={value => updateForm('currentLoadWeightKg', value)} value={form.currentLoadWeightKg} /><NumberField label="Thể tích hiện tại (CBM)" max={selectedVehicle?.maxVolumeCbm} onChange={value => updateForm('currentLoadVolumeCbm', value)} value={form.currentLoadVolumeCbm} /></div>
                                    {selectedVehicle && <div className="grid grid-cols-2 gap-3 rounded-lg bg-surface-container-low p-3 text-xs"><div><p className="text-on-surface-variant">Tải trọng xe</p><p className="mt-1 font-bold">{selectedVehicle.maxWeightKg.toLocaleString('vi-VN')} kg</p></div><div><p className="text-on-surface-variant">Dung tích xe</p><p className="mt-1 font-bold">{selectedVehicle.maxVolumeCbm} CBM</p></div></div>}
                                </div>
                                {message && <p className={`mt-4 rounded-lg px-3 py-2 text-sm ${message.startsWith('Đã') ? 'bg-secondary-container text-on-secondary-container' : 'bg-error-container text-error'}`}>{message}</p>}
                                <div className="mt-5 flex gap-3 border-t border-outline-variant/20 pt-5"><button className="flex-1 rounded-lg border border-outline-variant px-4 py-3 text-sm font-bold hover:bg-surface-container-low" onClick={resetForm} type="button">Làm mới</button><button className="flex flex-1 items-center justify-center gap-2 rounded-lg bg-primary px-4 py-3 text-sm font-bold text-on-primary hover:bg-primary/90 disabled:opacity-50" disabled={saving || drivers.length === 0 || vehicles.length === 0 || hubs.length < 2} onClick={() => void saveTrip()} type="button"><span className={`material-symbols-outlined text-[20px] ${saving ? 'animate-spin' : ''}`}>{saving ? 'progress_activity' : 'save'}</span>{saving ? 'Đang lưu...' : editingId ? 'Cập nhật' : 'Tạo chuyến'}</button></div>
                            </section>
                        </aside>
                    </div>
                </main>
            </div>
        </div>
    );
}

function Metric({ icon, label, value }: { icon: string; label: string; value: number }) {
    return <div className="flex items-center gap-3 rounded-lg border border-outline-variant/30 bg-surface-container-lowest p-4 card-shadow"><div className="flex h-10 w-10 items-center justify-center rounded-lg bg-surface-container-low text-primary"><span className="material-symbols-outlined">{icon}</span></div><div><p className="text-sm text-on-surface-variant">{label}</p><p className="text-2xl font-bold">{value}</p></div></div>;
}

function SelectField({ icon, label, onChange, options, placeholder, value }: { icon: string; label: string; onChange: (value: string) => void; options: Array<{ value: string; label: string }>; placeholder: string; value: string }) {
    return <label className="flex flex-col gap-2"><span className="text-sm font-bold text-on-surface-variant">{label}</span><div className="flex h-11 items-center rounded-lg border border-outline-variant/50 bg-surface-container-low px-3"><span className="material-symbols-outlined mr-2 text-[19px] text-on-surface-variant">{icon}</span><select className="w-full bg-transparent text-sm outline-none" disabled={options.length === 0} onChange={event => onChange(event.target.value)} value={value}>{options.length === 0 ? <option value="">{placeholder}</option> : options.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</select></div></label>;
}

function NumberField({ label, max, onChange, value }: { label: string; max?: number; onChange: (value: string) => void; value: string }) {
    return <label className="flex flex-col gap-2"><span className="text-xs font-bold text-on-surface-variant">{label}</span><input className="h-11 rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 text-sm outline-none focus:border-primary" max={max} min="0" onChange={event => onChange(event.target.value)} step="0.01" type="number" value={value} /></label>;
}

function TripDetail({ trip, driverName, hubNames, onEdit, onStatusChange, vehicleName }: { trip: Trip; driverName: string; hubNames: Record<string, string>; onEdit: () => void; onStatusChange: (status: Exclude<TripStatus, 'Active'>) => void; vehicleName: string }) {
    return <section className="rounded-lg border border-outline-variant/30 bg-surface-container-lowest p-4 card-shadow md:p-5"><div className="mb-4 flex flex-col gap-3 border-b border-outline-variant/20 pb-4 sm:flex-row sm:items-start sm:justify-between"><div><p className="text-xs font-bold uppercase text-on-surface-variant">Chi tiết chuyến</p><h2 className="mt-1 text-xl font-bold text-primary">{trip.id.toUpperCase()}</h2></div><div className="flex gap-2">{trip.status === 'Active' && <><button className="flex items-center gap-2 rounded-lg border border-outline-variant px-3 py-2 text-sm font-bold text-primary hover:bg-primary/5" onClick={onEdit} type="button"><span className="material-symbols-outlined text-[18px]">edit</span>Sửa</button><button className="flex items-center gap-2 rounded-lg bg-secondary px-3 py-2 text-sm font-bold text-on-secondary" onClick={() => onStatusChange('Completed')} type="button"><span className="material-symbols-outlined text-[18px]">task_alt</span>Hoàn thành</button><button aria-label="Đánh dấu sự cố" className="flex h-10 w-10 items-center justify-center rounded-lg border border-error text-error hover:bg-error/5" onClick={() => onStatusChange('Breakdown')} title="Đánh dấu sự cố" type="button"><span className="material-symbols-outlined text-[19px]">warning</span></button></>}</div></div><div className="mb-4 grid grid-cols-2 gap-3 md:grid-cols-4"><Fact label="Tài xế" value={driverName} /><Fact label="Xe" value={vehicleName} /><Fact label="Tải hiện tại" value={`${trip.currentLoadWeightKg.toLocaleString('vi-VN')} kg`} /><Fact label="Thể tích" value={`${trip.currentLoadVolumeCbm} CBM`} /></div><RouteMap destination={hubNames[trip.destHubId] || trip.destHubId} origin={hubNames[trip.originHubId] || trip.originHubId} routeLineString={trip.routeLineString} /></section>;
}

function Fact({ label, value }: { label: string; value: string }) {
    return <div className="rounded-lg bg-surface-container-low p-3"><p className="text-xs text-on-surface-variant">{label}</p><p className="mt-1 truncate text-sm font-bold" title={value}>{value}</p></div>;
}

function RouteMap({ destination, origin, routeLineString }: { destination: string; origin: string; routeLineString: string }) {
    const containerRef = useRef<HTMLDivElement | null>(null);
    const coordinates = useMemo(() => parseLineString(routeLineString), [routeLineString]);

    useEffect(() => {
        if (!containerRef.current || coordinates.length < 2) return;
        const latLngs = coordinates.map(([lng, lat]) => L.latLng(lat, lng));
        const map = L.map(containerRef.current, { scrollWheelZoom: false });
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; OpenStreetMap contributors' }).addTo(map);
        const line = L.polyline(latLngs, { color: '#1b39b7', weight: 5 }).addTo(map);
        L.circleMarker(latLngs[0], { radius: 7, color: '#0f766e', fillColor: '#14b8a6', fillOpacity: 1 }).bindTooltip(origin).addTo(map);
        L.circleMarker(latLngs[latLngs.length - 1], { radius: 7, color: '#b91c1c', fillColor: '#ef4444', fillOpacity: 1 }).bindTooltip(destination).addTo(map);
        map.fitBounds(line.getBounds(), { padding: [24, 24], maxZoom: 12 });
        window.setTimeout(() => map.invalidateSize(), 0);
        return () => {
            map.remove();
        };
    }, [coordinates, destination, origin]);

    if (coordinates.length < 2) return <div className="flex min-h-48 items-center justify-center rounded-lg bg-surface-container-low text-sm text-on-surface-variant">Chưa có dữ liệu tuyến đường.</div>;
    return <div className="overflow-hidden rounded-lg border border-outline-variant/30"><div className="flex items-center justify-between bg-surface-container-low px-4 py-3 text-sm"><span className="font-bold">{origin}</span><span className="material-symbols-outlined text-primary">arrow_forward</span><span className="font-bold">{destination}</span></div><div className="h-72" ref={containerRef} /></div>;
}

function parseLineString(value: string): [number, number][] {
    return value.replace(/^LINESTRING\s*\(/i, '').replace(/\)$/, '').split(',').map(point => point.trim().split(/\s+/).map(Number)).filter((point): point is [number, number] => point.length === 2 && point.every(Number.isFinite));
}
