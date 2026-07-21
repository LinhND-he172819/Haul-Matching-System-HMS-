export type TripStatus = 'Active' | 'Completed' | 'Breakdown';

export interface Trip {
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

export interface TripPayload {
    driverId: string;
    vehicleId: string;
    originHubId: string;
    destHubId: string;
    routeLineString?: string | null;
    currentLoadWeightKg: number;
    currentLoadVolumeCbm: number;
}

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5104';

async function readApiError(response: Response, fallback: string) {
    const text = await response.text();
    if (!text) return fallback;

    try {
        const body = JSON.parse(text) as {
            title?: string;
            detail?: string;
            message?: string;
            errors?: Record<string, string[]>;
        };
        return (body.errors ? Object.values(body.errors).flat().join(' ') : '') ||
            body.detail || body.message || body.title || fallback;
    } catch {
        return text;
    }
}

export async function fetchTrips(driverId = '', status = ''): Promise<Trip[]> {
    const url = new URL(`${API_BASE_URL}/api/trips`);
    if (driverId) url.searchParams.set('driverId', driverId);
    if (status) url.searchParams.set('status', status);

    const response = await fetch(url);
    if (!response.ok) throw new Error(await readApiError(response, `Không thể tải chuyến đi (${response.status}).`));
    return response.json();
}

export async function createTrip(payload: TripPayload): Promise<Trip> {
    const response = await fetch(`${API_BASE_URL}/api/trips`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });
    if (!response.ok) throw new Error(await readApiError(response, `Không thể tạo chuyến đi (${response.status}).`));
    return response.json();
}

export async function updateTrip(id: string, payload: TripPayload): Promise<Trip> {
    const response = await fetch(`${API_BASE_URL}/api/trips/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });
    if (!response.ok) throw new Error(await readApiError(response, `Không thể cập nhật chuyến đi (${response.status}).`));
    return response.json();
}

export async function changeTripStatus(id: string, status: Exclude<TripStatus, 'Active'>): Promise<Trip> {
    const response = await fetch(`${API_BASE_URL}/api/trips/${id}/status`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status, occurredAt: new Date().toISOString() })
    });
    if (!response.ok) throw new Error(await readApiError(response, `Không thể đổi trạng thái (${response.status}).`));
    return response.json();
}

export async function deleteTrip(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/api/trips/${id}`, { method: 'DELETE' });
    if (!response.ok) throw new Error(await readApiError(response, `Không thể xóa chuyến đi (${response.status}).`));
}
