/**
 * FeedbackModal — Star rating + comment + image upload for customer feedback.
 */
import { useState, useRef } from 'react';
import { createFeedback, uploadFeedbackEvidence } from '../../../api/customer/customerFeedbackApi';
import Toast from '../../matching/Toast';

/* ─── Constants ─────────────────────────────────────────────────── */

const RATING_LABELS: Record<number, string> = {
  1: 'Rất không hài lòng',
  2: 'Không hài lòng',
  3: 'Bình thường',
  4: 'Hài lòng',
  5: 'Rất hài lòng',
};

const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB
const MAX_FILES = 5;

/* ─── Props ─────────────────────────────────────────────────────── */

type Props = {
  shipmentId: string;
  shipmentCode?: string;
  onClose: () => void;
  onSuccess: () => void;
};

/* ─── Component ─────────────────────────────────────────────────── */

export default function FeedbackModal({ shipmentId, shipmentCode, onClose, onSuccess }: Props) {
  const [rating, setRating] = useState(0);
  const [hoverRating, setHoverRating] = useState(0);
  const [comment, setComment] = useState('');
  const [files, setFiles] = useState<File[]>([]);
  const [previews, setPreviews] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const displayRating = hoverRating || rating;

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = Array.from(e.target.files || []);
    const errors: string[] = [];

    const validFiles = selected.filter((file) => {
      if (!ALLOWED_TYPES.includes(file.type)) {
        errors.push(`${file.name}: Định dạng không hỗ trợ`);
        return false;
      }
      if (file.size > MAX_FILE_SIZE) {
        errors.push(`${file.name}: Vượt quá 10MB`);
        return false;
      }
      return true;
    });

    if (files.length + validFiles.length > MAX_FILES) {
      errors.push(`Tối đa ${MAX_FILES} ảnh. Hiện có ${files.length} ảnh.`);
    }

    if (errors.length > 0) {
      setToast({ message: errors.join('. '), type: 'error' });
    }

    const finalFiles = validFiles.slice(0, MAX_FILES - files.length);
    const newPreviews = finalFiles.map((f) => URL.createObjectURL(f));

    setFiles((prev) => [...prev, ...finalFiles]);
    setPreviews((prev) => [...prev, ...newPreviews]);

    // Reset input
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const handleRemoveFile = (index: number) => {
    URL.revokeObjectURL(previews[index]);
    setFiles((prev) => prev.filter((_, i) => i !== index));
    setPreviews((prev) => prev.filter((_, i) => i !== index));
  };

  const handleSubmit = async () => {
    if (rating < 1 || rating > 5) {
      setToast({ message: 'Vui lòng chọn số sao đánh giá.', type: 'error' });
      return;
    }
    if (comment.trim().length > 2000) {
      setToast({ message: 'Nội dung nhận xét không được vượt quá 2000 ký tự.', type: 'error' });
      return;
    }

    setSubmitting(true);
    try {
      // Step 1: Create feedback
      const result = await createFeedback(shipmentId, rating, comment.trim() || undefined);

      // Step 2: Upload evidence if any
      if (files.length > 0) {
        try {
          await uploadFeedbackEvidence(result.id, files);
        } catch (err: any) {
          // Feedback was created, but evidence upload failed
          setToast({
            message: `Đánh giá đã gửi nhưng tải ảnh thất bại: ${err.message}`,
            type: 'error',
          });
          // Still call onSuccess to refresh the feedback display
          onSuccess();
          onClose();
          return;
        }
      }

      setToast({ message: 'Gửi đánh giá thành công!', type: 'success' });
      // Clean up blob URLs
      previews.forEach((url) => URL.revokeObjectURL(url));
      onSuccess();
      onClose();
    } catch (err: any) {
      setToast({ message: err.message || 'Không thể gửi đánh giá.', type: 'error' });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant w-full max-w-lg max-h-[90vh] overflow-y-auto card-shadow">
        {/* Header */}
        <div className="flex items-center justify-between p-5 border-b border-outline-variant/30">
          <div>
            <h2 className="text-title-lg font-bold text-on-surface">Đánh giá đơn hàng</h2>
            {shipmentCode && (
              <p className="text-body-sm text-on-surface-variant mt-0.5">{shipmentCode}</p>
            )}
          </div>
          <button
            onClick={onClose}
            className="w-9 h-9 rounded-xl flex items-center justify-center hover:bg-surface-container-low transition-colors"
          >
            <span className="material-symbols-outlined text-[20px]">close</span>
          </button>
        </div>

        <div className="p-5 space-y-5">
          {/* Star Rating */}
          <div>
            <p className="text-label-lg font-bold text-on-surface mb-3">
              Bạn cảm thấy thế nào về đơn hàng này?
            </p>
            <div className="flex items-center gap-1">
              {[1, 2, 3, 4, 5].map((star) => (
                <button
                  key={star}
                  type="button"
                  onClick={() => setRating(star)}
                  onMouseEnter={() => setHoverRating(star)}
                  onMouseLeave={() => setHoverRating(0)}
                  className="w-10 h-10 flex items-center justify-center transition-transform hover:scale-110"
                >
                  <span
                    className={`material-symbols-outlined text-[36px] ${
                      star <= displayRating
                        ? 'text-amber-400'
                        : 'text-on-surface-variant/30'
                    }`}
                    style={{ fontVariationSettings: star <= displayRating ? "'FILL' 1" : "'FILL' 0" }}
                  >
                    star
                  </span>
                </button>
              ))}
              {displayRating > 0 && (
                <span className="ml-2 text-body-md text-on-surface-variant font-medium">
                  {displayRating}/5 — {RATING_LABELS[displayRating]}
                </span>
              )}
            </div>
          </div>

          {/* Comment */}
          <div>
            <label className="text-label-lg font-bold text-on-surface mb-2 block">
              Nhận xét <span className="text-on-surface-variant font-normal">(không bắt buộc)</span>
            </label>
            <textarea
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              placeholder="Viết nhận xét của bạn..."
              maxLength={2000}
              rows={4}
              className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md placeholder:text-on-surface-variant/50 focus:outline-none focus:border-primary resize-none"
            />
            <p className="text-body-sm text-on-surface-variant/60 mt-1 text-right">
              {comment.length}/2000
            </p>
          </div>

          {/* Image Upload */}
          <div>
            <label className="text-label-lg font-bold text-on-surface mb-2 block">
              Hình ảnh minh chứng <span className="text-on-surface-variant font-normal">(không bắt buộc)</span>
            </label>
            <div className="flex flex-wrap gap-3">
              {previews.map((url, idx) => (
                <div key={idx} className="relative group">
                  <img
                    src={url}
                    alt={`Preview ${idx + 1}`}
                    className="w-20 h-20 rounded-xl object-cover border border-outline-variant"
                  />
                  <button
                    onClick={() => handleRemoveFile(idx)}
                    className="absolute -top-1.5 -right-1.5 w-5 h-5 rounded-full bg-error text-on-error flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity"
                  >
                    <span className="material-symbols-outlined text-[14px]">close</span>
                  </button>
                </div>
              ))}
              {files.length < MAX_FILES && (
                <button
                  onClick={() => fileInputRef.current?.click()}
                  className="w-20 h-20 rounded-xl border-2 border-dashed border-outline-variant hover:border-primary flex flex-col items-center justify-center gap-1 transition-colors"
                >
                  <span className="material-symbols-outlined text-[20px] text-on-surface-variant/50">add_photo_alternate</span>
                  <span className="text-[10px] text-on-surface-variant/50">Thêm ảnh</span>
                </button>
              )}
            </div>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              multiple
              onChange={handleFileSelect}
              className="hidden"
            />
            <p className="text-body-sm text-on-surface-variant/60 mt-2">
              {files.length}/{MAX_FILES} ảnh — Tối đa 10MB/ảnh (JPG, PNG, WEBP)
            </p>
          </div>
        </div>

        {/* Footer */}
        <div className="flex gap-3 p-5 border-t border-outline-variant/30">
          <button
            onClick={onClose}
            disabled={submitting}
            className="flex-1 px-4 py-3 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low transition-colors text-label-md font-bold disabled:opacity-50"
          >
            Hủy
          </button>
          <button
            onClick={handleSubmit}
            disabled={rating < 1 || submitting}
            className="flex-1 px-4 py-3 rounded-xl bg-primary text-on-primary hover:bg-primary-700 transition-colors text-label-md font-bold disabled:opacity-50"
          >
            {submitting ? (
              <span className="flex items-center justify-center gap-2">
                <span className="material-symbols-outlined animate-spin text-[18px]">sync</span>
                Đang gửi...
              </span>
            ) : (
              'Gửi đánh giá'
            )}
          </button>
        </div>
      </div>

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
}
