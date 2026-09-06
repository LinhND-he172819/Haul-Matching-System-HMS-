/**
 * Customer Feedback API client.
 * Base: /api/customer
 */
import { authFetch } from '../../utils/authFetch';

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

// ── Types ─────────────────────────────────────────────────────────────

export type FeedbackEvidence = {
  id: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  uploadedAt: string;
};

export type CustomerFeedback = {
  id: string;
  shipmentId: string;
  shipmentCode?: string;
  rating: number;
  comment?: string;
  createdAt: string;
  updatedAt: string;
  evidence: FeedbackEvidence[];
};

// ── API Functions ─────────────────────────────────────────────────────

/**
 * Create a feedback for a shipment.
 * POST /api/customer/shipments/{shipmentId}/feedback
 */
export async function createFeedback(
  shipmentId: string,
  rating: number,
  comment?: string
): Promise<{ id: string; shipmentId: string; rating: number; comment?: string; createdAt: string; message: string }> {
  const res = await authFetch(
    `${API_BASE_URL}/api/customer/shipments/${shipmentId}/feedback`,
    {
      method: 'POST',
      body: JSON.stringify({ rating, comment: comment || '' }),
    },
    { includeJson: true }
  );
  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể gửi đánh giá.' }));
    throw new Error(data.message || 'Không thể gửi đánh giá.');
  }
  return res.json();
}

/**
 * Upload evidence images for a feedback.
 * POST /api/customer/feedbacks/{feedbackId}/evidence  (multipart/form-data)
 */
export async function uploadFeedbackEvidence(
  feedbackId: string,
  files: File[]
): Promise<{ evidence: Array<{ id: string; fileName: string; storageKey: string }>; message: string }> {
  const formData = new FormData();
  for (const file of files) {
    formData.append('files', file);
  }

  const res = await authFetch(
    `${API_BASE_URL}/api/customer/feedbacks/${feedbackId}/evidence`,
    {
      method: 'POST',
      body: formData,
    }
  );
  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể tải ảnh lên.' }));
    throw new Error(data.message || 'Không thể tải ảnh lên.');
  }
  return res.json();
}

/**
 * Get my feedback for a specific shipment.
 * GET /api/customer/shipments/{shipmentId}/feedback
 */
export async function getMyFeedback(shipmentId: string): Promise<CustomerFeedback | null> {
  const res = await authFetch(
    `${API_BASE_URL}/api/customer/shipments/${shipmentId}/feedback`
  );
  if (res.status === 404) return null;
  if (!res.ok) {
    const data = await res.json().catch(() => ({ message: 'Không thể tải đánh giá.' }));
    throw new Error(data.message || 'Không thể tải đánh giá.');
  }
  return res.json();
}
