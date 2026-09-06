/**
 * StaffFeedbackListPage — Admin/Staff shared feedback management list.
 * Admin sees all feedbacks; Warehouse_Staff sees only their hub's feedbacks (enforced server-side).
 */
import { useCallback, useEffect, useState } from 'react';
import {
  listFeedbacks,
  type StaffFeedbackPagedResult,
} from '../api/staffFeedbackApi';

/* ─── Constants ─────────────────────────────────────────────────── */

const RATING_BADGE: Record<number, string> = {
  1: 'bg-red-50 text-red-700 border border-red-200',
  2: 'bg-orange-50 text-orange-700 border border-orange-200',
  3: 'bg-yellow-50 text-yellow-700 border border-yellow-200',
  4: 'bg-blue-50 text-blue-700 border border-blue-200',
  5: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
};

/* ─── Props ─────────────────────────────────────────────────────── */

type Props = {
  onLogout: () => void;
  onSelectFeedback: (feedbackId: string) => void;
};

/* ─── Component ─────────────────────────────────────────────────── */

export default function StaffFeedbackListPage({ onLogout: _onLogout, onSelectFeedback }: Props) {
  const [data, setData] = useState<StaffFeedbackPagedResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [ratingFilter, setRatingFilter] = useState<number | ''>('');
  const [searchText, setSearchText] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');

  // Debounce search
  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(searchText), 400);
    return () => clearTimeout(t);
  }, [searchText]);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const result = await listFeedbacks({
        page,
        pageSize: 10,
        rating: ratingFilter !== '' ? ratingFilter : undefined,
        search: debouncedSearch || undefined,
      });
      setData(result);
    } catch {
      // errors silently shown as empty list
    } finally {
      setLoading(false);
    }
  }, [page, ratingFilter, debouncedSearch]);

  useEffect(() => { fetchData(); }, [fetchData]);

  // Reset page when filters change
  useEffect(() => { setPage(1); }, [ratingFilter, debouncedSearch]);

  const totalPages = data?.totalPages ?? 1;

  const formatDate = (iso: string) => {
    return new Date(iso).toLocaleDateString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  /* ─── Loading Skeleton ─────────────────────────────────────────── */

  if (loading && !data) {
    return (
      <div className="p-6 xl:p-8 space-y-4 max-w-7xl mx-auto">
        <div className="h-8 w-48 bg-surface-container-highest rounded-lg animate-pulse" />
        {[1, 2, 3].map(i => (
          <div key={i} className="bg-surface-container-low rounded-2xl border border-outline-variant p-5 animate-pulse">
            <div className="flex gap-4">
              <div className="w-12 h-12 bg-surface-container-highest rounded-xl" />
              <div className="flex-1 space-y-2">
                <div className="h-4 bg-surface-container-highest rounded w-1/3" />
                <div className="h-3 bg-surface-container-highest rounded w-1/2" />
              </div>
            </div>
          </div>
        ))}
      </div>
    );
  }

  /* ─── Render ───────────────────────────────────────────────────── */

  return (
    <div className="p-6 xl:p-8 space-y-6 max-w-7xl mx-auto">
      {/* Header */}
      <div>
        <h1 className="text-headline-lg font-headline-lg text-on-surface flex items-center gap-3">
          <span className="material-symbols-outlined text-[28px] text-amber-500" style={{ fontVariationSettings: "'FILL' 1" }}>
            star
          </span>
          Phản hồi khách hàng
        </h1>
        <p className="text-body-md text-on-surface-variant mt-1">
          {data?.totalCount ?? 0} phản hồi — Trang {page}/{totalPages}
        </p>
      </div>

      {/* Filters */}
      <div className="bg-surface-container-low rounded-2xl border border-outline-variant p-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {/* Search */}
          <div className="relative">
            <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-on-surface-variant/50 text-[20px]">search</span>
            <input
              type="text"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              placeholder="Tìm mã đơn hàng, tên khách hàng, nội dung..."
              className="w-full pl-10 pr-4 py-2.5 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md placeholder:text-on-surface-variant/50 focus:outline-none focus:border-primary"
            />
          </div>

          {/* Rating Filter */}
          <select
            value={ratingFilter}
            onChange={(e) => setRatingFilter(e.target.value ? Number(e.target.value) : '')}
            className="px-4 py-2.5 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md focus:outline-none focus:border-primary"
          >
            <option value="">Tất cả đánh giá</option>
            {[5, 4, 3, 2, 1].map((r) => (
              <option key={r} value={r}>{r} sao</option>
            ))}
          </select>
        </div>
      </div>

      {/* Empty State */}
      {!loading && data && data.items.length === 0 && (
        <div className="bg-surface-container-low rounded-2xl border border-outline-variant p-12 text-center">
          <span className="material-symbols-outlined text-[48px] text-on-surface-variant/30 mb-3">star</span>
          <p className="text-title-lg font-bold text-on-surface-variant">Không có phản hồi nào</p>
          <p className="text-body-md text-on-surface-variant/70 mt-1">Không tìm thấy phản hồi phù hợp với bộ lọc.</p>
        </div>
      )}

      {/* Feedback Cards */}
      <div className="space-y-3">
        {data?.items.map((fb) => (
          <button
            key={fb.id}
            onClick={() => onSelectFeedback(fb.id)}
            className="w-full text-left bg-surface-container-lowest rounded-2xl border border-outline-variant p-5 hover:shadow-md hover:border-primary/30 transition-all duration-200 group"
          >
            <div className="flex items-start gap-4">
              {/* Rating Icon */}
              <div className={`w-12 h-12 rounded-xl flex items-center justify-center shrink-0 ${RATING_BADGE[fb.rating] || 'bg-gray-50 text-gray-600'}`}>
                <div className="flex items-center gap-0.5">
                  <span
                    className="material-symbols-outlined text-[18px]"
                    style={{ fontVariationSettings: "'FILL' 1" }}
                  >
                    star
                  </span>
                  <span className="text-label-lg font-bold">{fb.rating}</span>
                </div>
              </div>

              {/* Content */}
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="text-label-lg font-bold text-on-surface">
                    {fb.shipmentCode || 'N/A'}
                  </span>
                  <span className="text-label-sm text-on-surface-variant">
                    bởi {fb.customerName || 'Khách hàng'}
                  </span>
                </div>

                {fb.comment && (
                  <p className="text-body-md text-on-surface-variant mt-1 line-clamp-2">
                    {fb.comment}
                  </p>
                )}

                <div className="flex items-center gap-4 mt-2 flex-wrap">
                  {fb.evidenceCount > 0 && (
                    <span className="inline-flex items-center gap-1 text-label-sm text-on-surface-variant">
                      <span className="material-symbols-outlined text-[14px]">image</span>
                      {fb.evidenceCount} ảnh
                    </span>
                  )}
                  <span className="text-label-sm text-on-surface-variant/60">
                    {formatDate(fb.createdAt)}
                  </span>
                </div>
              </div>

              {/* Arrow */}
              <span className="material-symbols-outlined text-[20px] text-on-surface-variant/30 group-hover:text-primary transition-colors mt-1">
                chevron_right
              </span>
            </div>
          </button>
        ))}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 pt-2">
          <button
            onClick={() => setPage(Math.max(1, page - 1))}
            disabled={page <= 1}
            className="w-10 h-10 rounded-xl flex items-center justify-center border border-outline-variant hover:bg-surface-container-low disabled:opacity-40 transition-colors"
          >
            <span className="material-symbols-outlined text-[18px]">chevron_left</span>
          </button>
          {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
            const start = Math.max(1, Math.min(page - 2, totalPages - 4));
            const pageNum = start + i;
            if (pageNum > totalPages) return null;
            return (
              <button
                key={pageNum}
                onClick={() => setPage(pageNum)}
                className={`w-10 h-10 rounded-xl flex items-center justify-center text-label-md font-bold transition-colors ${
                  pageNum === page
                    ? 'bg-primary text-on-primary'
                    : 'border border-outline-variant hover:bg-surface-container-low text-on-surface'
                }`}
              >
                {pageNum}
              </button>
            );
          })}
          <button
            onClick={() => setPage(Math.min(totalPages, page + 1))}
            disabled={page >= totalPages}
            className="w-10 h-10 rounded-xl flex items-center justify-center border border-outline-variant hover:bg-surface-container-low disabled:opacity-40 transition-colors"
          >
            <span className="material-symbols-outlined text-[18px]">chevron_right</span>
          </button>
        </div>
      )}
    </div>
  );
}
