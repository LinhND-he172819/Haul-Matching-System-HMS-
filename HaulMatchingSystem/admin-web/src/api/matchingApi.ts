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

const API_BASE =
    import.meta.env.VITE_API_BASE_URL ??
    import.meta.env.VITE_API_URL ??
    'http://localhost:5104';

function authHeaders(includeJson = false): HeadersInit {
    const token = localStorage.getItem('accessToken');
    if (!token) throw new Error('Bạn cần đăng nhập bằng tài khoản tài xế.');

    return {
        Authorization: `Bearer ${token}`,
        ...(includeJson ? { 'Content-Type': 'application/json' } : {})
    };
}

export async function fetchMatchingSuggestions(): Promise<MatchingSuggestionsResponse> {
    const res = await fetch(`${API_BASE}/api/drivers/me/matching-suggestions`, {
        credentials: 'include',
        headers: authHeaders()
    });
    if (!res.ok) throw new Error(`Fetch failed: ${res.status}`);
    return await res.json();
}

export async function postAcceptAll(): Promise<void> {
    const res = await fetch(`${API_BASE}/api/drivers/me/matching-suggestions/accept-all`, {
        method: 'POST',
        credentials: 'include',
        headers: authHeaders()
    });
    if (!res.ok) throw new Error(`Accept all failed: ${res.status}`);
}

export async function postRejectAll(): Promise<void> {
    const res = await fetch(`${API_BASE}/api/drivers/me/matching-suggestions/reject-all`, {
        method: 'POST',
        credentials: 'include',
        headers: authHeaders()
    });
    if (!res.ok) throw new Error(`Reject all failed: ${res.status}`);
}

export async function postAcceptSelected(shipmentIds: string[]): Promise<void> {
    const res = await fetch(`${API_BASE}/api/drivers/me/matching-suggestions/accept-selected`, {
        method: 'POST',
        credentials: 'include',
        headers: authHeaders(true),
        body: JSON.stringify({ shipmentIds })
    });
    if (!res.ok) throw new Error(`Accept selected failed: ${res.status}`);
}

export async function postRejectSelected(shipmentIds: string[]): Promise<void> {
    const res = await fetch(`${API_BASE}/api/drivers/me/matching-suggestions/reject-selected`, {
        method: 'POST',
        credentials: 'include',
        headers: authHeaders(true),
        body: JSON.stringify({ shipmentIds })
    });
    if (!res.ok) throw new Error(`Reject selected failed: ${res.status}`);
}
