/**
 * StaffFeedbackDetailPage — Full feedback detail with evidence viewer.
 * Shared between Admin and Warehouse_Staff. Hub isolation enforced server-side.
 */
import { useEffect, useState, useRef } from 'react';
import { authFetch } from '../utils/authFetch';
import {
  getFeedbackDetail,
  getEvidenceDownloadUrl,
  type StaffFeedbackDetail,
} from '../api/staffFeedbackApi';
import Toast from '../components/matching/Toast';

/* ─── Props ─────────────────────────────────────────────────────── */

type Props = {
  feedbackId: string;
  onBack: () => void;
  onLogout: () => void;
};

/* ─── Component ─────────────────────────────────────────────────── */

export default function StaffFeedbackDetailPage({ feedbackId, onBack, onLogout: _onLogout }: Props) {
  const [detail, setDetail] = useState<StaffFeedbackDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  // Evidence lightbox
  const [lightboxUrl, setLightboxUrl] = useState<string | null>(null);

  // Evidence blob URLs (auth-aware image loading)
  const [evidenceUrls, setEvidenceUrls] = useState<Record<string, string>>({});
  const blobUrlsRef = useRef<string[]>([]);

  // Fetch evidence images with auth headers → blob URLs
  const loadEvidenceImages = async (detailData: StaffFeedbackDetail) => {
    // Cleanup old blob URLs
    blobUrlsRef.current.forEach((url) => URL.revokeObjectURL(url));
    blobUrlsRef.current = [];

    const urls: Record<string, string> = {};
    for (const ev of detailData.evidence) {
      try {
        const res = await authFetch(getEvidenceDownloadUrl(detailData.id, ev.id));
        if (res.ok) {
          const blob = await res.blob();
          const blobUrl = URL.createObjectURL(blob);
          urls[ev.id] = blobUrl;
          blobUrlsRef.current.push(blobUrl);
        }
      } catch {
        // Silently fail — show placeholder
      }
    }
    setEvidenceUrls(urls);
  };

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      blobUrlsRef.current.forEach((url) => URL.revokeObjectURL(url));
    };
  }, []);

  const loadDetail = async () => {
    setLoading(true);
    try {
      const result = await getFeedbackDetail(feedbackId);
      setDetail(result);
      // Load evidence images with auth
      if (result.evidence.length > 0) {
        loadEvidenceImages(result);
      }
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi tải dữ liệu', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { loadDetail(); }, [feedbackId]);

  const formatDate = (iso: string) => {
    return new Date(iso).toLocaleDateString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const formatFileSize = (bytes: number) => {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  };

  /* ─── Star Display ────────────────────────────────────────────── */

  const StarDisplay = ({ rating, size = 24 }: { rating: number; size?: number }) => (
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

  /* ─── Loading Skeleton ─────────────────────────────────────────── */

  if (loading) {
    return (
      <div className="p-6 xl:p-8 space-y-6 max-w-7xl mx-auto">
        <div className="h-8 w-48 bg-surface-container-highest rounded-lg animate-pulse" />
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <div className="lg:col-span-2 space-y-4">
            <div className="bg-surface-container-low rounded-2xl h-48 animate-pulse" />
          </div>
          <div className="space-y-4">
            <div className="bg-surface-container-low rounded-2xl h-32 animate-pulse" />
          </div>
        </div>
      </div>
    );
  }

  /* ─── Not Found ────────────────────────────────────────────────── */

  if (!detail) {
    return (
      <div className="p-6 xl:p-8 max-w-7xl mx-auto">
        <div className="bg-surface-container-low rounded-2xl border border-outline-variant p-12 text-center">
          <span className="material-symbols-outlined text-[48px] text-on-surface-variant/30 mb-3">error</span>
          <p className="text-title-lg font-bold text-on-surface-variant">Không tìm thấy phản hồi</p>
          <button
            onClick={onBack}
            className="mt-4 px-6 py-2.5 rounded-xl bg-primary text-on-primary text-label-md font-bold hover:bg-primary/90 transition-colors"
          >
            Quay lại danh sách
          </button>
        </div>
      </div>
    );
  }

  /* ─── Render ───────────────────────────────────────────────────── */

  return (
    <div className="p-6 xl:p-8 space-y-6 max-w-7xl mx-auto">
      {/* Toast */}
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}

      {/* Back */}
      <button
        onClick={onBack}
        className="flex items-center gap-1 text-primary hover:text-primary/80 transition-colors text-label-md font-bold"
      >
        <span className="material-symbols-outlined text-[18px]">arrow_back</span>
        Quay lại danh sách
      </button>

      {/* Header */}
      <div className="flex items-start gap-4">
        <div className="w-14 h-14 rounded-2xl bg-amber-50 flex items-center justify-center shrink-0">
          <span className="material-symbols-outlined text-amber-500 text-[28px]" style={{ fontVariationSettings: "'FILL' 1" }}>
            star
          </span>
        </div>
        <div>
          <h1 className="text-headline-lg font-headline-lg text-on-surface">
            Phản hồi #{detail.id.substring(0, 8)}
          </h1>
          <p className="text-body-md text-on-surface-variant mt-0.5">
            Đơn hàng: {detail.shipmentCode || 'N/A'}
            {detail.shipmentStatus && <span className="ml-2 text-label-sm text-on-surface-variant/60">({detail.shipmentStatus})</span>}
          </p>
        </div>
      </div>

      {/* Two-column layout */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left: Main content */}
        <div className="lg:col-span-2 space-y-4">
          {/* Rating Card */}
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
            <h3 className="text-title-lg font-bold text-on-surface flex items-center gap-2 mb-3">
              <span className="material-symbols-outlined text-[20px]">rate_review</span>
              Đánh giá
            </h3>
            <div className="flex items-center gap-3 mb-3">
              <StarDisplay rating={detail.rating} size={32} />
              <span className="text-headline-sm font-bold text-on-surface">{detail.rating}/5</span>
            </div>
            {detail.comment ? (
              <p className="text-body-md text-on-surface whitespace-pre-wrap">{detail.comment}</p>
            ) : (
              <p className="text-body-md text-on-surface-variant/50 italic">Không có nhận xét.</p>
            )}
          </div>

          {/* Evidence Card */}
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
            <h3 className="text-title-lg font-bold text-on-surface flex items-center gap-2 mb-3">
              <span className="material-symbols-outlined text-[20px]">image</span>
              Hình ảnh minh chứng ({detail.evidence.length})
            </h3>
            {detail.evidence.length === 0 ? (
              <p className="text-body-md text-on-surface-variant/60 italic">Không có hình ảnh.</p>
            ) : (
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
                {detail.evidence.map((ev) => (
                  <div key={ev.id} className="group cursor-pointer" onClick={() => {
                    if (evidenceUrls[ev.id]) setLightboxUrl(evidenceUrls[ev.id]);
                  }}>
                    <div className="relative aspect-square rounded-xl overflow-hidden border border-outline-variant">
                      {evidenceUrls[ev.id] ? (
                        <img
                          src={evidenceUrls[ev.id]}
                          alt={ev.originalFileName}
                          className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-200"
                        />
                      ) : (
                        <div className="w-full h-full bg-surface-container flex items-center justify-center">
                          <span className="material-symbols-outlined text-on-surface-variant/30 text-[32px]">image</span>
                        </div>
                      )}
                      {/* Overlay on hover */}
                      <div className="absolute inset-0 bg-black/0 group-hover:bg-black/20 transition-colors flex items-center justify-center">
                        <span className="material-symbols-outlined text-white opacity-0 group-hover:opacity-100 transition-opacity text-[28px]">
                          zoom_in
                        </span>
                      </div>
                    </div>
                    <p className="text-label-sm text-on-surface-variant mt-1 truncate">
                      {ev.originalFileName}
                    </p>
                    <p className="text-label-xs text-on-surface-variant/60">
                      {formatFileSize(ev.fileSize)}
                    </p>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* Right: Sidebar info */}
        <div className="space-y-4">
          {/* Customer Info */}
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
            <h3 className="text-title-md font-bold text-on-surface flex items-center gap-2 mb-3">
              <span className="material-symbols-outlined text-[18px]">person</span>
              Khách hàng
            </h3>
            <div className="space-y-2">
              <InfoRow label="Họ tên" value={detail.customerName || 'N/A'} />
              <InfoRow label="Email" value={detail.customerEmail || 'N/A'} />
            </div>
          </div>

          {/* Shipment Info */}
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
            <h3 className="text-title-md font-bold text-on-surface flex items-center gap-2 mb-3">
              <span className="material-symbols-outlined text-[18px]">local_shipping</span>
              Đơn hàng
            </h3>
            <div className="space-y-2">
              <InfoRow label="Mã đơn" value={detail.shipmentCode || 'N/A'} />
              <InfoRow label="Trạng thái" value={detail.shipmentStatus || 'N/A'} />
            </div>
          </div>

          {/* Timestamps */}
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
            <h3 className="text-title-md font-bold text-on-surface flex items-center gap-2 mb-3">
              <span className="material-symbols-outlined text-[18px]">schedule</span>
              Thời gian
            </h3>
            <div className="space-y-2">
              <InfoRow label="Tạo lúc" value={formatDate(detail.createdAt)} />
              <InfoRow label="Cập nhật" value={formatDate(detail.updatedAt)} />
            </div>
          </div>
        </div>
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
    </div>
  );
}

/* ─── Helper Component ──────────────────────────────────────────── */

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-2">
      <span className="text-body-sm text-on-surface-variant">{label}</span>
      <span className="text-body-sm font-medium text-on-surface text-right">{value}</span>
    </div>
  );
}
