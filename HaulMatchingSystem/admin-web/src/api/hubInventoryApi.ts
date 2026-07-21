import { authFetch } from '../utils/authFetch';

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5104';

/* ------------------------------------------------------------------ */
/*  Helpers                                                            */
/* ------------------------------------------------------------------ */

async function readApiError(response: Response, fallback: string): Promise<string> {
    const text = await response.text();
    if (!text) return fallback;
    try {
        const body = JSON.parse(text) as { title?: string; detail?: string; errors?: Record<string, string[]> };
        const validationMessage = body.errors ? Object.values(body.errors).flat().join(' ') : '';
        return validationMessage || body.detail || body.title || fallback;
    } catch {
        return text;
    }
}

/* ------------------------------------------------------------------ */
/*  Types                                                              */
/* ------------------------------------------------------------------ */

export type ShipmentStatus =
    | 'Draft'
    | 'In_Warehouse'
    | 'Matched'
    | 'Assigned'
    | 'Out_For_Delivery'
    | 'Delivered'
    | 'Returned';

export interface HubInventoryShipment {
    id: string;
    qrCode: string;
    cargoType: string;
    receiverName: string;
    receiverPhone: string;
    destAddress: string;
    status: string;
    currentHubId: string | null;
    currentHubName: string | null;
    weightKg: number;
    volumeCbm: number;
    cod: number | null;
    shippingFee: number | null;
    specialHandlingNote: string | null;
    createdAt: string;
    intakeConfirmedAt: string | null;
    daysInWarehouse: number;
}

export interface PagedResult<T> {
    items: T[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
}

export interface HubInventoryDashboard {
    totalShipment: number;
    inWarehouse: number;
    matched: number;
    readyForDispatch: number;
    expired: number;
    totalWeight: number;
    totalVolume: number;
}

export interface HubInventoryDetail {
    id: string;
    qrCode: string;
    status: string;
    currentHubId: string | null;
    currentHubName: string | null;
    createdAt: string;
    intakeConfirmedAt: string | null;
    customerName: string | null;
    customerPhone: string | null;
    receiverName: string;
    receiverPhone: string;
    destAddress: string;
    cargoType: string;
    weightKg: number;
    volumeCbm: number;
    cod: number | null;
    shippingFee: number | null;
    specialHandlingNote: string | null;
    intakeStaffName: string | null;
    daysInWarehouse: number;
    timeline: TimelineEntry[];
}

export interface TimelineEntry {
    label: string;
    timestamp: string | null;
    isCompleted: boolean;
    isCurrent: boolean;
}

export interface UpdateShipmentPayload {
    receiverName?: string;
    receiverPhone?: string;
    destAddress?: string;
    cargoType?: string;
    weightKg?: number;
    volumeCbm?: number;
    specialHandlingNote?: string;
}

export interface InventoryQueryParams {
    page?: number;
    pageSize?: number;
    keyword?: string;
    status?: string;
    cargoType?: string;
    hubId?: string;
    sort?: string;
    fromDate?: string;
    toDate?: string;
}

/* ------------------------------------------------------------------ */
/*  API calls                                                          */
/* ------------------------------------------------------------------ */

export async function fetchInventory(params: InventoryQueryParams = {}): Promise<PagedResult<HubInventoryShipment>> {
    const url = new URL(`${API_BASE}/api/hub-inventory`);
    if (params.page) url.searchParams.set('page', String(params.page));
    if (params.pageSize) url.searchParams.set('pageSize', String(params.pageSize));
    if (params.keyword) url.searchParams.set('keyword', params.keyword);
    if (params.status) url.searchParams.set('status', params.status);
    if (params.cargoType) url.searchParams.set('cargoType', params.cargoType);
    if (params.hubId) url.searchParams.set('hubId', params.hubId);
    if (params.sort) url.searchParams.set('sort', params.sort);
    if (params.fromDate) url.searchParams.set('fromDate', params.fromDate);
    if (params.toDate) url.searchParams.set('toDate', params.toDate);

    const res = await authFetch(url);
    if (!res.ok) throw new Error(await readApiError(res, `Lỗi tải inventory (${res.status})`));
    return res.json();
}

export async function fetchDashboardSummary(hubId?: string): Promise<HubInventoryDashboard> {
    const url = new URL(`${API_BASE}/api/hub-inventory/dashboard`);
    if (hubId) url.searchParams.set('hubId', hubId);

    const res = await authFetch(url);
    if (!res.ok) throw new Error(await readApiError(res, `Lỗi tải dashboard (${res.status})`));
    return res.json();
}

export async function fetchShipmentDetail(shipmentId: string): Promise<HubInventoryDetail> {
    const res = await authFetch(`${API_BASE}/api/hub-inventory/${shipmentId}`);
    if (!res.ok) throw new Error(await readApiError(res, 'Không tìm thấy kiện hàng.'));
    return res.json();
}

export async function updateShipment(shipmentId: string, payload: UpdateShipmentPayload): Promise<void> {
    const res = await authFetch(`${API_BASE}/api/hub-inventory/${shipmentId}`, {
        method: 'PUT',
        body: JSON.stringify(payload)
    }, { includeJson: true });
    if (!res.ok) throw new Error(await readApiError(res, 'Cập nhật thất bại.'));
}

export async function fetchHubsForSelector(): Promise<{ id: string; name: string }[]> {
    const res = await authFetch(`${API_BASE}/api/hubs`);
    if (!res.ok) throw new Error(await readApiError(res, 'Lỗi tải danh sách Hub.'));
    return res.json();
}
