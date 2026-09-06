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
    fetchTripShipments,
    fetchTrips,
    unlinkTripShipment,
    updateTrip,
    type Trip,
    type TripPayload,
    type TripShipment,
    type TripStatus
} from '../api/tripsApi';
import { fetchInventory, type HubInventoryShipment } from '../api/hubInventoryApi';

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
    scheduledDepartureAt: string;
    warehouseShipmentIds: string[];
}

const statusStyle: Record<TripStatus, string> = {
    Active: 'border-secondary/30 bg-secondary-container text-on-secondary-container',
    Scheduled: 'border-tertiary/30 bg-tertiary-container text-on-tertiary-container',
    Ready: 'border-primary/30 bg-primary-container text-on-primary-container',
    InProgress: 'border-secondary/30 bg-secondary-container text-on-secondary-container',
    Completed: 'border-primary/20 bg-primary-fixed text-on-primary-fixed',
    Breakdown: 'border-error/20 bg-error-container text-error',
    Cancelled: 'border-outline/30 bg-surface-container-low text-on-surface-variant'
};

/** Map from current status to allowed target statuses (matches backend TripStateMachine) */
const allowedTransitions: Record<TripStatus, TripStatus[]> = {
    Active: ['Scheduled', 'Completed', 'Breakdown'],
    Scheduled: ['Ready', 'Cancelled'],
    Ready: ['InProgress', 'Cancelled'],
    InProgress: ['Completed'],
    Completed: [],
    Breakdown: ['Cancelled'],
    Cancelled: []
};

const statusLabels: Record<TripStatus, string> = {
    Active: 'Đang hoạt động',
    Scheduled: 'Đã lên lịch',
    Ready: 'Sẵn sàng',
    InProgress: 'Đang di chuyển',
    Completed: 'Đã hoàn thành',
    Breakdown: 'Sự cố',
    Cancelled: 'Đã hủy'
};

/** Shipment statuses seen on a trip — keep labels compact. */
const shipmentStatusLabels: Record<string, string> = {
    Matched: 'Đã ghép chuyến',
    In_Transit: 'Đang vận chuyển',
    Delivered: 'Đã giao',
    Returned_To_Hub: 'Đã trả về Hub',
    In_Warehouse: 'Trong kho',
};

const shipmentStatusStyle: Record<string, string> = {
    Matched: 'border-tertiary/30 bg-tertiary-container text-on-tertiary-container',
    In_Transit: 'border-secondary/30 bg-secondary-container text-on-secondary-container',
    Delivered: 'border-primary/20 bg-primary-fixed text-on-primary-fixed',
    Returned_To_Hub: 'border-outline/30 bg-surface-container-low text-on-surface-variant',
    In_Warehouse: 'border-outline/30 bg-surface-container-low text-on-surface-variant',
};

/** A trip can have its shipments removed only when it is in one of these states. */
const canUnlinkFromTrip = (status: TripStatus): boolean =>
    status === 'Active' ||
    status === 'Scheduled' ||
    status === 'Ready' ||
    status === 'Breakdown';

/** Default departure time = tomorrow 08:00 local time, formatted for <input type="datetime-local">. */
const defaultScheduledDepartureLocal = (): string => {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    d.setHours(8, 0, 0, 0);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
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
    routeLineString: '',
    scheduledDepartureAt: defaultScheduledDepartureLocal(),
    warehouseShipmentIds: []
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
    const [availableShipments, setAvailableShipments] = useState<HubInventoryShipment[]>([]);
    const [loadingShipments, setLoadingShipments] = useState(false);

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

    // Load available warehouse shipments from the selected origin hub (In_Warehouse / Returned).
    const loadAvailableShipments = async (originHubId: string) => {
        if (!originHubId) {
            setAvailableShipments([]);
            return;
        }
        try {
            setLoadingShipments(true);
            const result = await fetchInventory({
                hubId: originHubId,
                status: 'In_Warehouse',
                pageSize: 200
            });
            setAvailableShipments(result.items);
        } catch {
            setAvailableShipments([]);
        } finally {
            setLoadingShipments(false);
        }
    };

    useEffect(() => {
        void loadAvailableShipments(form.originHubId);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [form.originHubId]);

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
        active: trips.filter(trip => trip.status === 'Active' || trip.status === 'Scheduled' || trip.status === 'Ready' || trip.status === 'InProgress').length,
        completed: trips.filter(trip => trip.status === 'Completed').length,
        breakdown: trips.filter(trip => trip.status === 'Breakdown').length,
        cancelled: trips.filter(trip => trip.status === 'Cancelled').length
    }), [trips]);

    const updateForm = (field: keyof TripForm, value: string) => {
        setForm(current => ({
            ...current,
            [field]: value,
            ...((field === 'originHubId' || field === 'destHubId') ? { routeLineString: '' } : {})
        }));
    };

    const toggleShipment = (shipmentId: string) => {
        setForm(current => {
            const exists = current.warehouseShipmentIds.includes(shipmentId);
            const nextIds = exists
                ? current.warehouseShipmentIds.filter(id => id !== shipmentId)
                : [...current.warehouseShipmentIds, shipmentId];

            // Auto-calculate weight/volume from selected shipments + manual input.
            const selected = availableShipments.filter(s => nextIds.includes(s.id));
            const manualWeight = Number(current.currentLoadWeightKg) || 0;
            const manualVolume = Number(current.currentLoadVolumeCbm) || 0;
            const shipmentWeight = selected.reduce((sum, s) => sum + s.weightKg, 0);
            const shipmentVolume = selected.reduce((sum, s) => sum + s.volumeCbm, 0);

            return {
                ...current,
                warehouseShipmentIds: nextIds,
                currentLoadWeightKg: String(manualWeight + shipmentWeight),
                currentLoadVolumeCbm: String(manualVolume + shipmentVolume)
            };
        });
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
        // Convert ISO timestamp from API to local datetime-local format.
        const localDeparture = (() => {
            const raw = trip.scheduledDepartureAt;
            if (!raw) return defaultScheduledDepartureLocal();
            const d = new Date(raw);
            if (isNaN(d.getTime())) return defaultScheduledDepartureLocal();
            const pad = (n: number) => String(n).padStart(2, '0');
            return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
        })();
        setForm({
            driverId: trip.driverId,
            vehicleId: trip.vehicleId,
            originHubId: trip.originHubId,
            destHubId: trip.destHubId,
            currentLoadWeightKg: String(trip.currentLoadWeightKg),
            currentLoadVolumeCbm: String(trip.currentLoadVolumeCbm),
            routeLineString: trip.routeLineString,
            scheduledDepartureAt: localDeparture,
            warehouseShipmentIds: []
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

        // scheduledDepartureAt is required by the backend. Convert local datetime-local string to ISO UTC.
        if (!form.scheduledDepartureAt) return setValidationMessage('Vui lòng chọn ngày khởi hành dự kiến.');
        const departureDate = new Date(form.scheduledDepartureAt);
        if (isNaN(departureDate.getTime())) return setValidationMessage('Ngày khởi hành dự kiến không hợp lệ.');
        if (departureDate.getTime() <= Date.now()) return setValidationMessage('Ngày khởi hành dự kiến phải lớn hơn thời điểm hiện tại.');

        return {
            driverId: form.driverId,
            vehicleId: form.vehicleId,
            originHubId: form.originHubId,
            destHubId: form.destHubId,
            routeLineString: form.routeLineString || null,
            currentLoadWeightKg: weight,
            currentLoadVolumeCbm: volume,
            scheduledDepartureAt: departureDate.toISOString(),
            warehouseShipmentIds: form.warehouseShipmentIds
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

    const updateStatus = async (trip: Trip, status: TripStatus) => {
        try {
            await changeTripStatus(trip.id, status);
            await loadData();
            setMessage(`Đã chuyển trạng thái sang: ${statusLabels[status]}`);
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
                    <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                        <Metric icon="route" label="Đang hoạt động" value={stats.active} />
                        <Metric icon="task_alt" label="Đã hoàn thành" value={stats.completed} />
                        <Metric icon="warning" label="Sự cố" value={stats.breakdown} />
                        <Metric icon="cancel" label="Đã hủy" value={stats.cancelled} />
                    </div>

                    <div className="grid grid-cols-1 gap-4 xl:grid-cols-[minmax(0,1fr)_430px]">
                        <section className="order-2 min-w-0 space-y-4 xl:order-1">
                            <div className="rounded-lg border border-outline-variant/30 bg-surface-container-lowest p-4 card-shadow md:p-5">
                                <div className="mb-4 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                                    <div><h1 className="text-xl font-bold">Danh sách chuyến đi</h1><p className="mt-1 text-sm text-on-surface-variant">{filteredTrips.length} / {trips.length} chuyến</p></div>
                                    <div className="flex flex-col gap-2 sm:flex-row">
                                        <label className="flex h-10 items-center rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 sm:w-72"><span className="material-symbols-outlined mr-2 text-[19px] text-on-surface-variant">search</span><input className="w-full bg-transparent text-sm outline-none" onChange={event => setSearch(event.target.value)} placeholder="Tìm tài xế, xe, Hub, mã chuyến" type="search" value={search} /></label>
                                        <select className="h-10 rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 text-sm outline-none" onChange={event => setStatusFilter(event.target.value)} value={statusFilter}><option value="">Tất cả trạng thái</option><option value="Active">Đang hoạt động</option><option value="Scheduled">Đã lên lịch</option><option value="Ready">Sẵn sàng</option><option value="InProgress">Đang di chuyển</option><option value="Completed">Đã hoàn thành</option><option value="Breakdown">Sự cố</option><option value="Cancelled">Đã hủy</option></select>
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
                                                    <td className="px-4 py-3"><span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-bold ${statusStyle[trip.status]}`}>{statusLabels[trip.status]}</span></td>
                                                    <td className="px-4 py-3 text-on-surface-variant">{new Date(trip.createdAt).toLocaleDateString('vi-VN')}</td>
                                                    <td className="px-4 py-3 text-center"><div className="flex justify-center gap-1"><button aria-label="Xem chi tiết" className="flex h-8 w-8 items-center justify-center rounded-lg text-on-surface-variant hover:bg-surface-container" onClick={event => { event.stopPropagation(); setSelectedTripId(trip.id); }} title="Xem chi tiết" type="button"><span className="material-symbols-outlined text-[19px]">visibility</span></button>{trip.status !== 'Completed' && <button aria-label="Sửa chuyến" className="flex h-8 w-8 items-center justify-center rounded-lg text-primary hover:bg-primary/10" onClick={event => { event.stopPropagation(); editTrip(trip); }} title="Sửa chuyến" type="button"><span className="material-symbols-outlined text-[19px]">edit</span></button>}<button aria-label="Xóa chuyến" className="flex h-8 w-8 items-center justify-center rounded-lg text-error hover:bg-error/10" onClick={event => { event.stopPropagation(); void removeTrip(trip); }} title="Xóa chuyến" type="button"><span className="material-symbols-outlined text-[19px]">delete</span></button></div></td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                </div>
                            </div>

                            {selectedTrip && <TripDetail trip={selectedTrip} driverName={driverNames[selectedTrip.driverId] || selectedTrip.driverId} hubNames={hubNames} onEdit={() => editTrip(selectedTrip)} onStatusChange={status => void updateStatus(selectedTrip, status)} onUnlinkShipment={async shipmentId => { await unlinkTripShipment(selectedTrip.id, shipmentId); await loadData(); }} onUnlinkError={error => setMessage(error)} vehicleName={vehicleNames[selectedTrip.vehicleId] || selectedTrip.vehicleId} />}
                        </section>

                        <aside className="order-1 xl:order-2">
                            <section className="rounded-lg border border-outline-variant/30 bg-surface-container-lowest p-5 card-shadow xl:sticky xl:top-20">
                                <div className="mb-5 flex items-start justify-between border-b border-outline-variant/20 pb-4"><div><h2 className="text-xl font-bold">{editingId ? 'Cập nhật chuyến đi' : 'Tạo và gán chuyến'}</h2><p className="mt-1 text-sm text-on-surface-variant">Admin chọn tài xế phụ trách chuyến.</p></div>{editingId && <button aria-label="Hủy sửa" className="flex h-9 w-9 items-center justify-center rounded-lg border border-outline-variant/50" onClick={resetForm} title="Hủy sửa" type="button"><span className="material-symbols-outlined">close</span></button>}</div>
                                <div className="space-y-4">
                                    <SelectField icon="badge" label="Tài xế *" onChange={value => updateForm('driverId', value)} options={drivers.map(driver => ({ value: driver.id, label: driver.fullName }))} placeholder="Chưa có tài xế" value={form.driverId} />
                                    <SelectField icon="local_shipping" label="Xe *" onChange={value => updateForm('vehicleId', value)} options={vehicles.map(vehicle => ({ value: vehicle.id, label: `${vehicle.code} - ${vehicle.licensePlate}` }))} placeholder="Chưa có xe" value={form.vehicleId} />
                                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-1"><SelectField icon="trip_origin" label="Hub xuất phát *" onChange={value => updateForm('originHubId', value)} options={hubs.map(hub => ({ value: hub.id, label: hub.name }))} placeholder="Chưa có Hub" value={form.originHubId} /><SelectField icon="location_on" label="Hub đích *" onChange={value => updateForm('destHubId', value)} options={hubs.map(hub => ({ value: hub.id, label: hub.name }))} placeholder="Chưa có Hub" value={form.destHubId} /></div>
                                    <div className="grid grid-cols-2 gap-3"><NumberField label="Khối lượng hiện tại (kg)" max={selectedVehicle?.maxWeightKg} onChange={value => updateForm('currentLoadWeightKg', value)} value={form.currentLoadWeightKg} /><NumberField label="Thể tích hiện tại (CBM)" max={selectedVehicle?.maxVolumeCbm} onChange={value => updateForm('currentLoadVolumeCbm', value)} value={form.currentLoadVolumeCbm} /></div>
                                    <DateTimeField label="Ngày khởi hành dự kiến *" onChange={value => updateForm('scheduledDepartureAt', value)} value={form.scheduledDepartureAt} />
                                    {selectedVehicle && <div className="grid grid-cols-2 gap-3 rounded-lg bg-surface-container-low p-3 text-xs"><div><p className="text-on-surface-variant">Tải trọng xe</p><p className="mt-1 font-bold">{selectedVehicle.maxWeightKg.toLocaleString('vi-VN')} kg</p></div><div><p className="text-on-surface-variant">Dung tích xe</p><p className="mt-1 font-bold">{selectedVehicle.maxVolumeCbm} CBM</p></div></div>}

                                    {/* Warehouse shipment selection */}
                                    <div className="rounded-lg border border-outline-variant/30 bg-surface-container-low p-3">
                                        <div className="mb-2 flex items-center justify-between">
                                            <span className="text-sm font-bold text-on-surface-variant">Chọn hàng trong kho (Hub xuất phát)</span>
                                            {loadingShipments && <span className="text-xs text-on-surface-variant">Đang tải...</span>}
                                        </div>
                                        {!form.originHubId && (
                                            <p className="text-xs text-on-surface-variant">Vui lòng chọn Hub xuất phát trước.</p>
                                        )}
                                        {form.originHubId && availableShipments.length === 0 && !loadingShipments && (
                                            <p className="text-xs text-on-surface-variant">Không có đơn hàng nào trong kho của Hub này.</p>
                                        )}
                                        {availableShipments.length > 0 && (
                                            <div className="max-h-48 space-y-1 overflow-y-auto">
                                                {availableShipments.map(shipment => {
                                                    const checked = form.warehouseShipmentIds.includes(shipment.id);
                                                    return (
                                                        <label key={shipment.id} className={`flex cursor-pointer items-center gap-2 rounded-lg border px-2 py-1.5 text-xs ${checked ? 'border-primary bg-primary/5' : 'border-outline-variant/30'}`}>
                                                            <input
                                                                checked={checked}
                                                                className="h-4 w-4 accent-primary"
                                                                onChange={() => toggleShipment(shipment.id)}
                                                                type="checkbox"
                                                            />
                                                            <span className="font-bold">{shipment.qrCode}</span>
                                                            <span className="text-on-surface-variant">{shipment.receiverName}</span>
                                                            <span className="ml-auto text-on-surface-variant">{shipment.weightKg}kg / {shipment.volumeCbm}CBM</span>
                                                        </label>
                                                    );
                                                })}
                                            </div>
                                        )}
                                        {form.warehouseShipmentIds.length > 0 && (
                                            <p className="mt-2 text-xs font-bold text-primary">Đã chọn {form.warehouseShipmentIds.length} đơn hàng</p>
                                        )}
                                    </div>
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

function DateTimeField({ label, onChange, value }: { label: string; onChange: (value: string) => void; value: string }) {
    return <label className="flex flex-col gap-2"><span className="text-sm font-bold text-on-surface-variant">{label}</span><div className="flex h-11 items-center rounded-lg border border-outline-variant/50 bg-surface-container-low px-3"><span className="material-symbols-outlined mr-2 text-[19px] text-on-surface-variant">event</span><input className="w-full bg-transparent text-sm outline-none" onChange={event => onChange(event.target.value)} type="datetime-local" value={value} /></div></label>;
}

function TripDetail({ trip, driverName, hubNames, onEdit, onStatusChange, onUnlinkShipment, onUnlinkError, vehicleName }: { trip: Trip; driverName: string; hubNames: Record<string, string>; onEdit: () => void; onStatusChange: (status: TripStatus) => void; onUnlinkShipment: (shipmentId: string) => Promise<void>; onUnlinkError: (message: string) => void; vehicleName: string }) {
    const transitions = allowedTransitions[trip.status];

    const [shipments, setShipments] = useState<TripShipment[]>([]);
    const [loadingShipments, setLoadingShipments] = useState(false);
    const [unlinkingId, setUnlinkingId] = useState<string | null>(null);

    useEffect(() => {
        let cancelled = false;
        const load = async () => {
            setLoadingShipments(true);
            try {
                const data = await fetchTripShipments(trip.id);
                if (!cancelled) setShipments(data);
            } catch (error) {
                if (!cancelled) {
                    setShipments([]);
                    onUnlinkError(error instanceof Error ? error.message : 'Không thể tải danh sách đơn hàng.');
                }
            } finally {
                if (!cancelled) setLoadingShipments(false);
            }
        };
        void load();
        return () => { cancelled = true; };
    }, [trip.id, trip.version]);

    const handleUnlink = async (shipment: TripShipment) => {
        const confirm = window.confirm(
            `Gỡ đơn ${shipment.qrCode} khỏi chuyến ${trip.id.slice(0, 8).toUpperCase()}?\n\n` +
            `Đơn sẽ được chuyển về trạng thái "Trong kho" (In_Warehouse) để có thể ghép sang chuyến khác.`
        );
        if (!confirm) return;

        try {
            setUnlinkingId(shipment.shipmentId);
            await onUnlinkShipment(shipment.shipmentId);
            // Optimistic local update; loadData() in parent also refreshes shipments via the version effect.
            setShipments(current => current.filter(s => s.shipmentId !== shipment.shipmentId));
        } catch (error) {
            onUnlinkError(error instanceof Error ? error.message : 'Không thể gỡ đơn hàng.');
        } finally {
            setUnlinkingId(null);
        }
    };

    const transitionButtons: Partial<Record<TripStatus, { icon: string; label: string; className: string }>> = {
        Scheduled: { icon: 'event_available', label: 'Sẵn sàng', className: 'bg-primary text-on-primary' },
        Ready: { icon: 'play_arrow', label: 'Bắt đầu di chuyển', className: 'bg-secondary text-on-secondary' },
        InProgress: { icon: 'local_shipping', label: 'Bắt đầu vận chuyển', className: 'bg-secondary text-on-secondary' },
        Completed: { icon: 'task_alt', label: 'Hoàn thành', className: 'bg-secondary text-on-secondary' },
        Breakdown: { icon: 'warning', label: 'Sự cố', className: 'border border-error text-error hover:bg-error/5' },
        Cancelled: { icon: 'cancel', label: 'Hủy chuyến', className: 'border border-error text-error hover:bg-error/5' },
    };

    const unlinkAllowed = canUnlinkFromTrip(trip.status);
    const totalWeight = shipments.reduce((sum, s) => sum + s.weightKg, 0);
    const totalVolume = shipments.reduce((sum, s) => sum + s.volumeCbm, 0);
    const formatDateTime = (value: string | null) => {
        if (!value) return '—';
        const d = new Date(value);
        return isNaN(d.getTime()) ? '—' : d.toLocaleString('vi-VN');
    };

    return <section className="rounded-lg border border-outline-variant/30 bg-surface-container-lowest p-4 card-shadow md:p-5"><div className="mb-4 flex flex-col gap-3 border-b border-outline-variant/20 pb-4 sm:flex-row sm:items-start sm:justify-between"><div><p className="text-xs font-bold uppercase text-on-surface-variant">Chi tiết chuyến</p><h2 className="mt-1 text-xl font-bold text-primary">{trip.id.toUpperCase()}</h2><p className="mt-1 text-xs text-on-surface-variant">Mã nội bộ: {trip.id}</p></div><div className="flex flex-wrap gap-2">{trip.status !== 'Completed' && trip.status !== 'Cancelled' && <button className="flex items-center gap-2 rounded-lg border border-outline-variant px-3 py-2 text-sm font-bold text-primary hover:bg-primary/5" onClick={onEdit} type="button"><span className="material-symbols-outlined text-[18px]">edit</span>Sửa</button>}{transitions.map(target => { const cfg = transitionButtons[target]; return <button key={target} className={`flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-bold ${cfg?.className}`} onClick={() => onStatusChange(target)} type="button"><span className="material-symbols-outlined text-[18px]">{cfg?.icon}</span>{cfg?.label}</button>; })}</div></div><div className="mb-4 grid grid-cols-2 gap-3 md:grid-cols-4"><Fact label="Trạng thái" value={statusLabels[trip.status]} /><Fact label="Tài xế" value={driverName} /><Fact label="Xe" value={vehicleName} /><Fact label="Hub đi → đến" value={`${hubNames[trip.originHubId] || trip.originHubId.slice(0, 8)} → ${hubNames[trip.destHubId] || trip.destHubId.slice(0, 8)}`} /><Fact label="Khởi hành dự kiến" value={formatDateTime(trip.scheduledDepartureAt)} /><Fact label="Bắt đầu" value={formatDateTime(trip.startedAt)} /><Fact label="Hoàn thành" value={formatDateTime(trip.finishedAt)} /><Fact label="Cập nhật" value={formatDateTime(trip.updatedAt)} /></div><RouteMap destination={hubNames[trip.destHubId] || trip.destHubId} origin={hubNames[trip.originHubId] || trip.originHubId} routeLineString={trip.routeLineString} /><div className="mt-4"><div className="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between"><div><h3 className="text-base font-bold">Đơn hàng trong chuyến <span className="text-on-surface-variant">({shipments.length})</span></h3><p className="text-xs text-on-surface-variant">Tổng tải: {totalWeight.toLocaleString('vi-VN')} kg · {totalVolume.toLocaleString('vi-VN')} CBM</p></div>{unlinkAllowed && shipments.length > 0 && <p className="text-xs text-on-surface-variant">Bấm biểu tượng thùng rác để gỡ đơn (chỉ đơn còn ở trạng thái "Đã ghép chuyến").</p>}</div>{loadingShipments ? <div className="rounded-lg bg-surface-container-low p-6 text-center text-sm text-on-surface-variant">Đang tải đơn hàng...</div> : shipments.length === 0 ? <div className="rounded-lg border border-dashed border-outline-variant/40 bg-surface-container-low p-6 text-center text-sm text-on-surface-variant">Chuyến này chưa có đơn hàng nào.</div> : <div className="overflow-x-auto rounded-lg border border-outline-variant/20"><table className="w-full min-w-[760px] text-left text-sm"><thead className="bg-surface-container-low text-xs text-on-surface-variant"><tr><th className="px-3 py-2">Mã QR</th><th className="px-3 py-2">Loại hàng</th><th className="px-3 py-2">Tải / Thể tích</th><th className="px-3 py-2">Người gửi</th><th className="px-3 py-2">Người nhận</th><th className="px-3 py-2">Trạng thái</th><th className="px-3 py-2 text-center">Thao tác</th></tr></thead><tbody>{shipments.map(shipment => { const isMatched = shipment.status === 'Matched'; const canUnlink = unlinkAllowed && isMatched; return <tr className="border-t border-outline-variant/15 hover:bg-surface-container-low/40" key={shipment.shipmentId}><td className="px-3 py-2 font-bold text-primary">{shipment.qrCode}</td><td className="px-3 py-2">{shipment.cargoType || '—'}</td><td className="px-3 py-2 text-xs"><div>{shipment.weightKg.toLocaleString('vi-VN')} kg</div><div className="text-on-surface-variant">{shipment.volumeCbm} CBM</div></td><td className="px-3 py-2 text-xs"><div className="font-bold">{shipment.senderName || '—'}</div><div className="text-on-surface-variant">{shipment.senderPhone || ''}</div></td><td className="px-3 py-2 text-xs"><div className="font-bold">{shipment.receiverName || '—'}</div><div className="text-on-surface-variant">{shipment.receiverPhone || ''}</div></td><td className="px-3 py-2"><span className={`inline-flex rounded-full border px-2 py-0.5 text-xs font-bold ${shipmentStatusStyle[shipment.status] || 'border-outline/30 bg-surface-container-low text-on-surface-variant'}`}>{shipmentStatusLabels[shipment.status] || shipment.status}</span></td><td className="px-3 py-2 text-center">{canUnlink ? <button aria-label={`Gỡ đơn ${shipment.qrCode}`} className="flex h-8 w-8 items-center justify-center rounded-lg text-error hover:bg-error/10 disabled:opacity-50" disabled={unlinkingId === shipment.shipmentId} onClick={() => void handleUnlink(shipment)} title="Gỡ đơn khỏi chuyến" type="button"><span className={`material-symbols-outlined text-[19px] ${unlinkingId === shipment.shipmentId ? 'animate-spin' : ''}`}>{unlinkingId === shipment.shipmentId ? 'progress_activity' : 'delete'}</span></button> : <span className="text-xs text-on-surface-variant" title={isMatched ? 'Chuyến đã kết thúc, không thể gỡ' : 'Đơn đã vận chuyển, không thể gỡ từ chuyến'}>—</span>}</td></tr>; })}</tbody></table></div>}</div></section>;
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
