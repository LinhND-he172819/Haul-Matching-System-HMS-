import { authFetch } from '../../utils/authFetch';

const API_BASE =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

/* ─── Types ─────────────────────────────────────────────────────── */

export interface StaffProposalSummary {
  proposalId: string;
  code: string;
  status: string;
  proposalSource?: string;
  shipmentId: string;
  shipmentCode: string;
  commodity: string;
  weightKg: number;
  volumeCbm: number;
  senderName: string;
  senderPhone: string;
  pickupAddress: string;
  receiverName: string;
  receiverPhone: string;
  deliveryAddress: string;
  tripPostId: string;
  tripCode: string;
  origin: string;
  destination: string;
  departureTime: string;
  maxWeight: number;
  maxVolume: number;
  currentWeight: number;
  currentVolume: number;
  remainingWeight: number;
  remainingVolume: number;
  customerId: string;
  customerName: string;
  createdAt: string;
  quotationId?: string;
  quotationStatus?: string;
  // Driver info (for Driver proposals)
  driverId?: string;
  driverName?: string;
  driverPhone?: string;
  vehiclePlate?: string;
}

export interface ShipmentInfoDto {
  id: string;
  shipmentCode: string;
  commodity?: string | null;
  weightKg: number;
  volumeCbm: number;
  receiver?: string;
  receiverName?: string;
  receiverPhone?: string;
  deliveryAddress: string;
  specialHandlingNote?: string;
  status: string;
}

export interface TripInfoDto {
  tripPostId: string;
  tripId: string;
  tripCode?: string | null;
  title?: string;
  origin?: string | null;
  destination?: string | null;
  departureTime?: string | null;
  acceptUntil?: string;
  pickupMode: string;
  maxWeight: number;
  maxVolume: number;
}

export interface TripCapacityInfoDto {
  currentWeight: number;
  currentVolume: number;
  maxWeight: number;
  maxVolume: number;
  remainingWeight: number;
  remainingVolume: number;
}

export interface CustomerInfoDto {
  id: string;
  fullName: string;
  phone: string;
  email?: string;
}

export interface QuotationSummaryDto {
  id: string;
  quotationCode: string;
  shippingFee: number;
  depositAmount: number;
  remainingAmount: number;
  currency: string;
  status: string;
  sentAt?: string;
  expiresAt?: string;
  acceptedAt?: string;
  createdAt: string;
}

export interface ProposalAuditEntry {
  action: string;
  details: string;
  performedBy: string;
  performedByName: string;
  occurredAt: string;
}

export interface StaffProposalDetail {
  proposalId: string;
  proposalCode?: string | null;
  code?: string; // frontend alias fallback
  status: string;
  proposalSource?: string;
  createdAt: string;
  reviewedAt?: string | null;
  approvedAt?: string | null;
  rejectedAt?: string | null;
  rejectReason?: string | null;
  shipment: ShipmentInfoDto;
  senderName?: string;
  senderPhone?: string;
  pickupAddress?: string;
  pickupLatitude?: number | null;
  pickupLongitude?: number | null;
  pickupNote?: string;
  trip: TripInfoDto;
  tripCapacity: TripCapacityInfoDto;
  customer: CustomerInfoDto;
  /** API field name */
  quotationHistory?: QuotationSummaryDto[];
  currentQuotation?: QuotationSummaryDto | null;
  /** Frontend alias — falls back to quotationHistory */
  quotations?: QuotationSummaryDto[];
  /** API field name */
  auditTimeline?: ProposalAuditEntry[];
  /** Frontend alias — falls back to auditTimeline */
  audit?: ProposalAuditEntry[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/* ─── API Functions ─────────────────────────────────────────────── */

export async function getStaffProposals(params: {
  status?: string;
  proposalSource?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedResult<StaffProposalSummary>> {
  const url = new URL(`${API_BASE}/api/staff/proposals`);
  if (params.status) url.searchParams.set('status', params.status);
  if (params.proposalSource) url.searchParams.set('proposalSource', params.proposalSource);
  if (params.search) url.searchParams.set('search', params.search);
  if (params.page) url.searchParams.set('page', params.page.toString());
  if (params.pageSize) url.searchParams.set('pageSize', params.pageSize.toString());

  const res = await authFetch(url.toString());
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tải danh sách đề xuất (${res.status})`);
  }
  return res.json();
}

export async function getStaffProposalDetail(proposalId: string): Promise<StaffProposalDetail> {
  const res = await authFetch(`${API_BASE}/api/staff/proposals/${proposalId}`);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi tải chi tiết đề xuất (${res.status})`);
  }
  const raw = await res.json();
  // Normalize: API uses quotationHistory/auditTimeline, frontend aliases quotations/audit
  return {
    ...raw,
    code: raw.proposalCode || raw.code || '',
    quotations: raw.quotations ?? raw.quotationHistory ?? [],
    audit: raw.audit ?? raw.auditTimeline ?? [],
  } as StaffProposalDetail;
}

export async function approveProposal(proposalId: string): Promise<void> {
  const res = await authFetch(`${API_BASE}/api/staff/proposals/${proposalId}/approve`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({}),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi duyệt đề xuất (${res.status})`);
  }
}

export async function rejectProposal(proposalId: string, reason: string): Promise<void> {
  const res = await authFetch(`${API_BASE}/api/staff/proposals/${proposalId}/reject`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason }),
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Lỗi từ chối đề xuất (${res.status})`);
  }
}
