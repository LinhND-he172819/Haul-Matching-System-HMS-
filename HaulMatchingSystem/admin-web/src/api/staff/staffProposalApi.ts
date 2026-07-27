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
}

export interface ShipmentInfoDto {
  id: string;
  shipmentCode: string;
  commodity: string;
  weightKg: number;
  volumeCbm: number;
  receiver: string;
  deliveryAddress: string;
  specialHandlingNote?: string;
  status: string;
}

export interface TripInfoDto {
  tripPostId: string;
  tripId: string;
  tripCode: string;
  origin: string;
  destination: string;
  departureTime: string;
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
  email: string;
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
  code: string;
  status: string;
  createdAt: string;
  shipment: ShipmentInfoDto;
  trip: TripInfoDto;
  tripCapacity: TripCapacityInfoDto;
  customer: CustomerInfoDto;
  quotations: QuotationSummaryDto[];
  audit: ProposalAuditEntry[];
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
  page?: number;
  pageSize?: number;
}): Promise<PagedResult<StaffProposalSummary>> {
  const url = new URL(`${API_BASE}/api/staff/proposals`);
  if (params.status) url.searchParams.set('status', params.status);
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
  return res.json();
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
