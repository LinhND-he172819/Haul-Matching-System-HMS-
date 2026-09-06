/**
 * Admin/Staff Feedback Management API client.
 * Base: /api/staff/feedbacks
 * Admin sees all feedbacks; Warehouse_Staff sees only their hub's feedbacks (enforced server-side).
 */
import { authFetch } from '../utils/authFetch';

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

// ── Types ─────────────────────────────────────────────────────────────

export type StaffFeedbackListItem = {
  id: string;
  shipmentId: string;
  customerId: string;
  rating: number;
  comment?: string;
  createdAt: string;
  shipmentCode?: string;
  customerName?: string;
  evidenceCount: number;
};

export type StaffFeedbackPagedResult = {
  items: StaffFeedbackListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

export type StaffFeedbackEvidence = {
  id: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  uploadedAt: string;
};

export type StaffFeedbackDetail = {
  id: string;
  shipmentId: string;
  customerId: string;
  rating: number;
  comment?: string;
  createdAt: string;
  updatedAt: string;
  shipmentCode?: string;
  shipmentStatus?: string;
  customerName?: string;
  customerEmail?: string;
  evidence: StaffFeedbackEvidence[];
};

// ── API Functions ─────────────────────────────────────────────────────

/**
 * List feedbacks with pagination and filtering.
 * GET /api/staff/feedbacks
 */
export async function listFeedbacks(params: {
  page?: number;
  pageSize?: number;
  rating?: number;
  search?: string;
}): Promise<StaffFeedbackPagedResult> {
  const searchParams = new URLSearchParams();
  if (params.page) searchParams.set('page', params.page.toString());
  if (params.pageSize) searchParams.set('pageSize', params.pageSize.toString());
  if (params.rating) searchParams.set('rating', params.rating.toString());
  if (params.search) searchParams.set('search', params.search);

  const res = await authFetch(
    `${API_BASE_URL}/api/staff/feedbacks?${searchParams.toString()}`
  );
  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể tải danh sách phản hồi.' }));
    throw new Error(data.message || 'Không thể tải danh sách phản hồi.');
  }
  return res.json();
}

/**
 * Get feedback detail.
 * GET /api/staff/feedbacks/{feedbackId}
 */
export async function getFeedbackDetail(feedbackId: string): Promise<StaffFeedbackDetail> {
  const res = await authFetch(
    `${API_BASE_URL}/api/staff/feedbacks/${feedbackId}`
  );
  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể tải chi tiết phản hồi.' }));
    throw new Error(data.message || 'Không thể tải chi tiết phản hồi.');
  }
  return res.json();
}

/**
 * Get evidence download URL.
 * GET /api/staff/feedbacks/{feedbackId}/evidence/{evidenceId}
 * Use with authFetch for blob loading.
 */
export function getEvidenceDownloadUrl(feedbackId: string, evidenceId: string): string {
  return `${API_BASE_URL}/api/staff/feedbacks/${feedbackId}/evidence/${evidenceId}`;
}
