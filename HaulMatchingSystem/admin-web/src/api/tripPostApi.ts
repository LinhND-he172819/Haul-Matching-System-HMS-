import { authFetch } from '../utils/authFetch';

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5104';

/* ------------------------------------------------------------------ */
/*  Helpers                                                            */
/* ------------------------------------------------------------------ */

async function readApiError(response: Response, fallback: string): Promise<string> {
    const text = await response.text();
    if (!text) return fallback;
    try {
        const body = JSON.parse(text) as { title?: string; detail?: string; message?: string; errors?: Record<string, string[]> };
        const validationMessage = body.errors ? Object.values(body.errors).flat().join(' ') : '';
        return validationMessage || body.detail || body.message || body.title || fallback;
    } catch {
        return text;
    }
}

/* ------------------------------------------------------------------ */
/*  Types                                                              */
/* ------------------------------------------------------------------ */

export interface EligibleTrip {
    tripId: string;
    originHubId: string;
    originHubName: string;
    destinationHubId: string;
    destinationHubName: string;
    driverId: string;
    driverName: string;
    vehicleId: string;
    licensePlate: string;
    truckType: string;
    maxWeightKg: number;
    currentLoadWeightKg: number;
    remainingWeightKg: number;
    maxVolumeCbm: number;
    currentLoadVolumeCbm: number;
    remainingVolumeCbm: number;
    startedAt: string | null;
    status: string;
}

export type TripPostStatus = 'Open' | 'Closed' | 'Expired' | 'Cancelled';

export interface TripPostListItem {
    id: string;
    tripId: string;
    title: string;
    description: string | null;
    originHubName: string;
    destinationHubName: string;
    driverName: string;
    licensePlate: string;
    remainingWeightKg: number;
    remainingVolumeCbm: number;
    status: TripPostStatus;
    acceptUntil: string;
    publishedAt: string | null;
    createdByName: string;
    pickupMode: string; // "Hub" | "DirectPickup"
}

export interface TripPostDetail {
    id: string;
    tripId: string;
    title: string;
    description: string | null;
    originHubId: string;
    originHubName: string;
    destinationHubId: string;
    destinationHubName: string;
    driverId: string;
    driverName: string;
    vehicleId: string;
    licensePlate: string;
    truckType: string;
    maxWeightKg: number;
    maxVolumeCbm: number;
    currentLoadWeightKg: number;
    currentLoadVolumeCbm: number;
    remainingWeightKg: number;
    remainingVolumeCbm: number;
    tripStartedAt: string | null;
    tripStatus: string;
    status: string;
    acceptUntil: string;
    publishedAt: string | null;
    closedAt: string | null;
    createdById: string;
    createdByName: string;
    createdAt: string;
    updatedAt: string;
}

export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalItems: number;
    totalPages: number;
}

export interface TripPostKpi {
    totalPosts: number;
    openPosts: number;
    closedPosts: number;
    expiredPosts: number;
    cancelledPosts: number;
    eligibleTrips: number;
}

export interface CreateTripPostPayload {
    tripId: string;
    description?: string;
    acceptUntil: string;
    pickupMode?: string; // "Hub" | "DirectPickup"
}

export interface UpdateTripPostPayload {
    description?: string;
    acceptUntil?: string;
}

export interface CancelTripPostPayload {
    reason?: string;
}

export interface TripPostFilterParams {
    page?: number;
    pageSize?: number;
    keyword?: string;
    status?: string;
    hubId?: string;
    fromDate?: string;
    toDate?: string;
    sortBy?: string;
    sortDirection?: string;
}

export interface Hub {
    id: string;
    name: string;
}

/* ------------------------------------------------------------------ */
/*  Public API types (for Customer-facing marketplace)                 */
/* ------------------------------------------------------------------ */

export interface PublicTripPost {
    id: string;
    title: string;
    description: string | null;
    originHubName: string;
    destinationHubName: string;
    departureTime: string | null;
    acceptUntil: string;
    remainingWeightKg: number;
    remainingVolumeCbm: number;
    truckType: string;
    licensePlate: string;
    driverName: string;
    pickupMode: string; // "Hub" | "DirectPickup"
}

export interface PublicTripPostFilterParams {
    page?: number;
    pageSize?: number;
    keyword?: string;
    originHubId?: string;
    destinationHubId?: string;
    departureFrom?: string;
    departureTo?: string;
}

/* ------------------------------------------------------------------ */
/*  API calls                                                          */
/* ------------------------------------------------------------------ */

export async function fetchEligibleTrips(hubId?: string, keyword?: string): Promise<EligibleTrip[]> {
    const url = new URL(`${API_BASE}/api/trip-posts/eligible-trips`);
    if (hubId) url.searchParams.set('hubId', hubId);
    if (keyword) url.searchParams.set('keyword', keyword);

    const res = await authFetch(url);
    if (!res.ok) throw new Error(await readApiError(res, 'Không thể tải danh sách chuyến đủ điều kiện.'));
    return res.json();
}

export async function createTripPost(payload: CreateTripPostPayload): Promise<{ id: string; tripId: string; title: string; status: string; message: string }> {
    const res = await authFetch(`${API_BASE}/api/trip-posts`, {
        method: 'POST',
        body: JSON.stringify(payload),
    }, { includeJson: true });
    if (!res.ok) throw new Error(await readApiError(res, 'Đăng bài thất bại.'));
    return res.json();
}

export async function fetchTripPosts(params: TripPostFilterParams = {}): Promise<PagedResult<TripPostListItem>> {
    const url = new URL(`${API_BASE}/api/trip-posts`);
    if (params.page) url.searchParams.set('page', String(params.page));
    if (params.pageSize) url.searchParams.set('pageSize', String(params.pageSize));
    if (params.keyword) url.searchParams.set('keyword', params.keyword);
    if (params.status) url.searchParams.set('status', params.status);
    if (params.hubId) url.searchParams.set('hubId', params.hubId);
    if (params.fromDate) url.searchParams.set('fromDate', params.fromDate);
    if (params.toDate) url.searchParams.set('toDate', params.toDate);
    if (params.sortBy) url.searchParams.set('sortBy', params.sortBy);
    if (params.sortDirection) url.searchParams.set('sortDirection', params.sortDirection);

    const res = await authFetch(url);
    if (!res.ok) throw new Error(await readApiError(res, 'Không thể tải danh sách bài đăng.'));
    return res.json();
}

export async function fetchTripPostDetail(id: string): Promise<TripPostDetail> {
    const res = await authFetch(`${API_BASE}/api/trip-posts/${id}`);
    if (!res.ok) throw new Error(await readApiError(res, 'Không tìm thấy bài đăng.'));
    return res.json();
}

export async function updateTripPost(id: string, payload: UpdateTripPostPayload): Promise<TripPostDetail> {
    const res = await authFetch(`${API_BASE}/api/trip-posts/${id}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
    }, { includeJson: true });
    if (!res.ok) throw new Error(await readApiError(res, 'Cập nhật bài đăng thất bại.'));
    return res.json();
}

export async function closeTripPost(id: string): Promise<{ message: string }> {
    const res = await authFetch(`${API_BASE}/api/trip-posts/${id}/close`, {
        method: 'PUT',
    });
    if (!res.ok) throw new Error(await readApiError(res, 'Đóng bài đăng thất bại.'));
    return res.json();
}

export async function cancelTripPost(id: string, payload?: CancelTripPostPayload): Promise<{ message: string }> {
    const res = await authFetch(`${API_BASE}/api/trip-posts/${id}/cancel`, {
        method: 'PUT',
        body: JSON.stringify(payload ?? {}),
    }, { includeJson: true });
    if (!res.ok) throw new Error(await readApiError(res, 'Hủy bài đăng thất bại.'));
    return res.json();
}

export async function fetchTripPostKpi(): Promise<TripPostKpi> {
    const res = await authFetch(`${API_BASE}/api/trip-posts/kpi`);
    if (!res.ok) throw new Error(await readApiError(res, 'Không thể tải KPI.'));
    return res.json();
}

export async function fetchHubs(): Promise<Hub[]> {
    const res = await authFetch(`${API_BASE}/api/hubs`);
    if (!res.ok) throw new Error(await readApiError(res, 'Không thể tải danh sách Hub.'));
    return res.json();
}

/* ------------------------------------------------------------------ */
/*  Public API (Customer-facing marketplace)                           */
/* ------------------------------------------------------------------ */

export async function fetchPublicTripPosts(params: PublicTripPostFilterParams = {}): Promise<PagedResult<PublicTripPost>> {
    const url = new URL(`${API_BASE}/api/trip-posts/public`);
    if (params.page) url.searchParams.set('page', String(params.page));
    if (params.pageSize) url.searchParams.set('pageSize', String(params.pageSize));
    if (params.keyword) url.searchParams.set('keyword', params.keyword);
    if (params.originHubId) url.searchParams.set('originHubId', params.originHubId);
    if (params.destinationHubId) url.searchParams.set('destinationHubId', params.destinationHubId);
    if (params.departureFrom) url.searchParams.set('departureFrom', params.departureFrom);
    if (params.departureTo) url.searchParams.set('departureTo', params.departureTo);

    const res = await fetch(url.toString());
    if (!res.ok) throw new Error(await readApiError(res, 'Không thể tải danh sách chuyến xe.'));
    return res.json();
}
