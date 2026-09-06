export interface ShipmentSuggestionDto {
    shipmentId: string;
    receiverName?: string;
    receiverPhone?: string;
    destinationAddress?: string;
    weightKg: number;
    volumeCbm: number;
    deliverySequence: number;
    specialHandlingNote?: string;
}

export interface MatchingSuggestionsResponse {
    tripId: string;
    currentLoadWeight: number;
    currentLoadVolume: number;
    remainingWeightCapacity: number;
    remainingVolumeCapacity: number;
    shipments: ShipmentSuggestionDto[];
}

export interface TripResponse {
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
    status: string;
    createdAt: string;
    updatedAt: string;
}

export interface HubResponse {
    id: string;
    name: string;
    address: string;
    latitude: number;
    longitude: number;
    createdAt: string;
    updatedAt: string;
}

import { authFetch } from '../utils/authFetch';

const API_BASE =
    import.meta.env.VITE_API_BASE_URL ??
    import.meta.env.VITE_API_URL ??
    'http://localhost:5104';

export async function fetchMatchingSuggestions(): Promise<MatchingSuggestionsResponse | null> {
    const res = await authFetch(`${API_BASE}/api/drivers/me/matching-suggestions`);

    if (res.status === 404) {
        return null;
    }

    if (!res.ok) throw new Error(`Fetch failed: ${res.status}`);
    return await res.json();
}

export async function postAcceptAll(): Promise<void> {
    const res = await authFetch(`${API_BASE}/api/drivers/me/matching-suggestions/accept-all`, {
        method: 'POST',
    });
    if (!res.ok) throw new Error(`Accept all failed: ${res.status}`);
}

export async function postRejectAll(): Promise<void> {
    const res = await authFetch(`${API_BASE}/api/drivers/me/matching-suggestions/reject-all`, {
        method: 'POST',
    });
    if (!res.ok) throw new Error(`Reject all failed: ${res.status}`);
}

export async function postAcceptSelected(shipmentIds: string[]): Promise<void> {
    const res = await authFetch(`${API_BASE}/api/drivers/me/matching-suggestions/accept-selected`, {
        method: 'POST',
        body: JSON.stringify({ shipmentIds }),
    }, { includeJson: true });
    if (!res.ok) throw new Error(`Accept selected failed: ${res.status}`);
}

export async function postRejectSelected(shipmentIds: string[]): Promise<void> {
    const res = await authFetch(`${API_BASE}/api/drivers/me/matching-suggestions/reject-selected`, {
        method: 'POST',
        body: JSON.stringify({ shipmentIds }),
    }, { includeJson: true });
    if (!res.ok) throw new Error(`Reject selected failed: ${res.status}`);
}

export async function fetchTripById(tripId: string): Promise<TripResponse> {
    const res = await authFetch(`${API_BASE}/api/trips/${tripId}`);

    if (!res.ok) throw new Error(`Trip fetch failed: ${res.status}`);

    return await res.json();
}

export async function fetchHubById(hubId: string): Promise<HubResponse> {
    const res = await authFetch(`${API_BASE}/api/hubs/${hubId}`);

    if (!res.ok) throw new Error(`Hub fetch failed: ${res.status}`);

    return await res.json();
}
