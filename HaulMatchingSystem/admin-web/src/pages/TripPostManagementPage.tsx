import { useCallback, useEffect, useState } from 'react';
import {
    fetchTripPosts,
    fetchEligibleTrips,
    closeTripPost,
    cancelTripPost,
    fetchTripPostKpi,
    type TripPostListItem,
    type EligibleTrip,
    type TripPostKpi,
    type TripPostStatus,
} from '../api/tripPostApi';
import TripPostKpiCards from '../components/trip-posts/TripPostKpiCards';
import TripPostFilters from '../components/trip-posts/TripPostFilters';
import TripPostTable from '../components/trip-posts/TripPostTable';
import EligibleTripSelector from '../components/trip-posts/EligibleTripSelector';
import CreateTripPostForm from '../components/trip-posts/CreateTripPostForm';
import TripPostDetailDrawer from '../components/trip-posts/TripPostDetailDrawer';
import EditTripPostModal from '../components/trip-posts/EditTripPostModal';

interface TripPostManagementPageProps {
    sidebar?: React.ReactNode;
}

interface Filters {
    keyword: string;
    status: TripPostStatus | '';
    hubId: string;
    fromDate: string;
    toDate: string;
}

export default function TripPostManagementPage({ sidebar }: TripPostManagementPageProps) {
    // KPI
    const [kpi, setKpi] = useState<TripPostKpi | null>(null);
    const [kpiLoading, setKpiLoading] = useState(false);

    // List
    const [posts, setPosts] = useState<TripPostListItem[]>([]);
    const [loading, setLoading] = useState(false);
    const [page, setPage] = useState(1);
    const [totalPages, setTotalPages] = useState(1);
    const [totalRecords, setTotalRecords] = useState(0);
    const [filters, setFilters] = useState<Filters>({ keyword: '', status: '', hubId: '', fromDate: '', toDate: '' });

    // Create panel
    const [showCreatePanel, setShowCreatePanel] = useState(false);
    const [eligibleTrips, setEligibleTrips] = useState<EligibleTrip[]>([]);
    const [eligibleTripsLoading, setEligibleTripsLoading] = useState(false);
    const [selectedTrip, setSelectedTrip] = useState<EligibleTrip | null>(null);

    // Detail drawer
    const [detailPostId, setDetailPostId] = useState<string | null>(null);

    // Edit modal
    const [editPost, setEditPost] = useState<TripPostListItem | null>(null);

    // Confirm dialogs
    const [confirmAction, setConfirmAction] = useState<{ type: 'close' | 'cancel'; post: TripPostListItem } | null>(null);

    // Toast
    const [toast, setToast] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

    const showToast = useCallback((type: 'success' | 'error', message: string) => {
        setToast({ type, message });
        setTimeout(() => setToast(null), 4000);
    }, []);

    // Load KPI
    const loadKpi = useCallback(async () => {
        setKpiLoading(true);
        try {
            const data = await fetchTripPostKpi();
            setKpi(data);
        } catch { /* ignore */ } finally {
            setKpiLoading(false);
        }
    }, []);

    // Load posts
    const loadPosts = useCallback(async () => {
        setLoading(true);
        try {
            const data = await fetchTripPosts({
                page,
                pageSize: 10,
                keyword: filters.keyword || undefined,
                status: filters.status || undefined,
                hubId: filters.hubId || undefined,
                fromDate: filters.fromDate ? new Date(filters.fromDate).toISOString() : undefined,
                toDate: filters.toDate ? new Date(filters.toDate + 'T23:59:59').toISOString() : undefined,
            });
            setPosts(data.items);
            setTotalPages(data.totalPages);
            setTotalRecords(data.totalItems);
        } catch { /* ignore */ } finally {
            setLoading(false);
        }
    }, [page, filters]);

    useEffect(() => { loadKpi(); }, [loadKpi]);
    useEffect(() => { loadPosts(); }, [loadPosts]);

    // Load eligible trips when create panel opens
    useEffect(() => {
        if (!showCreatePanel) return;
        setEligibleTripsLoading(true);
        setSelectedTrip(null);
        fetchEligibleTrips()
            .then(setEligibleTrips)
            .catch(() => setEligibleTrips([]))
            .finally(() => setEligibleTripsLoading(false));
    }, [showCreatePanel]);

    // Filter change
    const handleFilterChange = (f: Filters) => {
        setFilters(f);
        setPage(1);
    };

    // CRUD actions
    const handleConfirmClose = async () => {
        if (!confirmAction) return;
        try {
            await closeTripPost(confirmAction.post.id);
            showToast('success', 'Đã đóng bài đăng thành công.');
            setConfirmAction(null);
            loadPosts();
            loadKpi();
        } catch (err) {
            showToast('error', err instanceof Error ? err.message : 'Đóng bài thất bại.');
        }
    };

    const handleConfirmCancel = async () => {
        if (!confirmAction) return;
        try {
            await cancelTripPost(confirmAction.post.id);
            showToast('success', 'Đã hủy bài đăng thành công.');
            setConfirmAction(null);
            loadPosts();
            loadKpi();
        } catch (err) {
            showToast('error', err instanceof Error ? err.message : 'Hủy bài thất bại.');
        }
    };

    const handleCreateSuccess = (message: string) => {
        showToast('success', message);
        setShowCreatePanel(false);
        loadPosts();
        loadKpi();
    };

    const handleEditSuccess = (message: string) => {
        showToast('success', message);
        setEditPost(null);
        loadPosts();
    };

    return (
        <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
            {sidebar}
            <div className="flex-1 flex flex-col xl:ml-64 w-full overflow-y-auto">
                {/* Page Header */}
                <div className="px-6 lg:px-8 pt-6 pb-4 flex items-center justify-between">
                    <div>
                        <h1 className="text-headline-lg font-headline-lg text-on-surface">Đăng bài chuyến xe còn chỗ</h1>
                        <p className="text-body-sm text-on-surface-variant mt-1">Quản lý bài đăng chuyến xe cho đối tác/nhà xe</p>
                    </div>
                    <button
                        onClick={() => setShowCreatePanel(!showCreatePanel)}
                        className={`flex items-center gap-2 px-5 py-2.5 rounded-xl text-label-lg font-bold transition-all ${
                            showCreatePanel
                                ? 'bg-surface-variant text-on-surface-variant border border-outline-variant'
                                : 'bg-primary hover:bg-primary/90 text-on-primary shadow-md hover:shadow-lg'
                        }`}
                    >
                        <span className="material-symbols-outlined text-[20px]">
                            {showCreatePanel ? 'close' : 'add'}
                        </span>
                        {showCreatePanel ? 'Đóng form' : 'Đăng bài mới'}
                    </button>
                </div>

                <div className="px-6 lg:px-8 pb-8 space-y-5">
                    {/* Create Panel */}
                    {showCreatePanel && (
                        <div className="rounded-2xl border border-outline-variant/40 bg-surface-container-low p-6 space-y-5">
                            <div className="flex items-center gap-2 mb-1">
                                <span className="material-symbols-outlined text-primary text-[24px]">add_circle</span>
                                <h2 className="text-title-lg font-bold text-on-surface">Đăng bài mới</h2>
                            </div>
                            <EligibleTripSelector
                                trips={eligibleTrips}
                                selectedTripId={selectedTrip?.tripId ?? ''}
                                onSelect={setSelectedTrip}
                                loading={eligibleTripsLoading}
                            />
                            <CreateTripPostForm
                                selectedTrip={selectedTrip}
                                onSuccess={handleCreateSuccess}
                                onError={(msg) => showToast('error', msg)}
                            />
                        </div>
                    )}

                    {/* KPI Cards */}
                    <TripPostKpiCards kpi={kpi} loading={kpiLoading} />

                    {/* Filters */}
                    <TripPostFilters filters={filters} onFilterChange={handleFilterChange} loading={loading} />

                    {/* Table + Pagination */}
                    <TripPostTable
                        posts={posts}
                        loading={loading}
                        onViewDetail={setDetailPostId}
                        onEdit={setEditPost}
                        onClose={(post) => setConfirmAction({ type: 'close', post })}
                        onCancel={(post) => setConfirmAction({ type: 'cancel', post })}
                    />

                    {/* Pagination */}
                    {totalPages > 1 && (
                        <div className="flex items-center justify-between">
                            <p className="text-body-sm text-on-surface-variant">
                                Trang {page}/{totalPages} • {totalRecords} bài đăng
                            </p>
                            <div className="flex items-center gap-2">
                                <button
                                    onClick={() => setPage(p => Math.max(1, p - 1))}
                                    disabled={page <= 1}
                                    className="p-2 rounded-lg border border-outline-variant hover:bg-surface-variant/50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                                >
                                    <span className="material-symbols-outlined text-[18px]">chevron_left</span>
                                </button>
                                {Array.from({ length: Math.min(totalPages, 5) }, (_, i) => {
                                    const start = Math.max(1, Math.min(page - 2, totalPages - 4));
                                    const p = start + i;
                                    if (p > totalPages) return null;
                                    return (
                                        <button
                                            key={p}
                                            onClick={() => setPage(p)}
                                            className={`w-9 h-9 rounded-lg text-label-sm font-bold transition-colors ${
                                                p === page
                                                    ? 'bg-primary text-on-primary'
                                                    : 'border border-outline-variant hover:bg-surface-variant/50 text-on-surface'
                                            }`}
                                        >
                                            {p}
                                        </button>
                                    );
                                })}
                                <button
                                    onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                                    disabled={page >= totalPages}
                                    className="p-2 rounded-lg border border-outline-variant hover:bg-surface-variant/50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                                >
                                    <span className="material-symbols-outlined text-[18px]">chevron_right</span>
                                </button>
                            </div>
                        </div>
                    )}
                </div>
            </div>

            {/* Detail Drawer */}
            <TripPostDetailDrawer postId={detailPostId} onClose={() => setDetailPostId(null)} />

            {/* Edit Modal */}
            <EditTripPostModal
                post={editPost}
                onClose={() => setEditPost(null)}
                onSuccess={handleEditSuccess}
                onError={(msg) => showToast('error', msg)}
            />

            {/* Confirm Dialog */}
            {confirmAction && (
                <>
                    <div className="fixed inset-0 z-[60] bg-black/40" onClick={() => setConfirmAction(null)} />
                    <div className="fixed inset-0 z-[61] flex items-center justify-center p-4">
                        <div className="bg-surface-container-lowest rounded-2xl shadow-xl w-full max-w-sm p-6 text-center">
                            <div className={`w-14 h-14 rounded-full flex items-center justify-center mx-auto mb-4 ${
                                confirmAction.type === 'cancel' ? 'bg-red-100' : 'bg-amber-100'
                            }`}>
                                <span className={`material-symbols-outlined text-[28px] ${
                                    confirmAction.type === 'cancel' ? 'text-red-500' : 'text-amber-600'
                                }`}>
                                    {confirmAction.type === 'cancel' ? 'cancel' : 'lock'}
                                </span>
                            </div>
                            <h3 className="text-title-lg font-bold text-on-surface mb-2">
                                {confirmAction.type === 'cancel' ? 'Hủy bài đăng?' : 'Đóng bài đăng?'}
                            </h3>
                            <p className="text-body-sm text-on-surface-variant mb-1">
                                {confirmAction.type === 'cancel'
                                    ? 'Bài đăng này sẽ bị hủy và không thể khôi phục.'
                                    : 'Bài đăng này sẽ được đóng và không hiển thị cho đối tác nữa.'}
                            </p>
                            <p className="text-label-sm text-on-surface-variant/70 mb-6 italic line-clamp-2">
                                "{confirmAction.post.title}"
                            </p>
                            <div className="flex gap-3">
                                <button
                                    onClick={() => setConfirmAction(null)}
                                    className="flex-1 px-4 py-2.5 rounded-xl border border-outline-variant text-on-surface-variant text-label-lg font-bold hover:bg-surface-variant/50 transition-colors"
                                >
                                    Hủy
                                </button>
                                <button
                                    onClick={confirmAction.type === 'cancel' ? handleConfirmCancel : handleConfirmClose}
                                    className={`flex-1 px-4 py-2.5 rounded-xl text-label-lg font-bold text-white transition-colors ${
                                        confirmAction.type === 'cancel'
                                            ? 'bg-red-500 hover:bg-red-600'
                                            : 'bg-amber-500 hover:bg-amber-600'
                                    }`}
                                >
                                    {confirmAction.type === 'cancel' ? 'Xác nhận hủy' : 'Xác nhận đóng'}
                                </button>
                            </div>
                        </div>
                    </div>
                </>
            )}

            {/* Toast */}
            {toast && (
                <div className="fixed top-4 right-4 z-[70] animate-in slide-in-from-right-4 fade-in duration-200">
                    <div className={`flex items-center gap-3 px-5 py-3 rounded-xl shadow-lg border ${
                        toast.type === 'success'
                            ? 'bg-emerald-50 border-emerald-200 text-emerald-800'
                            : 'bg-red-50 border-red-200 text-red-800'
                    }`}>
                        <span className="material-symbols-outlined text-[20px]">
                            {toast.type === 'success' ? 'check_circle' : 'error'}
                        </span>
                        <p className="text-body-sm font-medium">{toast.message}</p>
                        <button onClick={() => setToast(null)} className="ml-2 opacity-60 hover:opacity-100">
                            <span className="material-symbols-outlined text-[16px]">close</span>
                        </button>
                    </div>
                </div>
            )}
        </div>
    );
}
