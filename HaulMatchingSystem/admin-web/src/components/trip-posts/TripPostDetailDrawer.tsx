import { useEffect, useState } from 'react';
import { fetchTripPostDetail, type TripPostDetail } from '../../api/tripPostApi';
import StatusBadge from './StatusBadge';

interface Props {
    postId: string | null;
    onClose: () => void;
}

export default function TripPostDetailDrawer({ postId, onClose }: Props) {
    const [detail, setDetail] = useState<TripPostDetail | null>(null);
    const [loading, setLoading] = useState(false);

    useEffect(() => {
        if (!postId) { setDetail(null); return; }
        setLoading(true);
        fetchTripPostDetail(postId)
            .then(setDetail)
            .catch(() => {})
            .finally(() => setLoading(false));
    }, [postId]);

    if (!postId) return null;

    const d = detail;

    return (
        <>
            {/* Backdrop */}
            <div className="fixed inset-0 z-40 bg-black/30" onClick={onClose} />
            {/* Drawer */}
            <div className="fixed inset-y-0 right-0 z-50 w-full max-w-lg bg-surface-container-lowest shadow-2xl flex flex-col">
                {/* Header */}
                <div className="flex items-center justify-between px-6 py-4 border-b border-outline-variant/30">
                    <h2 className="text-title-lg font-bold text-on-surface">Chi tiết bài đăng</h2>
                    <button onClick={onClose} className="p-2 rounded-full hover:bg-surface-variant/50 transition-colors">
                        <span className="material-symbols-outlined text-on-surface-variant">close</span>
                    </button>
                </div>

                {/* Body */}
                <div className="flex-1 overflow-y-auto p-6 space-y-5">
                    {loading ? (
                        <div className="space-y-4">
                            {[1, 2, 3, 4].map(i => <div key={i} className="h-16 bg-surface-variant/30 rounded-lg animate-pulse" />)}
                        </div>
                    ) : !d ? (
                        <p className="text-body-md text-on-surface-variant text-center py-8">Không tìm thấy chi tiết.</p>
                    ) : (
                        <>
                            {/* Status & title */}
                            <div>
                                <div className="flex items-center gap-2 mb-2">
                                    <StatusBadge status={d.status} />
                                    <span className="text-label-sm text-on-surface-variant">#{d.id.slice(0, 8)}…</span>
                                </div>
                                <h3 className="text-body-lg font-semibold text-on-surface">{d.title}</h3>
                            </div>

                            {/* Description */}
                            {d.description && (
                                <Section label="Mô tả">
                                    <p className="text-body-md text-on-surface whitespace-pre-wrap">{d.description}</p>
                                </Section>
                            )}

                            {/* Schedule */}
                            <Section label="Thời gian">
                                <div className="space-y-2">
                                    <InfoRow icon="calendar_today" label="Đăng lúc" value={formatDate(d.publishedAt)} />
                                    <InfoRow icon="schedule" label="Hạn nhận" value={formatDate(d.acceptUntil)} />
                                    <InfoRow icon="edit_calendar" label="Tạo lúc" value={formatDate(d.createdAt)} />
                                    {d.closedAt && <InfoRow icon="lock" label="Đóng lúc" value={formatDate(d.closedAt)} />}
                                </div>
                            </Section>

                            {/* Route */}
                            <Section label="Tuyến đường">
                                <div className="flex items-center gap-3">
                                    <div className="flex-1 rounded-lg bg-emerald-50 p-3 text-center">
                                        <span className="material-symbols-outlined text-emerald-500 text-[20px]">home</span>
                                        <p className="text-body-sm font-medium text-emerald-700 mt-1">{d.originHubName}</p>
                                    </div>
                                    <span className="material-symbols-outlined text-on-surface-variant">arrow_forward</span>
                                    <div className="flex-1 rounded-lg bg-blue-50 p-3 text-center">
                                        <span className="material-symbols-outlined text-blue-500 text-[20px]">flag</span>
                                        <p className="text-body-sm font-medium text-blue-700 mt-1">{d.destinationHubName}</p>
                                    </div>
                                </div>
                            </Section>

                            {/* Driver & Vehicle */}
                            <Section label="Tài xế & Xe">
                                <div className="space-y-2">
                                    <InfoRow icon="person" label="Tài xế" value={d.driverName} />
                                    {d.licensePlate && <InfoRow icon="local_shipping" label="Xe" value={`${d.licensePlate} • ${d.truckType}`} />}
                                </div>
                            </Section>

                            {/* Capacity */}
                            <Section label="Sức chứa còn lại">
                                <div className="grid grid-cols-2 gap-4">
                                    <div className="rounded-lg bg-primary/5 p-3 text-center border border-primary/10">
                                        <p className="text-headline-sm font-bold text-primary">
                                            {d.remainingWeightKg.toLocaleString()}
                                        </p>
                                        <p className="text-label-sm text-on-surface-variant">kg còn lại</p>
                                    </div>
                                    <div className="rounded-lg bg-secondary/5 p-3 text-center border border-secondary/10">
                                        <p className="text-headline-sm font-bold text-secondary">
                                            {d.remainingVolumeCbm.toLocaleString()}
                                        </p>
                                        <p className="text-label-sm text-on-surface-variant">CBM còn lại</p>
                                    </div>
                                </div>
                            </Section>
                        </>
                    )}
                </div>

                {/* Footer */}
                <div className="px-6 py-3 border-t border-outline-variant/30 bg-surface-container-low">
                    <button
                        onClick={onClose}
                        className="w-full px-4 py-2 rounded-lg bg-surface-variant hover:bg-surface-variant/80 text-on-surface text-label-lg font-bold transition-colors"
                    >
                        Đóng
                    </button>
                </div>
            </div>
        </>
    );
}

function Section({ label, children }: { label: string; children: React.ReactNode }) {
    return (
        <div className="rounded-xl border border-outline-variant/30 bg-surface-container-low p-4">
            <h4 className="text-label-lg font-bold text-on-surface mb-2">{label}</h4>
            {children}
        </div>
    );
}

function InfoRow({ icon, label, value }: { icon: string; label: string; value: string }) {
    return (
        <div className="flex items-center gap-2">
            <span className="material-symbols-outlined text-on-surface-variant/50 text-[16px]">{icon}</span>
            <span className="text-label-sm text-on-surface-variant w-24">{label}</span>
            <span className="text-body-sm text-on-surface font-medium">{value}</span>
        </div>
    );
}

function formatDate(iso: string | null) {
    if (!iso) return '—';
    return new Date(iso).toLocaleString('vi-VN');
}
