import { authFetch } from '../utils/authFetch';

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

// ── Driver API ──

/** GET /api/driver/proposals/pending */
export async function fetchDriverPendingProposals(): Promise<DriverProposalsResponse | null> {
    const res = await authFetch(`${API_BASE}/api/driver/proposals/pending`);
    if (res.status === 404) return null;
    if (!res.ok) {
        let msg = `Fetch proposals failed: ${res.status}`;
        try { const body = await res.json(); msg = body.detail || body.message || msg; } catch {}
        throw new Error(msg);
    }
    return await res.json();
}

/** POST /api/driver/proposals/{proposalId}/accept */
export async function acceptProposal(proposalId: string): Promise<void> {
    const res = await authFetch(`${API_BASE}/api/driver/proposals/${proposalId}/accept`, {
        method: 'POST'
    });
    if (!res.ok) {
        let msg = `Accept proposal failed: ${res.status}`;
        try { const body = await res.json(); msg = body.detail || body.message || msg; } catch {}
        throw new Error(msg);
    }
}

/** POST /api/driver/proposals/{proposalId}/reject */
export async function rejectProposal(proposalId: string, reason: string): Promise<void> {
    const res = await authFetch(`${API_BASE}/api/driver/proposals/${proposalId}/reject`, {
        method: 'POST',
        body: JSON.stringify({ reason })
    }, { includeJson: true });
    if (!res.ok) {
        let msg = `Reject proposal failed: ${res.status}`;
        try { const body = await res.json(); msg = body.detail || body.message || msg; } catch {}
        throw new Error(msg);
    }
}

/** POST /api/driver/proposals/accept-all */
export async function acceptAllProposals(tripId: string): Promise<void> {
    const res = await authFetch(`${API_BASE}/api/driver/proposals/accept-all`, {
        method: 'POST',
        body: JSON.stringify({ tripId })
    }, { includeJson: true });
    if (!res.ok) {
        let msg = `Accept all proposals failed: ${res.status}`;
        try { const body = await res.json(); msg = body.detail || body.message || msg; } catch {}
        throw new Error(msg);
    }
}

// ── Customer API ──

/** POST /api/trip-posts/{tripPostId}/proposals */
export async function createProposal(
    tripPostId: string,
    request: CreateProposalRequest
): Promise<CreateProposalResponse> {
    const res = await authFetch(`${API_BASE}/api/trip-posts/${tripPostId}/proposals`, {
        method: 'POST',
        body: JSON.stringify(request)
    }, { includeJson: true });
    if (!res.ok) {
        let msg = `Create proposal failed: ${res.status}`;
        try { const body = await res.json(); msg = body.detail || body.message || msg; } catch {}
        throw new Error(msg);
    }
    return await res.json();
}

/** DELETE /api/trip-posts/{tripPostId}/proposals/{proposalId} */
export async function cancelProposal(tripPostId: string, proposalId: string): Promise<void> {
    const res = await authFetch(`${API_BASE}/api/trip-posts/${tripPostId}/proposals/${proposalId}`, {
        method: 'DELETE'
    });
    if (!res.ok) {
        let msg = `Cancel proposal failed: ${res.status}`;
        try { const body = await res.json(); msg = body.detail || body.message || msg; } catch {}
        throw new Error(msg);
    }
}
