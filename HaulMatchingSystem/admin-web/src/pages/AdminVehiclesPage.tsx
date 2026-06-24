import { useCallback, useEffect, useMemo, useState } from 'react';
import {
    createVehicle,
    fetchVehicles,
    updateVehicle,
    type Vehicle,
    type VehiclePayload,
    type VehicleStatus
} from '../api/vehiclesApi';
import { fetchHubs, type Hub } from '../api/hubsApi';

type VehicleFormState = {
    code: string;
    licensePlate: string;
    hubId: string;
    vehicleType: string;
    maxWeightKg: string;
    maxVolumeCbm: string;
    status: VehicleStatus;
};

const emptyForm: VehicleFormState = {
    code: '',
    licensePlate: '',
    hubId: '',
    vehicleType: 'Box Truck',
    maxWeightKg: '',
    maxVolumeCbm: '',
    status: 'Available'
};

const statusOptions: { value: VehicleStatus | ''; label: string }[] = [
    { value: '', label: 'All statuses' },
    { value: 'Available', label: 'Available' },
    { value: 'InMaintenance', label: 'In maintenance' },
    { value: 'Inactive', label: 'Inactive' }
];

const vehicleTypeOptions = ['Box Truck', 'Van', 'Container Truck', 'Refrigerated Truck', 'Pickup'];
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5104';

const statusStyle: Record<VehicleStatus, string> = {
    Available: 'bg-secondary-container text-on-secondary-container border-secondary/20',
    InMaintenance: 'bg-tertiary-fixed text-on-tertiary-fixed-variant border-on-tertiary-container/30',
    Inactive: 'bg-error-container text-error border-error/20'
};

function formatNumber(value: number) {
    return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 }).format(value);
}

export default function AdminVehiclesPage() {
    const [vehicles, setVehicles] = useState<Vehicle[]>([]);
    const [hubs, setHubs] = useState<Hub[]>([]);
    const [search, setSearch] = useState('');
    const [statusFilter, setStatusFilter] = useState('');
    const [form, setForm] = useState<VehicleFormState>(emptyForm);
    const [editingVehicleId, setEditingVehicleId] = useState<string | null>(null);
    const [isLoadingVehicles, setIsLoadingVehicles] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [status, setStatus] = useState('Ready');
    const [message, setMessage] = useState(`Connected to ${apiBaseUrl}/api/vehicles`);

    const selectedVehicle = useMemo(
        () => vehicles.find((vehicle) => vehicle.id === editingVehicleId) ?? null,
        [editingVehicleId, vehicles]
    );
    const hubNames = useMemo(
        () => Object.fromEntries(hubs.map((hub) => [hub.id, hub.name])),
        [hubs]
    );

    const loadVehicles = useCallback(async (query = search, nextStatus = statusFilter) => {
        try {
            setIsLoadingVehicles(true);
            const data = await fetchVehicles(query, nextStatus);
            setVehicles(data);
            setStatus('Connected');
            setMessage(`${data.length} vehicles loaded`);
        } catch (error) {
            console.error(error);
            setVehicles([]);
            setStatus('API offline');
            setMessage(error instanceof Error ? error.message : 'Cannot load vehicles.');
        } finally {
            setIsLoadingVehicles(false);
        }
    }, [search, statusFilter]);

    useEffect(() => {
        const timer = window.setTimeout(() => {
            void loadVehicles(search, statusFilter);
        }, 250);

        return () => window.clearTimeout(timer);
    }, [loadVehicles, search, statusFilter]);

    useEffect(() => {
        const loadHubs = async () => {
            try {
                const data = await fetchHubs();
                setHubs(data);
                setForm((current) => current.hubId || data.length === 0
                    ? current
                    : { ...current, hubId: data[0].id });
            } catch (error) {
                console.error(error);
                setStatus('Hub load failed');
                setMessage(error instanceof Error ? error.message : 'Cannot load hubs for vehicle assignment.');
            }
        };

        void loadHubs();
    }, []);

    const updateForm = (field: keyof VehicleFormState, value: string) => {
        setForm((current) => ({ ...current, [field]: value }));
    };

    const resetForm = () => {
        setEditingVehicleId(null);
        setForm(emptyForm);
        setStatus('Ready');
        setMessage('Create a vehicle or select one to edit.');
    };

    const editVehicle = (vehicle: Vehicle) => {
        setEditingVehicleId(vehicle.id);
        setForm({
            code: vehicle.code,
            licensePlate: vehicle.licensePlate,
            hubId: vehicle.hubId,
            vehicleType: vehicle.vehicleType,
            maxWeightKg: String(vehicle.maxWeightKg),
            maxVolumeCbm: String(vehicle.maxVolumeCbm),
            status: vehicle.status
        });
        setStatus('Editing vehicle');
        setMessage(vehicle.code);
    };

    const buildPayload = (): VehiclePayload | null => {
        const maxWeightKg = Number(form.maxWeightKg);
        const maxVolumeCbm = Number(form.maxVolumeCbm);

        if (!form.code.trim()) {
            setStatus('Validation failed');
            setMessage('Vehicle code is required.');
            return null;
        }

        if (!form.licensePlate.trim()) {
            setStatus('Validation failed');
            setMessage('License plate is required.');
            return null;
        }

        if (!form.hubId) {
            setStatus('Validation failed');
            setMessage('Hub assignment is required.');
            return null;
        }

        if (!form.vehicleType.trim()) {
            setStatus('Validation failed');
            setMessage('Vehicle type is required.');
            return null;
        }

        if (!Number.isFinite(maxWeightKg) || maxWeightKg <= 0) {
            setStatus('Validation failed');
            setMessage('Max weight must be greater than 0.');
            return null;
        }

        if (!Number.isFinite(maxVolumeCbm) || maxVolumeCbm <= 0) {
            setStatus('Validation failed');
            setMessage('Max volume must be greater than 0.');
            return null;
        }

        return {
            code: form.code.trim(),
            licensePlate: form.licensePlate.trim(),
            hubId: form.hubId,
            vehicleType: form.vehicleType.trim(),
            maxWeightKg,
            maxVolumeCbm,
            status: form.status
        };
    };

    const submitForm = async () => {
        const payload = buildPayload();
        if (!payload) {
            return;
        }

        try {
            setIsSaving(true);
            if (editingVehicleId) {
                await updateVehicle(editingVehicleId, payload);
            } else {
                await createVehicle(payload);
            }

            await loadVehicles(search, statusFilter);
            setStatus(editingVehicleId ? 'Vehicle updated' : 'Vehicle created');
            setMessage(payload.code);
            resetForm();
        } catch (error) {
            console.error(error);
            setStatus('Save failed');
            setMessage(error instanceof Error ? error.message : 'Cannot save vehicle.');
        } finally {
            setIsSaving(false);
        }
    };

    const totals = useMemo(() => ({
        available: vehicles.filter((vehicle) => vehicle.status === 'Available').length,
        maintenance: vehicles.filter((vehicle) => vehicle.status === 'InMaintenance').length,
        totalWeight: vehicles.reduce((sum, vehicle) => sum + vehicle.maxWeightKg, 0)
    }), [vehicles]);

    return (
        <div className="min-h-screen bg-surface text-on-surface font-body-md">
            <header className="sticky top-0 z-20 border-b border-outline-variant/40 bg-surface-container-lowest/95 backdrop-blur">
                <div className="mx-auto flex max-w-[1440px] items-center justify-between gap-4 px-4 py-4 md:px-8">
                    <div className="flex items-center gap-3">
                        <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary text-on-primary">
                            <span className="material-symbols-outlined text-[22px]">local_shipping</span>
                        </div>
                        <div>
                            <h1 className="font-headline-md text-2xl font-semibold text-primary">Vehicle Management</h1>
                            <p className="text-sm text-on-surface-variant">Admin Operations</p>
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

            <main className="mx-auto grid max-w-[1440px] grid-cols-1 gap-4 px-4 py-4 md:px-8 xl:grid-cols-[minmax(0,1fr)_440px]">
                <section className="space-y-4">
                    <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
                        <MetricCard icon="local_shipping" label="Fleet records" value={String(vehicles.length)} />
                        <MetricCard icon="task_alt" label="Available" value={String(totals.available)} />
                        <MetricCard icon="scale" label="Total capacity kg" value={formatNumber(totals.totalWeight)} />
                    </div>

                    <section className="rounded-xl border border-outline-variant/30 bg-surface-container-lowest p-card-padding card-shadow">
                        <div className="mb-4 flex flex-col justify-between gap-3 md:flex-row md:items-center">
                            <div>
                                <h2 className="font-headline-md text-xl font-semibold text-on-surface">Vehicle List</h2>
                                <p className="text-sm text-on-surface-variant">
                                    {isLoadingVehicles ? 'Loading...' : `${vehicles.length} rows, ${totals.maintenance} in maintenance`}
                                </p>
                            </div>

                            <div className="flex flex-col gap-2 sm:flex-row">
                                <label className="flex w-full items-center gap-2 rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 py-2 md:w-72">
                                    <span className="material-symbols-outlined text-[20px] text-on-surface-variant">search</span>
                                    <input
                                        className="w-full bg-transparent text-sm outline-none"
                                        onChange={(event) => setSearch(event.target.value)}
                                        placeholder="Search vehicles"
                                        value={search}
                                    />
                                </label>

                                <select
                                    className="rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 py-2 text-sm outline-none"
                                    onChange={(event) => setStatusFilter(event.target.value)}
                                    value={statusFilter}
                                >
                                    {statusOptions.map((option) => (
                                        <option key={option.label} value={option.value}>{option.label}</option>
                                    ))}
                                </select>
                            </div>
                        </div>

                        <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
                            {vehicles.map((vehicle) => (
                                <article
                                    className="rounded-lg border border-outline-variant/30 bg-surface-container-low p-4"
                                    key={vehicle.id}
                                >
                                    <div className="mb-4 flex items-start justify-between gap-3">
                                        <div>
                                            <h3 className="font-headline-md text-lg font-semibold text-on-surface">{vehicle.code}</h3>
                                            <p className="mt-1 text-sm font-semibold text-primary">{vehicle.licensePlate}</p>
                                        </div>
                                        <span className={`rounded-full border px-3 py-1 text-xs font-semibold ${statusStyle[vehicle.status]}`}>
                                            {vehicle.status}
                                        </span>
                                    </div>

                                    <div className="grid grid-cols-2 gap-3 text-sm">
                                        <VehicleFact label="Type" value={vehicle.vehicleType} />
                                        <VehicleFact label="Hub" value={hubNames[vehicle.hubId] ?? vehicle.hubId.slice(0, 8)} />
                                        <VehicleFact label="Weight" value={`${formatNumber(vehicle.maxWeightKg)} kg`} />
                                        <VehicleFact label="Volume" value={`${formatNumber(vehicle.maxVolumeCbm)} cbm`} />
                                    </div>

                                    <div className="mt-4 flex justify-end">
                                        <button
                                            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border border-outline-variant/40 bg-surface-container-lowest text-primary transition-colors hover:bg-surface-container"
                                            onClick={() => editVehicle(vehicle)}
                                            title="Edit vehicle"
                                            type="button"
                                        >
                                            <span className="material-symbols-outlined text-[20px]">edit</span>
                                        </button>
                                    </div>
                                </article>
                            ))}
                        </div>

                        {!isLoadingVehicles && vehicles.length === 0 && (
                            <div className="flex min-h-[260px] flex-col items-center justify-center text-center">
                                <span className="material-symbols-outlined mb-2 text-[40px] text-on-surface-variant">local_shipping</span>
                                <p className="font-semibold text-on-surface">No vehicles loaded</p>
                                <p className="mt-1 text-sm text-on-surface-variant">{message}</p>
                            </div>
                        )}
                    </section>
                </section>

                <aside className="space-y-4">
                    <section className="rounded-xl border border-outline-variant/30 bg-surface-container-lowest p-card-padding card-shadow">
                        <div className="mb-4 flex items-start justify-between gap-3">
                            <div>
                                <h2 className="font-headline-md text-xl font-semibold text-on-surface">
                                    {editingVehicleId ? 'Update Vehicle' : 'Create Vehicle'}
                                </h2>
                                <p className="text-sm text-on-surface-variant">
                                    {selectedVehicle ? selectedVehicle.id : 'New fleet record'}
                                </p>
                            </div>
                            <button
                                className="flex h-9 w-9 items-center justify-center rounded-lg border border-outline-variant/40 bg-surface-container-low text-on-surface-variant hover:bg-surface-container"
                                onClick={resetForm}
                                title="Reset form"
                                type="button"
                            >
                                <span className="material-symbols-outlined text-[20px]">refresh</span>
                            </button>
                        </div>

                        <div className="space-y-4">
                            <TextField
                                label="Vehicle code"
                                onChange={(value) => updateForm('code', value)}
                                placeholder="HMS-TRK-01"
                                value={form.code}
                            />

                            <TextField
                                label="License plate"
                                onChange={(value) => updateForm('licensePlate', value)}
                                placeholder="51C-123.45"
                                value={form.licensePlate}
                            />

                            <label className="flex flex-col gap-2">
                                <span className="text-sm font-semibold text-on-surface-variant">Assigned hub</span>
                                <select
                                    className="rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 py-2 text-sm text-on-surface outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:opacity-60"
                                    disabled={hubs.length === 0}
                                    onChange={(event) => updateForm('hubId', event.target.value)}
                                    value={form.hubId}
                                >
                                    {hubs.length === 0 ? (
                                        <option value="">Create a hub first</option>
                                    ) : hubs.map((hub) => (
                                        <option key={hub.id} value={hub.id}>{hub.name}</option>
                                    ))}
                                </select>
                            </label>

                            <label className="flex flex-col gap-2">
                                <span className="text-sm font-semibold text-on-surface-variant">Vehicle type</span>
                                <select
                                    className="rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 py-2 text-sm text-on-surface outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20"
                                    onChange={(event) => updateForm('vehicleType', event.target.value)}
                                    value={form.vehicleType}
                                >
                                    {vehicleTypeOptions.map((type) => (
                                        <option key={type} value={type}>{type}</option>
                                    ))}
                                </select>
                            </label>

                            <div className="grid grid-cols-2 gap-3">
                                <TextField
                                    label="Max weight kg"
                                    onChange={(value) => updateForm('maxWeightKg', value)}
                                    type="number"
                                    value={form.maxWeightKg}
                                />
                                <TextField
                                    label="Max volume cbm"
                                    onChange={(value) => updateForm('maxVolumeCbm', value)}
                                    type="number"
                                    value={form.maxVolumeCbm}
                                />
                            </div>

                            <label className="flex flex-col gap-2">
                                <span className="text-sm font-semibold text-on-surface-variant">Status</span>
                                <select
                                    className="rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 py-2 text-sm text-on-surface outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20"
                                    onChange={(event) => updateForm('status', event.target.value)}
                                    value={form.status}
                                >
                                    {statusOptions.filter((option) => option.value).map((option) => (
                                        <option key={option.value} value={option.value}>{option.label}</option>
                                    ))}
                                </select>
                            </label>

                            <button
                                className="flex w-full items-center justify-center gap-2 rounded-lg bg-primary px-4 py-3 text-sm font-semibold text-on-primary transition-colors hover:bg-primary-container disabled:opacity-60"
                                disabled={isSaving}
                                onClick={() => void submitForm()}
                                type="button"
                            >
                                <span className="material-symbols-outlined text-[20px]">save</span>
                                {isSaving ? 'Saving...' : editingVehicleId ? 'Update Vehicle' : 'Create Vehicle'}
                            </button>
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

function VehicleFact({ label, value }: { label: string; value: string }) {
    return (
        <div className="rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-3">
            <p className="text-xs font-semibold uppercase text-on-surface-variant">{label}</p>
            <p className="mt-1 truncate font-semibold text-on-surface">{value}</p>
        </div>
    );
}

function TextField({
    label,
    onChange,
    placeholder,
    type = 'text',
    value
}: {
    label: string;
    onChange: (value: string) => void;
    placeholder?: string;
    type?: string;
    value: string;
}) {
    return (
        <label className="flex flex-col gap-2">
            <span className="text-sm font-semibold text-on-surface-variant">{label}</span>
            <input
                className="rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 py-2 text-sm text-on-surface outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20"
                min={type === 'number' ? '0' : undefined}
                onChange={(event) => onChange(event.target.value)}
                placeholder={placeholder}
                type={type}
                value={value}
            />
        </label>
    );
}
