export type TripStatus = 'Active' | 'Scheduled' | 'Ready' | 'InProgress' | 'Completed' | 'Breakdown' | 'Cancelled';

export interface Trip {
    id: string;
    driverId: string;
    vehicleId: string;
    originHubId: string;
    destHubId: string;
    routeLineString: string;
    currentLoadWeightKg: number;
    currentLoadVolumeCbm: number;
    scheduledDepartureAt: string;
    startedAt: string | null;
    finishedAt: string | null;
    version: number;
    status: TripStatus;
    createdAt: string;
    updatedAt: string;
}

export interface TripShipment {
    shipmentId: string;
    qrCode: string;
    cargoType: string | null;
    weightKg: number;
    volumeCbm: number;
    status: string;
    senderName: string | null;
    senderPhone: string | null;
    pickupAddress: string | null;
    receiverName: string | null;
    receiverPhone: string | null;
    deliveryAddress: string | null;
}

export interface UnlinkTripShipmentResult {
    trip: Trip;
    unlinkedShipmentQrCode: string;
    alsoUnlinkedOnCancel: string[];
}

export interface TripPayload {
    driverId: string;
    vehicleId: string;
    originHubId: string;
    destHubId: string;
    routeLineString?: string | null;
    currentLoadWeightKg: number;
    currentLoadVolumeCbm: number;
    scheduledDepartureAt?: string | null;
    warehouseShipmentIds?: string[];
}

import { authFetch } from '../utils/authFetch';

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

    const response = await authFetch(url);
    if (!response.ok) throw new Error(await readApiError(response, `Không thể tải chuyến đi (${response.status}).`));
    return response.json();
}

export async function createTrip(payload: TripPayload): Promise<Trip> {
    const response = await authFetch(`${API_BASE_URL}/api/trips`, {
        method: 'POST',
        body: JSON.stringify(payload),
    }, { includeJson: true });
    if (!response.ok) throw new Error(await readApiError(response, `Không thể tạo chuyến đi (${response.status}).`));
    return response.json();
}

export async function updateTrip(id: string, payload: TripPayload): Promise<Trip> {
    const response = await authFetch(`${API_BASE_URL}/api/trips/${id}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
    }, { includeJson: true });
    if (!response.ok) throw new Error(await readApiError(response, `Không thể cập nhật chuyến đi (${response.status}).`));
    return response.json();
}

export async function changeTripStatus(id: string, status: TripStatus): Promise<Trip> {
    const response = await authFetch(`${API_BASE_URL}/api/trips/${id}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ status, occurredAt: new Date().toISOString() }),
    }, { includeJson: true });
    if (!response.ok) throw new Error(await readApiError(response, `Không thể đổi trạng thái (${response.status}).`));
    return response.json();
}

export async function deleteTrip(id: string): Promise<void> {
    const response = await authFetch(`${API_BASE_URL}/api/trips/${id}`, { method: 'DELETE' });
    if (!response.ok) throw new Error(await readApiError(response, `Không thể xóa chuyến đi (${response.status}).`));
}

export async function fetchTripShipments(id: string): Promise<TripShipment[]> {
    const response = await authFetch(`${API_BASE_URL}/api/trips/${id}/shipments`);
    if (!response.ok) {
        if (response.status === 404) return [];
        throw new Error(await readApiError(response, `Không thể tải đơn hàng của chuyến (${response.status}).`));
    }
    return response.json();
}

export async function unlinkTripShipment(tripId: string, shipmentId: string): Promise<UnlinkTripShipmentResult> {
    const response = await authFetch(`${API_BASE_URL}/api/trips/${tripId}/shipments/${shipmentId}`, {
        method: 'DELETE',
    });
    if (!response.ok) {
        const errorText = await readApiError(response, `Không thể gỡ đơn hàng (${response.status}).`);
        const err = new Error(errorText);
        (err as Error & { status?: number }).status = response.status;
        throw err;
    }
    return response.json();
}
