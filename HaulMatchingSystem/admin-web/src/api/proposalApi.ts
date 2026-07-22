// ── Types matching backend DTOs ──

export interface ProposalDto {
    proposalId: string;
    shipmentId: string;
    tripPostId: string;
    shipmentCode?: string;
    commodity?: string;
    weightKg: number;
    volumeCbm: number;
    receiverName?: string;
    receiverPhone?: string;
    deliveryAddress?: string;
    senderName: string;
    senderPhone: string;
    pickupAddress: string;
    pickupLatitude?: number;
    pickupLongitude?: number;
    pickupNote?: string;
    status: string;
    createdAt: string;
}

export interface DriverProposalsResponse {
    tripId: string;
    currentLoadWeight: number;
    currentLoadVolume: number;
    remainingWeightCapacity: number;
    remainingVolumeCapacity: number;
    proposals: ProposalDto[];
}

export interface CreateProposalRequest {
    shipmentId: string;
    senderName: string;
    senderPhone: string;
    pickupAddress: string;
    pickupLatitude?: number;
    pickupLongitude?: number;
    pickupNote?: string;
}

export interface CreateProposalResponse {
    proposalId: string;
    shipmentId: string;
    tripPostId: string;
    status: string;
    createdAt: string;
}

export interface RejectProposalRequest {
    reason: string;
}

export interface AcceptAllProposalsRequest {
    tripId: string;
}

// ── Helpers ──

const API_BASE =
    import.meta.env.VITE_API_BASE_URL ??
    import.meta.env.VITE_API_URL ??
    'http://localhost:5104';

function authHeaders(includeJson = false): HeadersInit {
    const token = localStorage.getItem('accessToken');
    return {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(includeJson ? { 'Content-Type': 'application/json' } : {})
    };
}

// ── Driver API ──

/** GET /api/driver/proposals/pending */
export async function fetchDriverPendingProposals(): Promise<DriverProposalsResponse | null> {
    const res = await fetch(`${API_BASE}/api/driver/proposals/pending`, {
        credentials: 'include',
        headers: authHeaders()
    });
    if (res.status === 404) return null;
    if (!res.ok) throw new Error(`Fetch proposals failed: ${res.status}`);
    return await res.json();
}

/** POST /api/driver/proposals/{proposalId}/accept */
export async function acceptProposal(proposalId: string): Promise<void> {
    const res = await fetch(`${API_BASE}/api/driver/proposals/${proposalId}/accept`, {
        method: 'POST',
        credentials: 'include',
        headers: authHeaders()
    });
    if (!res.ok) throw new Error(`Accept proposal failed: ${res.status}`);
}

/** POST /api/driver/proposals/{proposalId}/reject */
export async function rejectProposal(proposalId: string, reason: string): Promise<void> {
    const res = await fetch(`${API_BASE}/api/driver/proposals/${proposalId}/reject`, {
        method: 'POST',
        credentials: 'include',
        headers: authHeaders(true),
        body: JSON.stringify({ reason })
    });
    if (!res.ok) throw new Error(`Reject proposal failed: ${res.status}`);
}

/** POST /api/driver/proposals/accept-all */
export async function acceptAllProposals(tripId: string): Promise<void> {
    const res = await fetch(`${API_BASE}/api/driver/proposals/accept-all`, {
        method: 'POST',
        credentials: 'include',
        headers: authHeaders(true),
        body: JSON.stringify({ tripId })
    });
    if (!res.ok) throw new Error(`Accept all proposals failed: ${res.status}`);
}

// ── Customer API ──

/** POST /api/trip-posts/{tripPostId}/proposals */
export async function createProposal(
    tripPostId: string,
    request: CreateProposalRequest
): Promise<CreateProposalResponse> {
    const res = await fetch(`${API_BASE}/api/trip-posts/${tripPostId}/proposals`, {
        method: 'POST',
        credentials: 'include',
        headers: authHeaders(true),
        body: JSON.stringify(request)
    });
    if (!res.ok) throw new Error(`Create proposal failed: ${res.status}`);
    return await res.json();
}

/** DELETE /api/trip-posts/{tripPostId}/proposals/{proposalId} */
export async function cancelProposal(tripPostId: string, proposalId: string): Promise<void> {
    const res = await fetch(`${API_BASE}/api/trip-posts/${tripPostId}/proposals/${proposalId}`, {
        method: 'DELETE',
        credentials: 'include',
        headers: authHeaders()
    });
    if (!res.ok) throw new Error(`Cancel proposal failed: ${res.status}`);
}
