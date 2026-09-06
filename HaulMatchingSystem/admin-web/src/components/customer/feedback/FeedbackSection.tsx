/**
 * FeedbackSection — Displays existing feedback or "give feedback" button in ShipmentDetailPage.
 * Shows star rating, comment, evidence thumbnails. Opens FeedbackModal when needed.
 */
import { useState, useEffect, useRef } from 'react';
import { getMyFeedback, type CustomerFeedback } from '../../../api/customer/customerFeedbackApi';
import { authFetch } from '../../../utils/authFetch';
import FeedbackModal from './FeedbackModal';

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  'http://localhost:5104';

/* ─── Props ─────────────────────────────────────────────────────── */

type Props = {
  shipmentId: string;
  shipmentCode?: string;
  status?: string;
  canGiveFeedback?: boolean;
  onFeedbackChanged?: () => void;
};

/* ─── Star Display ──────────────────────────────────────────────── */

function StarDisplay({ rating, size = 20 }: { rating: number; size?: number }) {
  return (
    <div className="flex items-center gap-0.5">
      {[1, 2, 3, 4, 5].map((star) => (
        <span
          key={star}
          className={`material-symbols-outlined ${star <= rating ? 'text-amber-400' : 'text-on-surface-variant/25'}`}
          style={{
            fontVariationSettings: star <= rating ? "'FILL' 1" : "'FILL' 0",
            fontSize: `${size}px`,
          }}
        >
          star
        </span>
      ))}
    </div>
  );
}

/* ─── Main Component ────────────────────────────────────────────── */

export default function FeedbackSection({ shipmentId, shipmentCode, status: _status, canGiveFeedback, onFeedbackChanged: _onFeedbackChanged }: Props) {
  const [feedback, setFeedback] = useState<CustomerFeedback | null>(null);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [imageUrls, setImageUrls] = useState<Record<string, string>>({});
  const [lightboxUrl, setLightboxUrl] = useState<string | null>(null);
  const blobUrlsRef = useRef<string[]>([]);

  // Load feedback
  const loadFeedback = async () => {
    try {
      const data = await getMyFeedback(shipmentId);
      setFeedback(data);
    } catch {
      // Silently fail
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadFeedback();
  }, [shipmentId]);

  // Load evidence images as blob URLs (auth-aware)
  useEffect(() => {
    if (!feedback?.evidence?.length) return;

    // Cleanup old blob URLs
    blobUrlsRef.current.forEach((url) => URL.revokeObjectURL(url));
    blobUrlsRef.current = [];

    const load = async () => {
      const urls: Record<string, string> = {};
      for (const ev of feedback.evidence) {
        try {
          const res = await authFetch(
            `${API_BASE_URL}/api/customer/feedbacks/${feedback.id}/evidence/${ev.id}`
          );
          if (res.ok) {
            const blob = await res.blob();
            const blobUrl = URL.createObjectURL(blob);
            urls[ev.id] = blobUrl;
            blobUrlsRef.current.push(blobUrl);
          }
        } catch {
          // Silently fail
        }
      }
      setImageUrls(urls);
    };

    load();

    return () => {
      blobUrlsRef.current.forEach((url) => URL.revokeObjectURL(url));
      blobUrlsRef.current = [];
    };
  }, [feedback]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      blobUrlsRef.current.forEach((url) => URL.revokeObjectURL(url));
    };
  }, []);

  const formatDate = (iso: string) => {
    return new Date(iso).toLocaleDateString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  if (loading) {
    return (
      <div className="mt-6 rounded-2xl bg-surface-container-lowest border border-outline-variant/30 p-5">
        <div className="flex items-center gap-2 text-on-surface-variant text-body-md">
          <span className="material-symbols-outlined animate-spin text-[18px]">sync</span>
          Đang tải thông tin đánh giá...
        </div>
      </div>
    );
  }

  // No feedback yet — show "give feedback" button
  if (!feedback && canGiveFeedback) {
    return (
      <>
        <div className="mt-6 rounded-2xl bg-surface-container-lowest border border-outline-variant/30 p-5">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-amber-50 flex items-center justify-center">
                <span className="material-symbols-outlined text-amber-500 text-[22px]" style={{ fontVariationSettings: "'FILL' 1" }}>
                  star
                </span>
              </div>
              <div>
                <p className="text-body-lg font-bold text-on-surface">Đánh giá đơn hàng</p>
                <p className="text-body-sm text-on-surface-variant">Chia sẻ trải nghiệm của bạn về đơn hàng này</p>
              </div>
            </div>
            <button
              onClick={() => setShowModal(true)}
              className="px-5 py-2.5 rounded-xl bg-primary text-on-primary hover:bg-primary-700 transition-colors text-label-md font-bold flex items-center gap-2"
            >
              <span className="material-symbols-outlined text-[18px]">star</span>
              Đánh giá
            </button>
          </div>
        </div>
        {showModal && (
          <FeedbackModal
            shipmentId={shipmentId}
            shipmentCode={shipmentCode}
            onClose={() => setShowModal(false)}
            onSuccess={loadFeedback}
          />
        )}
      </>
    );
  }

  // No feedback and cannot give (not Completed)
  if (!feedback) return null;

  // Display existing feedback
  return (
    <>
      <div className="mt-6 rounded-2xl bg-surface-container-lowest border border-outline-variant/30 p-5">
        <div className="flex items-center gap-3 mb-3">
          <div className="w-10 h-10 rounded-xl bg-amber-50 flex items-center justify-center">
            <span className="material-symbols-outlined text-amber-500 text-[22px]" style={{ fontVariationSettings: "'FILL' 1" }}>
              star
            </span>
          </div>
          <div>
            <p className="text-body-lg font-bold text-on-surface">Đánh giá của bạn</p>
            <p className="text-body-sm text-on-surface-variant">{formatDate(feedback.createdAt)}</p>
          </div>
        </div>

        <div className="mb-2">
          <StarDisplay rating={feedback.rating} size={24} />
        </div>

        {feedback.comment && (
          <p className="text-body-md text-on-surface mb-3 whitespace-pre-wrap">{feedback.comment}</p>
        )}

        {/* Evidence Thumbnails */}
        {feedback.evidence.length > 0 && (
          <div>
            <p className="text-body-sm font-medium text-on-surface-variant mb-2">
              Ảnh minh chứng ({feedback.evidence.length})
            </p>
            <div className="flex flex-wrap gap-2">
              {feedback.evidence.map((ev) => (
                <button
                  key={ev.id}
                  onClick={() => imageUrls[ev.id] && setLightboxUrl(imageUrls[ev.id])}
                  className="w-20 h-20 rounded-xl overflow-hidden border border-outline-variant hover:border-primary transition-colors"
                >
                  {imageUrls[ev.id] ? (
                    <img
                      src={imageUrls[ev.id]}
                      alt={ev.originalFileName}
                      className="w-full h-full object-cover"
                    />
                  ) : (
                    <div className="w-full h-full bg-surface-container flex items-center justify-center">
                      <span className="material-symbols-outlined text-on-surface-variant/30 text-[20px]">image</span>
                    </div>
                  )}
                </button>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Lightbox */}
      {lightboxUrl && (
        <div
          className="fixed inset-0 z-[60] flex items-center justify-center bg-black/70 p-4"
          onClick={() => setLightboxUrl(null)}
        >
          <img
            src={lightboxUrl}
            alt="Evidence"
            className="max-w-full max-h-[90vh] rounded-2xl object-contain"
          />
          <button className="absolute top-4 right-4 w-10 h-10 rounded-full bg-white/20 backdrop-blur-sm flex items-center justify-center text-white hover:bg-white/30 transition-colors">
            <span className="material-symbols-outlined text-[24px]">close</span>
          </button>
        </div>
      )}

      {showModal && (
        <FeedbackModal
          shipmentId={shipmentId}
          shipmentCode={shipmentCode}
          onClose={() => setShowModal(false)}
          onSuccess={loadFeedback}
        />
      )}
    </>
  );
}
