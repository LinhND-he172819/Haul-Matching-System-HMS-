/**
 * IncidentDetailPage — Full incident detail with evidence viewer and state management actions.
 * Shared between Admin and Warehouse_Staff. Hub isolation enforced server-side.
 */
import { useEffect, useState, useRef } from 'react';
import { authFetch } from '../utils/authFetch';
import {
  getIncidentDetail,
  getIncidentHistory,
  takeIncident,
  resolveIncident,
  rejectIncident,
  getEvidenceDownloadUrl,
  type AdminIncidentDetail,
  type AdminIncidentHistoryItem,
} from '../api/incidentApi';
import Toast from '../components/matching/Toast';

/* ─── Constants ─────────────────────────────────────────────────── */

const STATUS_BADGE: Record<string, string> = {
  Open: 'bg-amber-50 text-amber-700 border border-amber-200',
  InProgress: 'bg-blue-50 text-blue-700 border border-blue-200',
  Resolved: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
  Rejected: 'bg-red-50 text-red-700 border border-red-200',
};

const STATUS_LABEL: Record<string, string> = {
  Open: 'Mới',
  InProgress: 'Đang xử lý',
  Resolved: 'Đã xử lý',
  Rejected: 'Đã từ chối',
};

/** Audit action → Vietnamese label */
const ACTION_LABEL: Record<string, string> = {
  IncidentReported: 'Báo cáo sự cố',
  IncidentTaken: 'Tiếp nhận xử lý',
  IncidentResolved: 'Hoàn tất xử lý',
  IncidentRejected: 'Từ chối sự cố',
};

/** State transition display */
const ACTION_TRANSITION: Record<string, string> = {
  IncidentReported: '→ Mới',
  IncidentTaken: 'Mới → Đang xử lý',
  IncidentResolved: 'Đang xử lý → Đã xử lý',
  IncidentRejected: 'Mới → Đã từ chối',
};

const INCIDENT_TYPE_LABEL: Record<string, string> = {
  Delay: 'Trễ hạn',
  VehicleBreakdown: 'Hỏng xe',
  Accident: 'Tai nạn',
  CargoDamage: 'Hư hỏng hàng',
  CargoLost: 'Mất hàng',
  DeliveryProblem: 'Sự cố giao hàng',
  RouteProblem: 'Sự cố tuyến đường',
  Weather: 'Thời tiết',
  Other: 'Khác',
};

/* ─── Props ─────────────────────────────────────────────────────── */

type Props = {
  incidentId: string;
  onBack: () => void;
  onLogout: () => void;
  /** true = Admin role; false = Warehouse_Staff */
  isAdmin: boolean;
};

/* ─── Component ─────────────────────────────────────────────────── */

export default function IncidentDetailPage({ incidentId, onBack, onLogout, isAdmin }: Props) {
  const [detail, setDetail] = useState<AdminIncidentDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  // Action dialog states
  const [showResolveDialog, setShowResolveDialog] = useState(false);
  const [showRejectDialog, setShowRejectDialog] = useState(false);
  const [actionNote, setActionNote] = useState('');

  // Incident history
  const [historyItems, setHistoryItems] = useState<AdminIncidentHistoryItem[]>([]);

  // Evidence lightbox
  const [lightboxUrl, setLightboxUrl] = useState<string | null>(null);

  // Evidence blob URLs (auth-aware image loading)
  const [evidenceUrls, setEvidenceUrls] = useState<Record<string, string>>({});
  const blobUrlsRef = useRef<string[]>([]);

  // Fetch evidence images with auth headers → blob URLs
  const loadEvidenceImages = async (detailData: AdminIncidentDetail) => {
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
      const result = await getIncidentDetail(incidentId);
      setDetail(result);
      // Load evidence images with auth
      if (result.evidence.length > 0) {
        loadEvidenceImages(result);
      }
      // Load history
      try {
        const historyResult = await getIncidentHistory(incidentId);
        setHistoryItems(historyResult.items || []);
      } catch {
        // History load failure is non-critical
      }
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi tải dữ liệu', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { loadDetail(); }, [incidentId]);

  // ─── State Change Handlers ─────────────────────────────────────

  const handleTake = async () => {
    setActionLoading(true);
    try {
      const res = await takeIncident(incidentId);
      const emailMsg = res.emailSent ? ' ✉️ Email đã gửi.' : res.emailError ? ` ⚠️ Email lỗi: ${res.emailError}` : '';
      setToast({ message: (res.message || 'Đã tiếp nhận sự cố.') + emailMsg, type: 'success' });
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi', type: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleResolve = async () => {
    if (!actionNote.trim()) {
      setToast({ message: 'Vui lòng nhập kết quả xử lý.', type: 'error' });
      return;
    }
    setActionLoading(true);
    try {
      const res = await resolveIncident(incidentId, actionNote.trim());
      const emailMsg = res.emailSent ? ' ✉️ Email đã gửi.' : res.emailError ? ` ⚠️ Email lỗi: ${res.emailError}` : '';
      setToast({ message: (res.message || 'Đã hoàn tất xử lý sự cố.') + emailMsg, type: 'success' });
      setShowResolveDialog(false);
      setActionNote('');
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi', type: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleReject = async () => {
    if (!actionNote.trim()) {
      setToast({ message: 'Vui lòng nhập lý do từ chối.', type: 'error' });
      return;
    }
    setActionLoading(true);
    try {
      const res = await rejectIncident(incidentId, actionNote.trim());
      const emailMsg = res.emailSent ? ' ✉️ Email đã gửi.' : res.emailError ? ` ⚠️ Email lỗi: ${res.emailError}` : '';
      setToast({ message: (res.message || 'Đã từ chối sự cố.') + emailMsg, type: 'success' });
      setShowRejectDialog(false);
      setActionNote('');
      await loadDetail();
    } catch (err: any) {
      setToast({ message: err.message || 'Lỗi', type: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  const formatFileSize = (bytes: number) => {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  };

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
          <p className="text-title-lg font-bold text-on-surface-variant">Không tìm thấy sự cố</p>
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

  const allowed = detail.allowedActions?.[0] ?? { canTake: false, canResolve: false, canReject: false };

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
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div className="flex items-center gap-3">
          <span className="material-symbols-outlined text-[32px] text-error">warning</span>
          <div>
            <h1 className="text-headline-lg font-headline-lg text-on-surface flex items-center gap-2">
              {detail.incidentCode}
              <span className={`inline-flex items-center px-3 py-1 rounded-full text-label-md font-medium ${STATUS_BADGE[detail.status] || ''}`}>
                {STATUS_LABEL[detail.status] || detail.status}
              </span>
            </h1>
            <p className="text-body-md text-on-surface-variant mt-0.5">
              {INCIDENT_TYPE_LABEL[detail.incidentType] || detail.incidentType}
            </p>
          </div>
        </div>

        {/* Action Buttons */}
        <div className="flex gap-2 flex-wrap">
          {allowed.canTake && (
            <button
              onClick={handleTake}
              disabled={actionLoading}
              className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-blue-600 text-white text-label-md font-bold hover:bg-blue-700 transition-colors disabled:opacity-50"
            >
              <span className="material-symbols-outlined text-[18px]">handshake</span>
              Tiếp nhận xử lý
            </button>
          )}
          {allowed.canResolve && (
            <button
              onClick={() => { setShowResolveDialog(true); setActionNote(''); }}
              disabled={actionLoading}
              className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-emerald-600 text-white text-label-md font-bold hover:bg-emerald-700 transition-colors disabled:opacity-50"
            >
              <span className="material-symbols-outlined text-[18px]">check_circle</span>
              Hoàn tất xử lý
            </button>
          )}
          {allowed.canReject && isAdmin && (
            <button
              onClick={() => { setShowRejectDialog(true); setActionNote(''); }}
              disabled={actionLoading}
              className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-red-600 text-white text-label-md font-bold hover:bg-red-700 transition-colors disabled:opacity-50"
            >
              <span className="material-symbols-outlined text-[18px]">cancel</span>
              Từ chối
            </button>
          )}
        </div>
      </div>

      {/* Two-column layout */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left: Main info */}
        <div className="lg:col-span-2 space-y-4">
          {/* Description Card */}
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
            <h3 className="text-title-lg font-bold text-on-surface flex items-center gap-2 mb-3">
              <span className="material-symbols-outlined text-[20px]">description</span>
              Mô tả sự cố
            </h3>
            <p className="text-body-md text-on-surface whitespace-pre-wrap">{detail.description}</p>
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
                        <div className="w-full h-full flex items-center justify-center bg-surface-container-highest">
                          <span className="material-symbols-outlined text-[28px] text-on-surface-variant/30">image</span>
                        </div>
                      )}
                      <div className="absolute inset-0 bg-black/0 group-hover:bg-black/10 transition-colors flex items-center justify-center">
                        <span className="material-symbols-outlined text-white opacity-0 group-hover:opacity-100 text-[28px] transition-opacity">
                          zoom_in
                        </span>
                      </div>
                    </div>
                    <p className="text-caption text-on-surface-variant truncate mt-1" title={ev.originalFileName}>
                      {ev.originalFileName}
                    </p>
                    <p className="text-caption text-on-surface-variant/50">
                      {formatFileSize(ev.fileSize)}
                    </p>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Resolution Note (if resolved/rejected) */}
          {(detail.resolutionNote || detail.status === 'Resolved' || detail.status === 'Rejected') && detail.resolutionNote && (
            <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
              <h3 className="text-title-lg font-bold text-on-surface flex items-center gap-2 mb-3">
                <span className="material-symbols-outlined text-[20px]">notes</span>
                Ghi chú xử lý
              </h3>
              <p className="text-body-md text-on-surface whitespace-pre-wrap">{detail.resolutionNote}</p>
              <div className="mt-3 flex items-center gap-4 text-label-sm text-on-surface-variant">
                {detail.resolvedByName && (
                  <span className="flex items-center gap-1">
                    <span className="material-symbols-outlined text-[14px]">person</span>
                    {detail.resolvedByName}
                  </span>
                )}
                {detail.resolvedAt && (
                  <span>{new Date(detail.resolvedAt).toLocaleString('vi-VN')}</span>
                )}
              </div>
            </div>
          )}

          {/* ─── Xử lý sự cố Section ─────────────────────────── */}
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
            <h3 className="text-title-lg font-bold text-on-surface flex items-center gap-2 mb-4">
              <span className="material-symbols-outlined text-[20px]">construction</span>
              Xử lý sự cố
            </h3>

            {/* CASE 1: Open */}
            {detail.status === 'Open' && (
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <span className="inline-flex items-center px-3 py-1 rounded-full text-label-md font-medium bg-amber-50 text-amber-700 border border-amber-200">
                    Mới
                  </span>
                </div>
                <p className="text-body-md text-on-surface-variant">
                  Chưa có người tiếp nhận xử lý.
                </p>
                <p className="text-body-sm text-on-surface-variant/60 italic">
                  Sử dụng nút "Tiếp nhận xử lý" hoặc "Từ chối" ở trên để thực hiện hành động.
                </p>
              </div>
            )}

            {/* CASE 2: InProgress */}
            {detail.status === 'InProgress' && (
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <span className="inline-flex items-center px-3 py-1 rounded-full text-label-md font-medium bg-blue-50 text-blue-700 border border-blue-200">
                    Đang xử lý
                  </span>
                </div>
                {detail.assignedToName && (
                  <div className="space-y-1">
                    <InfoRow icon="person" label="Người tiếp nhận" value={detail.assignedToName} />
                    {detail.assignedAt && (
                      <InfoRow icon="schedule" label="Thời gian tiếp nhận" value={new Date(detail.assignedAt).toLocaleString('vi-VN')} />
                    )}
                  </div>
                )}
                {allowed.canResolve && (
                  <p className="text-body-sm text-on-surface-variant/60 italic pt-2">
                    Sử dụng nút "Hoàn tất xử lý" ở trên để hoàn tất.
                  </p>
                )}
              </div>
            )}

            {/* CASE 3: Resolved */}
            {detail.status === 'Resolved' && (
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <span className="inline-flex items-center px-3 py-1 rounded-full text-label-md font-medium bg-emerald-50 text-emerald-700 border border-emerald-200">
                    Đã xử lý
                  </span>
                </div>
                <div className="space-y-1">
                  {detail.assignedToName && (
                    <InfoRow icon="person" label="Người tiếp nhận" value={detail.assignedToName} />
                  )}
                  {detail.assignedAt && (
                    <InfoRow icon="schedule" label="Thời gian tiếp nhận" value={new Date(detail.assignedAt).toLocaleString('vi-VN')} />
                  )}
                  {detail.resolvedByName && (
                    <InfoRow icon="person" label="Người hoàn tất" value={detail.resolvedByName} />
                  )}
                  {detail.resolvedAt && (
                    <InfoRow icon="check_circle" label="Thời gian hoàn tất" value={new Date(detail.resolvedAt).toLocaleString('vi-VN')} />
                  )}
                </div>
              </div>
            )}

            {/* CASE 4: Rejected */}
            {detail.status === 'Rejected' && (
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <span className="inline-flex items-center px-3 py-1 rounded-full text-label-md font-medium bg-red-50 text-red-700 border border-red-200">
                    Đã từ chối
                  </span>
                </div>
                <div className="space-y-1">
                  {detail.resolvedByName && (
                    <InfoRow icon="person" label="Người từ chối" value={detail.resolvedByName} />
                  )}
                  {detail.resolvedAt && (
                    <InfoRow icon="schedule" label="Thời gian từ chối" value={new Date(detail.resolvedAt).toLocaleString('vi-VN')} />
                  )}
                  {detail.resolutionNote && (
                    <InfoRow icon="notes" label="Lý do" value={detail.resolutionNote} />
                  )}
                </div>
              </div>
            )}
          </div>

          {/* ─── Lịch sử xử lý Section ────────────────────────── */}
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5">
            <h3 className="text-title-lg font-bold text-on-surface flex items-center gap-2 mb-4">
              <span className="material-symbols-outlined text-[20px]">history</span>
              Lịch sử xử lý
            </h3>
            {historyItems.length === 0 ? (
              <p className="text-body-md text-on-surface-variant/60 italic">Chưa có lịch sử.</p>
            ) : (
              <div className="relative">
                {/* Timeline line */}
                <div className="absolute left-[7px] top-3 bottom-3 w-0.5 bg-outline-variant/30" />
                <div className="space-y-4">
                  {historyItems.map((item, idx) => (
                    <div key={item.id || idx} className="relative flex gap-3">
                      {/* Dot */}
                      <div className={`relative z-10 w-[15px] h-[15px] rounded-full border-2 shrink-0 mt-0.5 ${
                        item.action === 'IncidentReported' ? 'bg-amber-100 border-amber-400' :
                        item.action === 'IncidentTaken' ? 'bg-blue-100 border-blue-400' :
                        item.action === 'IncidentResolved' ? 'bg-emerald-100 border-emerald-400' :
                        'bg-red-100 border-red-400'
                      }`} />
                      {/* Content */}
                      <div className="flex-1 min-w-0 pb-1">
                        <p className="text-label-md font-bold text-on-surface">
                          {ACTION_LABEL[item.action] || item.action}
                        </p>
                        <p className="text-label-sm text-on-surface-variant mt-0.5">
                          {item.actorName}
                          {ACTION_TRANSITION[item.action] && (
                            <span className="ml-1 text-on-surface-variant/70">
                              ({ACTION_TRANSITION[item.action]})
                            </span>
                          )}
                        </p>
                        {item.note && (
                          <p className="text-label-sm text-on-surface-variant/70 mt-0.5 italic">
                            {item.action === 'IncidentRejected' ? 'Lý do: ' : 'Ghi chú: '}{item.note}
                          </p>
                        )}
                        <p className="text-caption text-on-surface-variant/50 mt-1">
                          {new Date(item.createdAt).toLocaleString('vi-VN')}
                        </p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Right: Sidebar info */}
        <div className="space-y-4">
          {/* Trip Info Card */}
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5 space-y-3">
            <h3 className="text-title-lg font-bold text-on-surface flex items-center gap-2">
              <span className="material-symbols-outlined text-[20px]">route</span>
              Thông tin chuyến đi
            </h3>
            <div className="space-y-2">
              <InfoRow icon="tag" label="Mã chuyến" value={detail.tripCode} />
              <InfoRow icon="directions" label="Tuyến" value={detail.route || 'N/A'} />
              <InfoRow icon="garage" label="Biển số xe" value={detail.vehiclePlate || 'N/A'} />
            </div>
          </div>

          {/* Driver Info Card */}
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5 space-y-3">
            <h3 className="text-title-lg font-bold text-on-surface flex items-center gap-2">
              <span className="material-symbols-outlined text-[20px]">person</span>
              Tài xế báo cáo
            </h3>
            <div className="space-y-2">
              <InfoRow icon="badge" label="Tên" value={detail.driverName} />
            </div>
          </div>

          {/* Assignment Info */}
          {detail.assignedToName && (
            <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5 space-y-3">
              <h3 className="text-title-lg font-bold text-on-surface flex items-center gap-2">
                <span className="material-symbols-outlined text-[20px]">assignment_ind</span>
                Người xử lý
              </h3>
              <div className="space-y-2">
                <InfoRow icon="person" label="Phụ trách" value={detail.assignedToName} />
                {detail.assignedAt && (
                  <InfoRow icon="schedule" label="Tiếp nhận lúc" value={new Date(detail.assignedAt).toLocaleString('vi-VN')} />
                )}
              </div>
            </div>
          )}

          {/* Timeline Card */}
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-5 space-y-3">
            <h3 className="text-title-lg font-bold text-on-surface flex items-center gap-2">
              <span className="material-symbols-outlined text-[20px]">timeline</span>
              Thời gian
            </h3>
            <div className="space-y-2">
              <InfoRow icon="report" label="Báo cáo lúc" value={new Date(detail.reportedAt).toLocaleString('vi-VN')} />
              {detail.updatedAt && (
                <InfoRow icon="update" label="Cập nhật" value={new Date(detail.updatedAt).toLocaleString('vi-VN')} />
              )}
              {detail.resolvedAt && (
                <InfoRow icon="check_circle" label="Giải quyết lúc" value={new Date(detail.resolvedAt).toLocaleString('vi-VN')} />
              )}
            </div>
          </div>
        </div>
      </div>

      {/* ─── Resolve Dialog ─────────────────────────────────────── */}
      {showResolveDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-6 w-full max-w-md card-shadow">
            <h3 className="text-title-lg font-bold text-on-surface mb-2">Hoàn tất xử lý sự cố</h3>
            <p className="text-body-md text-on-surface-variant mb-4">
              Nhập kết quả xử lý sự cố này.
            </p>
            <textarea
              value={actionNote}
              onChange={(e) => setActionNote(e.target.value)}
              placeholder="Nhập nội dung xử lý sự cố..."
              className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md placeholder:text-on-surface-variant/50 focus:outline-none focus:border-primary resize-none"
              rows={3}
            />
            <div className="flex gap-3 mt-4">
              <button
                onClick={() => { setShowResolveDialog(false); setActionNote(''); }}
                className="flex-1 px-4 py-3 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low transition-colors text-label-md font-bold"
              >
                Hủy
              </button>
              <button
                onClick={handleResolve}
                disabled={!actionNote.trim() || actionLoading}
                className="flex-1 px-4 py-3 rounded-xl bg-emerald-600 text-white hover:bg-emerald-700 transition-colors text-label-md font-bold disabled:opacity-50"
              >
                {actionLoading ? 'Đang xử lý...' : 'Xác nhận hoàn tất'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ─── Reject Dialog ──────────────────────────────────────── */}
      {showRejectDialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-surface-container-lowest rounded-2xl border border-outline-variant p-6 w-full max-w-md card-shadow">
            <h3 className="text-title-lg font-bold text-on-surface mb-2">Từ chối sự cố</h3>
            <p className="text-body-md text-on-surface-variant mb-4">
              Nhập lý do từ chối sự cố này.
            </p>
            <textarea
              value={actionNote}
              onChange={(e) => setActionNote(e.target.value)}
              placeholder="Lý do từ chối..."
              className="w-full px-4 py-3 rounded-xl border border-outline-variant bg-surface text-on-surface text-body-md placeholder:text-on-surface-variant/50 focus:outline-none focus:border-primary resize-none"
              rows={3}
            />
            <div className="flex gap-3 mt-4">
              <button
                onClick={() => { setShowRejectDialog(false); setActionNote(''); }}
                className="flex-1 px-4 py-3 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low transition-colors text-label-md font-bold"
              >
                Hủy
              </button>
              <button
                onClick={handleReject}
                disabled={!actionNote.trim() || actionLoading}
                className="flex-1 px-4 py-3 rounded-xl bg-red-600 text-white hover:bg-red-700 transition-colors text-label-md font-bold disabled:opacity-50"
              >
                {actionLoading ? 'Đang từ chối...' : 'Từ chối'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ─── Lightbox ──────────────────────────────────────────── */}
      {lightboxUrl && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 p-4"
          onClick={() => setLightboxUrl(null)}
        >
          <button
            className="absolute top-4 right-4 text-white hover:text-white/80 z-50"
            onClick={() => setLightboxUrl(null)}
          >
            <span className="material-symbols-outlined text-[32px]">close</span>
          </button>
          <img
            src={lightboxUrl}
            alt="Evidence"
            className="max-w-full max-h-full object-contain rounded-lg"
            onClick={(e) => e.stopPropagation()}
          />
        </div>
      )}
    </div>
  );
}

/* ─── Helper Component ─────────────────────────────────────────── */

function InfoRow({ icon, label, value }: { icon: string; label: string; value: string }) {
  return (
    <div className="flex items-start gap-2">
      <span className="material-symbols-outlined text-[16px] text-on-surface-variant/50 mt-0.5">{icon}</span>
      <div className="flex-1">
        <p className="text-label-sm text-on-surface-variant">{label}</p>
        <p className="text-body-md text-on-surface font-medium">{value}</p>
      </div>
    </div>
  );
}
