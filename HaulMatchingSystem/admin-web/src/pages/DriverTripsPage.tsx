import { useEffect, useMemo, useRef, useState } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';

type TripStatus = 'Active' | 'Completed' | 'Breakdown';

interface DriverTrip {
    id: string;
    driverId: string;
    vehicleId: string;
    originHubId: string;
    destHubId: string;
    routeCode: string;
    originHub: string;
    destinationHub: string;
    status: TripStatus;
    startTime: string;
    eta: string;
    shipments: number;
    weightUsedKg: number;
    weightCapacityKg: number;
    volumeUsedCbm: number;
    volumeCapacityCbm: number;
    nextStop: string;
    distanceKm: number;
    routeLineString: string;
}

interface ApiTrip {
    id: string;
    driverId: string;
    vehicleId: string;
    originHubId: string;
    destHubId: string;
    routeLineString: string;
    currentLoadWeightKg: number;
    currentLoadVolumeCbm: number;
    startedAt: string | null;
    finishedAt: string | null;
    version: number;
    status: TripStatus;
    createdAt: string;
    updatedAt: string;
}

interface TripFormState {
    driverId: string;
    vehicleId: string;
    originHubId: string;
    destHubId: string;
    routeLineString: string;
    currentLoadWeightKg: string;
    currentLoadVolumeCbm: string;
}

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5104';

const hubOptions = [
    { id: '10000000-0000-0000-0000-000000000001', name: 'Ho Chi Minh Hub' },
    { id: '10000000-0000-0000-0000-000000000002', name: 'Da Nang Hub' },
    { id: '10000000-0000-0000-0000-000000000003', name: 'Ha Noi Hub' },
    { id: '10000000-0000-0000-0000-000000000004', name: 'Can Tho Hub' }
];

const driverOptions = [
    { id: '20000000-0000-0000-0000-000000000001', name: 'Driver Nguyen Minh' },
    { id: '20000000-0000-0000-0000-000000000002', name: 'Driver Tran Lan' }
];

const currentDriver = driverOptions[0];

const vehicleOptions = [
    { id: '30000000-0000-0000-0000-000000000001', name: 'HMS-TRK-01', weight: 7000, volume: 42 },
    { id: '30000000-0000-0000-0000-000000000002', name: 'HMS-TRK-02', weight: 5000, volume: 30 },
    { id: '30000000-0000-0000-0000-000000000003', name: 'HMS-TRK-03', weight: 6500, volume: 38 }
];

const hubNames = Object.fromEntries(hubOptions.map((hub) => [hub.id, hub.name]));

const vehicleCapacity: Record<string, { weight: number; volume: number }> = Object.fromEntries(
    vehicleOptions.map((vehicle) => [vehicle.id, { weight: vehicle.weight, volume: vehicle.volume }])
);

const defaultTripForm: TripFormState = {
    driverId: currentDriver.id,
    vehicleId: vehicleOptions[0].id,
    originHubId: hubOptions[0].id,
    destHubId: hubOptions[1].id,
    routeLineString: '',
    currentLoadWeightKg: '0',
    currentLoadVolumeCbm: '0'
};

const tabs = ['Trips', 'Loads', 'GPS', 'Alerts'] as const;

const statusStyle: Record<TripStatus, string> = {
    Active: 'bg-secondary-container text-on-secondary-container border-secondary/20',
    Completed: 'bg-primary-fixed text-on-primary-fixed border-primary/20',
    Breakdown: 'bg-error-container text-error border-error/20'
};

type RouteCoordinate = [number, number];

function percentage(used: number, capacity: number) {
    return Math.min(100, Math.round((used / capacity) * 100));
}

function mapApiTrip(trip: ApiTrip): DriverTrip {
    const originHub = hubNames[trip.originHubId] ?? shortId(trip.originHubId);
    const destinationHub = hubNames[trip.destHubId] ?? shortId(trip.destHubId);
    const capacity = vehicleCapacity[trip.vehicleId] ?? {
        weight: Math.max(7000, trip.currentLoadWeightKg),
        volume: Math.max(42, trip.currentLoadVolumeCbm)
    };

    return {
        id: trip.id,
        driverId: trip.driverId,
        vehicleId: trip.vehicleId,
        originHubId: trip.originHubId,
        destHubId: trip.destHubId,
        routeCode: `${hubCode(originHub)} -> ${hubCode(destinationHub)}`,
        originHub,
        destinationHub,
        status: trip.status,
        startTime: formatTime(trip.startedAt ?? trip.createdAt),
        eta: trip.finishedAt ? formatTime(trip.finishedAt) : 'In progress',
        shipments: 0,
        weightUsedKg: trip.currentLoadWeightKg,
        weightCapacityKg: capacity.weight,
        volumeUsedCbm: trip.currentLoadVolumeCbm,
        volumeCapacityCbm: capacity.volume,
        nextStop: trip.status === 'Completed' ? 'Closed at destination hub' : destinationHub,
        distanceKm: estimateDistanceKm(trip.routeLineString),
        routeLineString: trip.routeLineString
    };
}

function formatTime(value: string) {
    return new Date(value).toLocaleTimeString('vi-VN', {
        hour: '2-digit',
        minute: '2-digit'
    });
}

function hubCode(name: string) {
    return name
        .split(' ')
        .filter((part) => /^[A-Z]/.test(part))
        .map((part) => part[0])
        .join('')
        .slice(0, 3)
        .padEnd(3, 'X');
}

function shortId(id: string) {
    return id.slice(0, 8);
}

function parseLineStringCoordinates(routeLineString: string): RouteCoordinate[] {
    return routeLineString
        .replace(/^LINESTRING\s*\(/i, '')
        .replace(/\)$/, '')
        .split(',')
        .map((point) => point.trim().split(/\s+/).map(Number))
        .filter((point): point is RouteCoordinate => point.length === 2 && point.every(Number.isFinite));
}

function estimateDistanceKm(routeLineString: string) {
    const coordinates = parseLineStringCoordinates(routeLineString);

    if (coordinates.length < 2) {
        return 0;
    }

    let total = 0;
    for (let index = 1; index < coordinates.length; index++) {
        total += haversineKm(coordinates[index - 1], coordinates[index]);
    }

    return Math.round(total);
}

function haversineKm(from: number[], to: number[]) {
    const earthRadiusKm = 6371;
    const [fromLng, fromLat] = from;
    const [toLng, toLat] = to;
    const latDelta = toRadians(toLat - fromLat);
    const lngDelta = toRadians(toLng - fromLng);
    const a =
        Math.sin(latDelta / 2) ** 2 +
        Math.cos(toRadians(fromLat)) * Math.cos(toRadians(toLat)) * Math.sin(lngDelta / 2) ** 2;

    return 2 * earthRadiusKm * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

function toRadians(value: number) {
    return value * (Math.PI / 180);
}

function RouteMap({ trip }: { trip: DriverTrip }) {
    const coordinates = parseLineStringCoordinates(trip.routeLineString);
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
            zoomControl: true
        });
        mapRef.current = map;

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap contributors'
        }).addTo(map);

        const routeLine = L.polyline(routeLatLngs, {
            color: '#2563eb',
            weight: 6,
            opacity: 0.9,
            lineCap: 'round',
            lineJoin: 'round'
        }).addTo(map);

        L.circleMarker(routeLatLngs[0], {
            radius: 8,
            color: '#0f766e',
            fillColor: '#14b8a6',
            fillOpacity: 1,
            weight: 3
        })
            .bindTooltip(trip.originHub)
            .addTo(map);

        L.circleMarker(routeLatLngs[routeLatLngs.length - 1], {
            radius: 8,
            color: '#b91c1c',
            fillColor: '#ef4444',
            fillOpacity: 1,
            weight: 3
        })
            .bindTooltip(trip.destinationHub)
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
    }, [trip.routeLineString, trip.originHub, trip.destinationHub]);

    if (coordinates.length < 2) {
        return (
            <div className="min-h-[260px] rounded-lg bg-surface-container-low border border-outline-variant/30 flex items-center justify-center text-center px-6">
                <div>
                    <span className="material-symbols-outlined text-[32px] text-on-surface-variant mb-2">route</span>
                    <p className="text-label-lg font-label-lg text-on-surface">Route unavailable</p>
                    <p className="text-body-md font-body-md text-on-surface-variant mt-1">LINESTRING has fewer than two points</p>
                </div>
            </div>
        );
    }

    return (
        <div className="rounded-lg bg-surface-container-low border border-outline-variant/30 overflow-hidden">
            <div className="flex items-center justify-between gap-3 px-4 py-3 border-b border-outline-variant/30">
                <div>
                    <p className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Route Map</p>
                    <p className="text-label-lg font-label-lg text-on-surface mt-0.5">{trip.originHub} to {trip.destinationHub}</p>
                </div>
                <div className="text-right">
                    <p className="text-label-md font-label-md text-on-surface-variant">Distance</p>
                    <p className="text-label-lg font-label-lg text-primary">{trip.distanceKm} km</p>
                </div>
            </div>
            <div className="relative h-[360px] bg-surface-container-lowest">
                <div ref={mapContainerRef} className="absolute inset-0 z-0" />
                <div className="absolute left-4 top-4 rounded bg-surface-container-lowest/90 border border-outline-variant/30 px-3 py-2">
                    <p className="text-label-md font-label-md text-secondary">Start</p>
                    <p className="text-body-md font-body-md text-on-surface">{trip.originHub}</p>
                </div>
                <div className="absolute bottom-4 right-4 rounded bg-surface-container-lowest/90 border border-outline-variant/30 px-3 py-2 text-right">
                    <p className="text-label-md font-label-md text-error">Destination</p>
                    <p className="text-body-md font-body-md text-on-surface">{trip.destinationHub}</p>
                </div>
            </div>
        </div>
    );
}

<<<<<<< HEAD
function RouteMap({ trip }: { trip: DriverTrip }) {
    const coordinates = parseLineStringCoordinates(trip.routeLineString);
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
            zoomControl: true
        });
        mapRef.current = map;

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap contributors'
        }).addTo(map);

        const routeLine = L.polyline(routeLatLngs, {
            color: '#2563eb',
            weight: 6,
            opacity: 0.9,
            lineCap: 'round',
            lineJoin: 'round'
        }).addTo(map);

        L.circleMarker(routeLatLngs[0], {
            radius: 8,
            color: '#0f766e',
            fillColor: '#14b8a6',
            fillOpacity: 1,
            weight: 3
        })
            .bindTooltip(trip.originHub)
            .addTo(map);

        L.circleMarker(routeLatLngs[routeLatLngs.length - 1], {
            radius: 8,
            color: '#b91c1c',
            fillColor: '#ef4444',
            fillOpacity: 1,
            weight: 3
        })
            .bindTooltip(trip.destinationHub)
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
    }, [trip.routeLineString, trip.originHub, trip.destinationHub]);

    if (coordinates.length < 2) {
        return (
            <div className="min-h-[260px] rounded-lg bg-surface-container-low border border-outline-variant/30 flex items-center justify-center text-center px-6">
                <div>
                    <span className="material-symbols-outlined text-[32px] text-on-surface-variant mb-2">route</span>
                    <p className="text-label-lg font-label-lg text-on-surface">Route unavailable</p>
                    <p className="text-body-md font-body-md text-on-surface-variant mt-1">LINESTRING has fewer than two points</p>
                </div>
            </div>
        );
    }

    return (
        <div className="rounded-lg bg-surface-container-low border border-outline-variant/30 overflow-hidden">
            <div className="flex items-center justify-between gap-3 px-4 py-3 border-b border-outline-variant/30">
                <div>
                    <p className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Route Map</p>
                    <p className="text-label-lg font-label-lg text-on-surface mt-0.5">{trip.originHub} to {trip.destinationHub}</p>
                </div>
                <div className="text-right">
                    <p className="text-label-md font-label-md text-on-surface-variant">Distance</p>
                    <p className="text-label-lg font-label-lg text-primary">{trip.distanceKm} km</p>
                </div>
            </div>
            <div className="relative h-[360px] bg-surface-container-lowest">
                <div ref={mapContainerRef} className="absolute inset-0 z-0" />
                <div className="absolute left-4 top-4 rounded bg-surface-container-lowest/90 border border-outline-variant/30 px-3 py-2">
                    <p className="text-label-md font-label-md text-secondary">Start</p>
                    <p className="text-body-md font-body-md text-on-surface">{trip.originHub}</p>
                </div>
                <div className="absolute bottom-4 right-4 rounded bg-surface-container-lowest/90 border border-outline-variant/30 px-3 py-2 text-right">
                    <p className="text-label-md font-label-md text-error">Destination</p>
                    <p className="text-body-md font-body-md text-on-surface">{trip.destinationHub}</p>
                </div>
            </div>
        </div>
    );
}


=======
export default function DriverTripsPage({ onLogout, onBackToAdmin }: { onLogout?: () => void; onBackToAdmin?: () => void }) {
>>>>>>> Dev
    const [activeTab, setActiveTab] = useState<(typeof tabs)[number]>('Trips');
    const [trips, setTrips] = useState<DriverTrip[]>([]);
    const [apiStatus, setApiStatus] = useState('Loading trips...');
    const [apiMessage, setApiMessage] = useState(`Fetching ${apiBaseUrl}/api/trips?driverId=${currentDriver.id}`);
    const [editingTripId, setEditingTripId] = useState<string | null>(null);
    const [tripForm, setTripForm] = useState<TripFormState>(defaultTripForm);

    const loadTrips = async () => {
        try {
            const response = await fetch(`${apiBaseUrl}/api/trips?driverId=${currentDriver.id}`);
            if (!response.ok) {
                throw new Error(`API returned ${response.status}`);
            }

            const apiTrips = await response.json() as ApiTrip[];
            setTrips(apiTrips.map(mapApiTrip));
            setApiStatus('Connected');
            setApiMessage(`${apiTrips.length} trips loaded from database`);
        } catch (error) {
            console.error(error);
            setTrips([]);
            setApiStatus('API offline');
            setApiMessage(`Cannot reach ${apiBaseUrl}/api/trips?driverId=${currentDriver.id}. Start the API with: dotnet run --project src/HMS.API/HMS.API.csproj --launch-profile http`);
        }
    };

    useEffect(() => {
        void loadTrips();
    }, []);

    const changeTripStatus = async (trip: DriverTrip, status: Exclude<TripStatus, 'Active'>) => {
        try {
            const response = await fetch(`${apiBaseUrl}/api/trips/${trip.id}/status`, {
                method: 'PATCH',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    status,
                    occurredAt: new Date().toISOString()
                })
            });

            if (!response.ok) {
                throw new Error(`API returned ${response.status}`);
            }

            await loadTrips();
        } catch (error) {
            console.error(error);
            setApiStatus('Action failed');
            setApiMessage('Trip status update failed. Check that the trip is Active and the API is running.');
        }
    };

    const resetTripForm = () => {
        setEditingTripId(null);
        setTripForm(defaultTripForm);
    };

    const updateTripForm = (field: keyof TripFormState, value: string) => {
        setTripForm((current) => ({ ...current, [field]: value }));
    };

    const editTrip = (trip: DriverTrip) => {
        setEditingTripId(trip.id);
        setTripForm({
            driverId: currentDriver.id,
            vehicleId: trip.vehicleId,
            originHubId: trip.originHubId,
            destHubId: trip.destHubId,
            routeLineString: trip.routeLineString,
            currentLoadWeightKg: String(trip.weightUsedKg),
            currentLoadVolumeCbm: String(trip.volumeUsedCbm)
        });
        setApiMessage(`Editing trip ${trip.id}`);
    };

    const submitTripForm = async () => {
        try {
            const url = editingTripId ? `${apiBaseUrl}/api/trips/${editingTripId}` : `${apiBaseUrl}/api/trips`;
            const response = await fetch(url, {
                method: editingTripId ? 'PUT' : 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    driverId: tripForm.driverId,
                    vehicleId: tripForm.vehicleId,
                    originHubId: tripForm.originHubId,
                    destHubId: tripForm.destHubId,
                    routeLineString: tripForm.routeLineString,
                    currentLoadWeightKg: Number(tripForm.currentLoadWeightKg),
                    currentLoadVolumeCbm: Number(tripForm.currentLoadVolumeCbm)
                })
            });

            if (!response.ok) {
                throw new Error(`API returned ${response.status}`);
            }

            resetTripForm();
            await loadTrips();
            setApiStatus('Connected');
            setApiMessage(editingTripId ? 'Trip updated in database' : 'Trip created in database');
        } catch (error) {
            console.error(error);
            setApiStatus('Action failed');
            setApiMessage('Create/update failed. Check API connectivity, numeric load values, and that only Active trips are editable.');
        }
    };

    const deleteTrip = async (trip: DriverTrip) => {
        if (!window.confirm(`Delete trip ${trip.id}?`)) {
            return;
        }

        try {
            const response = await fetch(`${apiBaseUrl}/api/trips/${trip.id}`, {
                method: 'DELETE'
            });

            if (!response.ok) {
                throw new Error(`API returned ${response.status}`);
            }

            if (editingTripId === trip.id) {
                resetTripForm();
            }

            await loadTrips();
            setApiStatus('Connected');
            setApiMessage(`Trip ${trip.id} deleted from database`);
        } catch (error) {
            console.error(error);
            setApiStatus('Action failed');
            setApiMessage('Delete failed. Check that the API is running.');
        }
    };

    const activeTrip = trips.find((trip) => trip.status === 'Active') ?? trips[0];
    const tripStats = useMemo(() => ({
        active: trips.filter((trip) => trip.status === 'Active').length,
        completed: trips.filter((trip) => trip.status === 'Completed').length,
        exceptions: trips.filter((trip) => trip.status === 'Breakdown').length,
        assignedShipments: trips.reduce((total, trip) => total + trip.shipments, 0)
    }), [trips]);

    const weightPercent = activeTrip ? percentage(activeTrip.weightUsedKg, activeTrip.weightCapacityKg) : 0;
    const volumePercent = activeTrip ? percentage(activeTrip.volumeUsedCbm, activeTrip.volumeCapacityCbm) : 0;

    return (
        <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden">
            <nav className="bg-surface-container-lowest border-r border-outline-variant fixed left-0 h-full w-64 flex-col py-6 px-4 z-20 hidden xl:flex">
                <div className="mb-8 flex items-center gap-3 px-2">
                    <div className="w-8 h-8 rounded bg-primary flex items-center justify-center text-on-primary">
                        <span className="material-symbols-outlined text-[20px]">local_shipping</span>
                    </div>
                    <div>
                        <h1 className="text-headline-lg font-headline-lg text-primary">Ghep Chuyen</h1>
                        <p className="text-label-md font-label-md text-on-surface-variant">Driver Console</p>
                    </div>
                </div>

                {onBackToAdmin && (
                    <button
                        onClick={onBackToAdmin}
                        className="w-full flex items-center gap-3 px-4 py-3 rounded-xl border border-dashed border-primary text-primary hover:bg-surface-container-low transition-all mb-4 text-left"
                    >
                        <span className="material-symbols-outlined text-lg">admin_panel_settings</span>
                        <span className="text-label-lg font-bold">Admin Console</span>
                    </button>
                )}

                <button className="w-full bg-primary hover:bg-primary-container text-on-primary text-label-lg font-label-lg py-3 rounded-lg mb-6 transition-colors flex items-center justify-center gap-2">
                    <span className="material-symbols-outlined">near_me</span>
                    Start GPS Ping
                </button>

                <div className="flex-1 space-y-1">
                    {tabs.map((tab) => (
                        <button
                            className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg transition-colors duration-200 text-left ${
                                activeTab === tab
                                    ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low'
                                    : 'text-on-surface-variant hover:bg-surface-container'
                            }`}
                            key={tab}
                            onClick={() => setActiveTab(tab)}
                            type="button"
                        >
                            <span className="material-symbols-outlined">
                                {tab === 'Trips' ? 'route' : tab === 'Loads' ? 'package_2' : tab === 'GPS' ? 'my_location' : 'campaign'}
                            </span>
                            <span className="text-label-lg font-label-lg">{tab}</span>
                        </button>
                    ))}
                </div>
            </nav>

            <div className="flex-1 flex flex-col xl:ml-64 w-full">
                <header className="bg-surface-container-lowest border-b border-outline-variant h-16 w-full flex justify-between items-center px-4 md:px-8 sticky top-0 z-10">
                    <div className="flex items-center bg-surface-container-low rounded-lg px-3 py-1.5 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 w-full max-w-sm transition-all">
                        <span className="material-symbols-outlined text-on-surface-variant text-[20px] mr-2">search</span>
                        <input className="bg-transparent border-none outline-none text-body-md font-body-md w-full placeholder-on-surface-variant/70 focus:ring-0 p-0 text-on-surface" placeholder="Search trips..." type="text" />
                    </div>
                    <div className="flex items-center gap-4 md:gap-6 ml-4">
                        <div className="hidden sm:flex items-center gap-2 text-label-md font-label-md text-on-surface-variant bg-surface px-3 py-1 rounded-full border border-outline-variant/30">
                            <span className={`w-2 h-2 rounded-full ${apiStatus === 'Connected' ? 'bg-secondary live-pulse' : 'bg-error'}`}></span>
                            {apiStatus}
                        </div>
                        {onLogout && (
                            <button 
                                onClick={onLogout}
                                className="w-10 h-10 rounded-full flex items-center justify-center hover:bg-surface-container text-error transition-colors"
                                title="Đăng xuất"
                            >
                                <span className="material-symbols-outlined">logout</span>
                            </button>
                        )}
                        <div className="w-8 h-8 rounded-full bg-primary-fixed text-primary overflow-hidden border border-outline-variant/50 flex items-center justify-center">
                            <span className="material-symbols-outlined text-[20px]">person</span>
                        </div>
                    </div>
                </header>

                <main className="flex-1 p-container-margin overflow-y-auto">
                    <div className="flex flex-col lg:flex-row lg:justify-between lg:items-end mb-6 gap-4">
                        <div>
                            <div className="flex items-center text-label-md font-label-md text-on-surface-variant mb-1">
                                <span>Driver</span>
                                <span className="material-symbols-outlined text-[14px] mx-1">chevron_right</span>
                                <span className="text-primary font-bold">{activeTab}</span>
                            </div>
                            <h2 className="text-headline-lg font-headline-lg text-on-surface">My Trips</h2>
                        </div>

                        <div className="flex items-center gap-2 overflow-x-auto pb-1">
                            {tabs.map((tab) => (
                                <button
                                    className={`px-4 py-2 rounded-lg text-label-lg font-label-lg border transition-colors ${
                                        activeTab === tab
                                            ? 'bg-primary text-on-primary border-primary'
                                            : 'bg-surface-container-lowest text-on-surface-variant border-outline-variant/40 hover:bg-surface-container'
                                    }`}
                                    key={tab}
                                    onClick={() => setActiveTab(tab)}
                                    type="button"
                                >
                                    {tab}
                                </button>
                            ))}
                        </div>
                    </div>

                    <div className="mb-gutter bg-surface-container-lowest rounded-xl p-4 card-shadow border border-outline-variant/20 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
                        <div className="flex items-start gap-3">
                            <span className={`material-symbols-outlined text-[20px] ${apiStatus === 'Connected' ? 'text-secondary' : 'text-error'}`}>
                                {apiStatus === 'Connected' ? 'database' : 'sync_problem'}
                            </span>
                            <div>
                                <p className="text-label-lg font-label-lg text-on-surface">Database source</p>
                                <p className="text-body-md font-body-md text-on-surface-variant mt-1">{apiMessage}</p>
                            </div>
                        </div>
                        <button
                            className="bg-surface-container hover:bg-surface-container-high text-primary text-label-lg font-label-lg px-4 py-2 rounded-lg transition-colors flex items-center justify-center gap-2 border border-outline-variant/30"
                            onClick={() => void loadTrips()}
                            type="button"
                        >
                            <span className="material-symbols-outlined text-[20px]">refresh</span>
                            Reload
                        </button>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-gutter mb-gutter">
                        <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20">
                            <div className="flex justify-between items-start mb-4">
                                <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Active Trips</span>
                                <div className="w-8 h-8 rounded-full bg-primary-fixed flex items-center justify-center text-primary">
                                    <span className="material-symbols-outlined text-[20px]">route</span>
                                </div>
                            </div>
                            <span className="text-display-lg font-display-lg text-on-surface">{tripStats.active}</span>
                        </div>

                        <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20">
                            <div className="flex justify-between items-start mb-4">
                                <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Assigned Shipments</span>
                                <div className="w-8 h-8 rounded-full bg-surface-container-high flex items-center justify-center text-primary">
                                    <span className="material-symbols-outlined text-[20px]">package_2</span>
                                </div>
                            </div>
                            <span className="text-display-lg font-display-lg text-on-surface">{tripStats.assignedShipments}</span>
                        </div>

                        <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20">
                            <div className="flex justify-between items-start mb-4">
                                <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Completed</span>
                                <div className="w-8 h-8 rounded-full bg-secondary-container flex items-center justify-center text-on-secondary-container">
                                    <span className="material-symbols-outlined text-[20px]">task_alt</span>
                                </div>
                            </div>
                            <span className="text-display-lg font-display-lg text-on-surface">{tripStats.completed}</span>
                        </div>

                        <div className="bg-error-container/20 rounded-xl p-card-padding card-shadow border border-error/20">
                            <div className="flex justify-between items-start mb-4">
                                <span className="text-label-md font-label-md text-error uppercase tracking-wider font-bold">Exceptions</span>
                                <div className="w-8 h-8 rounded-full bg-error text-on-error flex items-center justify-center">
                                    <span className="material-symbols-outlined text-[20px]">warning</span>
                                </div>
                            </div>
                            <span className="text-display-lg font-display-lg text-error">{tripStats.exceptions}</span>
                        </div>
                    </div>

                    <section className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 mb-gutter">
                        <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-3 mb-4 pb-3 border-b border-outline-variant/30">
                            <div>
                                <h3 className="text-headline-md font-headline-md text-on-surface flex items-center gap-2">
                                    <span className="material-symbols-outlined text-primary">{editingTripId ? 'edit' : 'add_circle'}</span>
                                    {editingTripId ? 'Edit Trip' : 'Create Trip'}
                                </h3>
                                <p className="text-body-md font-body-md text-on-surface-variant mt-1">
                                    {editingTripId ?? 'New trip will be inserted into transport.trips'}
                                </p>
                            </div>
                            <div className="flex gap-2">
                                <button
                                    className="bg-surface-container hover:bg-surface-container-high text-primary text-label-lg font-label-lg px-4 py-2 rounded-lg transition-colors flex items-center justify-center gap-2 border border-outline-variant/30"
                                    onClick={resetTripForm}
                                    type="button"
                                >
                                    <span className="material-symbols-outlined text-[20px]">restart_alt</span>
                                    Reset
                                </button>
                                <button
                                    className="bg-primary hover:bg-primary-container text-on-primary text-label-lg font-label-lg px-4 py-2 rounded-lg transition-colors flex items-center justify-center gap-2"
                                    onClick={() => void submitTripForm()}
                                    type="button"
                                >
                                    <span className="material-symbols-outlined text-[20px]">save</span>
                                    {editingTripId ? 'Save' : 'Create'}
                                </button>
                            </div>
                        </div>

                        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
                            <div className="flex flex-col gap-2">
                                <span className="text-label-md font-label-md text-on-surface-variant">Signed-in Driver</span>
                                <div className="bg-surface-container-low border border-outline-variant/50 rounded-lg px-3 py-2 min-h-[42px] flex items-center justify-between gap-3">
                                    <span className="text-body-md font-body-md text-on-surface">{currentDriver.name}</span>
                                    <span className="material-symbols-outlined text-primary text-[20px]">verified_user</span>
                                </div>
                            </div>

                            <label className="flex flex-col gap-2">
                                <span className="text-label-md font-label-md text-on-surface-variant">Vehicle</span>
                                <select
                                    className="bg-surface-container-low border border-outline-variant/50 rounded-lg px-3 py-2 text-body-md font-body-md text-on-surface outline-none focus:ring-2 focus:ring-primary"
                                    onChange={(event) => updateTripForm('vehicleId', event.target.value)}
                                    value={tripForm.vehicleId}
                                >
                                    {vehicleOptions.map((vehicle) => (
                                        <option key={vehicle.id} value={vehicle.id}>{vehicle.name}</option>
                                    ))}
                                </select>
                            </label>

                            <label className="flex flex-col gap-2">
                                <span className="text-label-md font-label-md text-on-surface-variant">Origin Hub</span>
                                <select
                                    className="bg-surface-container-low border border-outline-variant/50 rounded-lg px-3 py-2 text-body-md font-body-md text-on-surface outline-none focus:ring-2 focus:ring-primary"
                                    onChange={(event) => updateTripForm('originHubId', event.target.value)}
                                    value={tripForm.originHubId}
                                >
                                    {hubOptions.map((hub) => (
                                        <option key={hub.id} value={hub.id}>{hub.name}</option>
                                    ))}
                                </select>
                            </label>

                            <label className="flex flex-col gap-2">
                                <span className="text-label-md font-label-md text-on-surface-variant">Destination Hub</span>
                                <select
                                    className="bg-surface-container-low border border-outline-variant/50 rounded-lg px-3 py-2 text-body-md font-body-md text-on-surface outline-none focus:ring-2 focus:ring-primary"
                                    onChange={(event) => updateTripForm('destHubId', event.target.value)}
                                    value={tripForm.destHubId}
                                >
                                    {hubOptions.map((hub) => (
                                        <option key={hub.id} value={hub.id}>{hub.name}</option>
                                    ))}
                                </select>
                            </label>

                            <label className="flex flex-col gap-2 md:col-span-2">
                                <span className="text-label-md font-label-md text-on-surface-variant">Route LINESTRING optional</span>
                                <input
                                    className="bg-surface-container-low border border-outline-variant/50 rounded-lg px-3 py-2 text-body-md font-body-md text-on-surface outline-none focus:ring-2 focus:ring-primary"
                                    onChange={(event) => updateTripForm('routeLineString', event.target.value)}
                                    value={tripForm.routeLineString}
                                />
                            </label>

                            <label className="flex flex-col gap-2">
                                <span className="text-label-md font-label-md text-on-surface-variant">Current Weight Kg</span>
                                <input
                                    className="bg-surface-container-low border border-outline-variant/50 rounded-lg px-3 py-2 text-body-md font-body-md text-on-surface outline-none focus:ring-2 focus:ring-primary"
                                    min="0"
                                    onChange={(event) => updateTripForm('currentLoadWeightKg', event.target.value)}
                                    type="number"
                                    value={tripForm.currentLoadWeightKg}
                                />
                            </label>

                            <label className="flex flex-col gap-2">
                                <span className="text-label-md font-label-md text-on-surface-variant">Current Volume Cbm</span>
                                <input
                                    className="bg-surface-container-low border border-outline-variant/50 rounded-lg px-3 py-2 text-body-md font-body-md text-on-surface outline-none focus:ring-2 focus:ring-primary"
                                    min="0"
                                    onChange={(event) => updateTripForm('currentLoadVolumeCbm', event.target.value)}
                                    type="number"
                                    value={tripForm.currentLoadVolumeCbm}
                                />
                            </label>
                        </div>
                    </section>

                    <div className="grid grid-cols-1 xl:grid-cols-12 gap-gutter mb-8">
                        <section className="xl:col-span-7 bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20">
                            {activeTrip ? (
                                <>
                                    <div className="flex items-start justify-between gap-4 pb-4 mb-4 border-b border-outline-variant/30">
                                        <div>
                                            <p className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Current Route</p>
                                            <h3 className="text-headline-md font-headline-md text-on-surface mt-1">{activeTrip.routeCode}</h3>
                                        </div>
                                        <span className={`px-3 py-1 rounded-full border text-label-md font-label-md ${statusStyle[activeTrip.status]}`}>
                                            {activeTrip.status}
                                        </span>
                                    </div>

                                    <div className="grid grid-cols-1 md:grid-cols-[1fr_auto_1fr] gap-4 items-center mb-6">
                                        <div className="bg-surface-container-low rounded-lg p-4 border border-outline-variant/30">
                                            <p className="text-label-md font-label-md text-on-surface-variant">Origin</p>
                                            <p className="text-label-lg font-label-lg text-on-surface mt-1">{activeTrip.originHub}</p>
                                            <p className="text-body-md font-body-md text-primary mt-2">{activeTrip.startTime}</p>
                                        </div>
                                        <div className="hidden md:flex items-center justify-center text-primary">
                                            <span className="material-symbols-outlined text-[32px]">east</span>
                                        </div>
                                        <div className="bg-surface-container-low rounded-lg p-4 border border-outline-variant/30">
                                            <p className="text-label-md font-label-md text-on-surface-variant">Destination</p>
                                            <p className="text-label-lg font-label-lg text-on-surface mt-1">{activeTrip.destinationHub}</p>
                                            <p className="text-body-md font-body-md text-primary mt-2">{activeTrip.eta}</p>
                                        </div>
                                    </div>

                                    <div className="mb-6">
                                        <RouteMap trip={activeTrip} />
                                    </div>

                                    <div className="space-y-4">
                                        <div>
                                            <div className="flex justify-between text-label-md font-label-md mb-2">
                                                <span className="text-on-surface-variant">Weight Load</span>
                                                <span className="text-on-surface">{activeTrip.weightUsedKg}/{activeTrip.weightCapacityKg} kg</span>
                                            </div>
                                            <div className="w-full bg-surface-container-high rounded-full h-2 overflow-hidden">
                                                <div className="bg-primary h-2 rounded-full" style={{ width: `${weightPercent}%` }}></div>
                                            </div>
                                        </div>
                                        <div>
                                            <div className="flex justify-between text-label-md font-label-md mb-2">
                                                <span className="text-on-surface-variant">Volume Load</span>
                                                <span className="text-on-surface">{activeTrip.volumeUsedCbm}/{activeTrip.volumeCapacityCbm} cbm</span>
                                            </div>
                                            <div className="w-full bg-surface-container-high rounded-full h-2 overflow-hidden">
                                                <div className="bg-on-tertiary-container h-2 rounded-full" style={{ width: `${volumePercent}%` }}></div>
                                            </div>
                                        </div>
                                    </div>
                                </>
                            ) : (
                                <div className="h-full min-h-[260px] flex flex-col items-center justify-center text-center">
                                    <span className="material-symbols-outlined text-[40px] text-on-surface-variant mb-3">route</span>
                                    <h3 className="text-headline-md font-headline-md text-on-surface">No database trips loaded</h3>
                                    <p className="text-body-md font-body-md text-on-surface-variant mt-2 max-w-md">{apiMessage}</p>
                                </div>
                            )}
                        </section>

                        <section className="xl:col-span-5 bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20">
                            <div className="flex justify-between items-center pb-4 mb-4 border-b border-outline-variant/30">
                                <h3 className="text-headline-md font-headline-md text-on-surface flex items-center gap-2">
                                    <span className="material-symbols-outlined text-primary">timeline</span>
                                    Trip Actions
                                </h3>
                            </div>
                            <div className="space-y-3">
                                <div className="flex gap-3 items-start bg-surface-container p-3 rounded border-l-4 border-primary">
                                    <span className="material-symbols-outlined text-primary text-[20px] mt-0.5">flag</span>
                                    <div>
                                        <h4 className="text-label-lg font-label-lg text-on-surface">Next Stop</h4>
                                        <p className="text-body-md font-body-md text-on-surface-variant mt-1">{activeTrip?.nextStop ?? 'No active database trip selected'}</p>
                                    </div>
                                </div>
                                <div className="grid grid-cols-2 gap-3">
                                    <button
                                        className="bg-primary hover:bg-primary-container disabled:bg-outline-variant disabled:text-on-surface-variant text-on-primary text-label-lg font-label-lg py-3 rounded-lg transition-colors flex items-center justify-center gap-2"
                                        disabled={!activeTrip || activeTrip.status !== 'Active'}
                                        onClick={() => activeTrip && void changeTripStatus(activeTrip, 'Completed')}
                                        type="button"
                                    >
                                        <span className="material-symbols-outlined">task_alt</span>
                                        Complete
                                    </button>
                                    <button
                                        className="bg-error-container hover:bg-error/20 disabled:bg-outline-variant disabled:text-on-surface-variant text-error text-label-lg font-label-lg py-3 rounded-lg transition-colors flex items-center justify-center gap-2 border border-error/20"
                                        disabled={!activeTrip || activeTrip.status !== 'Active'}
                                        onClick={() => activeTrip && void changeTripStatus(activeTrip, 'Breakdown')}
                                        type="button"
                                    >
                                        <span className="material-symbols-outlined">report</span>
                                        Breakdown
                                    </button>
                                </div>
                            </div>
                        </section>
                    </div>

                    <section className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20">
                        <div className="flex justify-between items-center mb-4 pb-2 border-b border-outline-variant/30">
                            <h3 className="text-headline-md font-headline-md text-on-surface flex items-center gap-2">
                                <span className="material-symbols-outlined text-primary">format_list_bulleted</span>
                                Trip List
                            </h3>
                            <span className="text-label-md font-label-md text-on-surface-variant">{trips.length} trips</span>
                        </div>
                        <div className="overflow-x-auto custom-scrollbar">
                            <table className="w-full min-w-[760px] text-left">
                                <thead>
                                    <tr className="text-label-md font-label-md text-on-surface-variant border-b border-outline-variant/30">
                                        <th className="py-3 pr-4">Trip</th>
                                        <th className="py-3 pr-4">Route</th>
                                        <th className="py-3 pr-4">Shipments</th>
                                        <th className="py-3 pr-4">Distance</th>
                                        <th className="py-3 pr-4">ETA</th>
                                        <th className="py-3 pr-4">Status</th>
                                        <th className="py-3 pr-4 text-right">Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {trips.map((trip) => (
                                        <tr className="border-b border-outline-variant/20 last:border-0" key={trip.id}>
                                            <td className="py-4 pr-4 text-label-lg font-label-lg text-on-surface">{trip.id}</td>
                                            <td className="py-4 pr-4">
                                                <div className="text-label-lg font-label-lg text-on-surface">{trip.routeCode}</div>
                                                <div className="text-label-md font-label-md text-on-surface-variant">{trip.originHub} to {trip.destinationHub}</div>
                                            </td>
                                            <td className="py-4 pr-4 text-body-md font-body-md text-on-surface">{trip.shipments}</td>
                                            <td className="py-4 pr-4 text-body-md font-body-md text-on-surface">{trip.distanceKm} km</td>
                                            <td className="py-4 pr-4 text-body-md font-body-md text-on-surface">{trip.eta}</td>
                                            <td className="py-4 pr-4">
                                                <span className={`inline-flex px-3 py-1 rounded-full border text-label-md font-label-md ${statusStyle[trip.status]}`}>
                                                    {trip.status}
                                                </span>
                                            </td>
                                            <td className="py-4 pr-4">
                                                <div className="flex justify-end gap-2">
                                                    <button
                                                        className="w-9 h-9 rounded-lg bg-surface-container hover:bg-surface-container-high disabled:opacity-40 text-primary border border-outline-variant/30 flex items-center justify-center transition-colors"
                                                        disabled={trip.status !== 'Active'}
                                                        onClick={() => editTrip(trip)}
                                                        title={trip.status === 'Active' ? 'Edit trip' : 'Only active trips can be edited'}
                                                        type="button"
                                                    >
                                                        <span className="material-symbols-outlined text-[20px]">edit</span>
                                                    </button>
                                                    <button
                                                        className="w-9 h-9 rounded-lg bg-error-container hover:bg-error/20 text-error border border-error/20 flex items-center justify-center transition-colors"
                                                        onClick={() => void deleteTrip(trip)}
                                                        title="Delete trip"
                                                        type="button"
                                                    >
                                                        <span className="material-symbols-outlined text-[20px]">delete</span>
                                                    </button>
                                                </div>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    </section>
                </main>
            </div>
        </div>
    );
}
