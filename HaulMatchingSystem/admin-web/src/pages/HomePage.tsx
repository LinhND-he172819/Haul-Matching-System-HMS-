import { useState, useEffect, useCallback } from 'react';
import { fetchPublicTripPosts, type PublicTripPost } from '../api/tripPostApi';
import TripSearchBar from '../components/customer/TripSearchBar';
import TripMarketplace from '../components/customer/TripMarketplace';
import TripDetailDrawer from '../components/customer/TripDetailDrawer';
import AppHeader from '../components/AppHeader';

interface HomePageProps {
    onNavigate: (page: string) => void;
    onNewProposal?: (tripPostId: string, tripId: string, pickupMode?: string, trip?: PublicTripPost) => void;
    onLogout?: () => void;
}

export default function HomePage({ onNavigate, onNewProposal, onLogout }: HomePageProps) {
    const [role, setRole] = useState<string | null>(null);

    // Trips state
    const [trips, setTrips] = useState<PublicTripPost[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [page, setPage] = useState(1);
    const [totalPages, setTotalPages] = useState(1);
    const [totalItems, setTotalItems] = useState(0);

    // Search filters
    const [filters, setFilters] = useState({
        keyword: '',
        originHubName: '',
        destinationHubName: '',
        departureDate: '',
        originHubId: '',
        destinationHubId: '',
        departureFrom: '',
        departureTo: '',
    });

    // Detail drawer
    const [drawerTrip, setDrawerTrip] = useState<PublicTripPost | null>(null);

    // Load user role
    useEffect(() => {
        const storedRole = localStorage.getItem('role');
        if (storedRole) {
            setRole(storedRole);
        }
    }, []);

    // Fetch trips from public API
    const loadTrips = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const data = await fetchPublicTripPosts({
                page,
                pageSize: 12,
                keyword: filters.keyword || undefined,
                originHubId: filters.originHubId || undefined,
                destinationHubId: filters.destinationHubId || undefined,
                departureFrom: filters.departureFrom || undefined,
                departureTo: filters.departureTo || undefined,
            });
            setTrips(data.items);
            setTotalPages(data.totalPages);
            setTotalItems(data.totalItems);
        } catch (err: any) {
            setError(err.message ?? 'Không thể tải danh sách chuyến xe.');
            setTrips([]);
        } finally {
            setLoading(false);
        }
    }, [page, filters]);

    useEffect(() => {
        loadTrips();
    }, [loadTrips]);

    // Search handler from TripSearchBar
    const handleSearch = (f: {
        keyword: string;
        originHubId: string;
        destinationHubId: string;
        departureFrom: string;
        departureTo: string;
    }) => {
        setFilters(prev => ({
            ...prev,
            keyword: f.keyword,
            originHubId: f.originHubId,
            destinationHubId: f.destinationHubId,
            departureFrom: f.departureFrom,
            departureTo: f.departureTo,
        }));
        setPage(1);
    };

    // Proposal navigation — open CreateProposalPage with trip data
    const handleNewProposal = (trip: PublicTripPost) => {
        if (onNewProposal) {
            onNewProposal(trip.id, trip.tripId, trip.pickupMode, trip);
        }
    };

    return (
        <div className="bg-[#f2f4f7] min-h-screen font-sans flex flex-col">
            {/* ── Shared App Header ──────────────────────────────── */}
            <AppHeader
                onLogout={onLogout}
                pages={[
                    { label: 'Trang chủ', onClick: () => {}, active: true },
                    ...(role === 'Customer' ? [
                        { label: 'Tạo đơn gửi hàng', onClick: () => onNavigate('create-shipment') },
                        { label: 'Đơn hàng của tôi', onClick: () => onNavigate('my-shipments') },
                    ] : []),
                    ...(role === 'Driver' ? [
                        { label: 'Chuyến đi của tôi', onClick: () => onNavigate('driver-trips-v2') },
                    ] : []),
                ]}
            />

            {/* ── Hero Section ────────────────────────────────────── */}
            <div className="bg-primary h-[350px] relative w-full flex flex-col items-center pt-16">
                <div className="absolute inset-0 opacity-10"
                     style={{ backgroundImage: 'radial-gradient(circle, #fff 2px, transparent 2px)', backgroundSize: '24px 24px' }}>
                </div>
                <div className="relative z-10 text-center px-4">
                    <h1 className="text-4xl md:text-5xl font-bold text-white mb-4">Tìm chuyến xe phù hợp để gửi hàng</h1>
                    <p className="text-green-100 text-lg md:text-xl">Lựa chọn chuyến xe còn chỗ và gửi đề xuất ghép chuyến nhanh chóng.</p>
                </div>
            </div>

            {/* ── Main Content ─────────────────────────────────────── */}
            <main className="flex-1 w-full max-w-7xl mx-auto px-4 -mt-20 relative z-20 pb-20">
                {/* Search Panel */}
                <div className="mb-8">
                    <TripSearchBar
                        keyword={filters.keyword}
                        originHubName={filters.originHubName}
                        destinationHubName={filters.destinationHubName}
                        departureDate={filters.departureDate}
                        onSearch={handleSearch}
                    />
                </div>

                {/* Results count */}
                {!loading && !error && trips.length > 0 && (
                    <div className="mb-5">
                        <p className="text-sm text-gray-500">
                            Tìm thấy {totalItems} chuyến xe
                        </p>
                    </div>
                )}

                {/* Error state */}
                {error && (
                    <div className="bg-white rounded-2xl shadow-md border border-red-100 p-8 text-center mb-6">
                        <div className="w-16 h-16 bg-red-50 rounded-full flex items-center justify-center mx-auto mb-4">
                            <span className="material-symbols-outlined text-red-500 text-[36px]">error</span>
                        </div>
                        <p className="text-gray-700 font-medium mb-1">Đã xảy ra lỗi</p>
                        <p className="text-gray-400 text-sm mb-5">{error}</p>
                        <button
                            onClick={loadTrips}
                            className="inline-flex items-center gap-2 px-5 py-2.5 bg-primary text-white font-bold text-sm rounded-xl hover:bg-primary-container transition-colors shadow-sm"
                        >
                            <span className="material-symbols-outlined text-[18px]">refresh</span>
                            Thử lại
                        </button>
                    </div>
                )}

                {/* Trip marketplace grid */}
                {!error && (
                    <TripMarketplace
                        trips={trips}
                        loading={loading}
                        onViewDetail={setDrawerTrip}
                        onNewProposal={handleNewProposal}
                    />
                )}

                {/* Pagination */}
                {!loading && !error && totalPages > 1 && (
                    <div className="flex items-center justify-center gap-2 mt-10">
                        <button
                            onClick={() => setPage(p => Math.max(1, p - 1))}
                            disabled={page <= 1}
                            className="w-10 h-10 rounded-xl flex items-center justify-center border border-gray-200 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                        >
                            <span className="material-symbols-outlined text-[20px]">chevron_left</span>
                        </button>
                        {Array.from({ length: Math.min(totalPages, 7) }, (_, i) => {
                            const start = Math.max(1, Math.min(page - 3, totalPages - 6));
                            const p = start + i;
                            if (p > totalPages) return null;
                            return (
                                <button
                                    key={p}
                                    onClick={() => setPage(p)}
                                    className={`w-10 h-10 rounded-xl text-sm font-bold transition-colors ${
                                        p === page
                                            ? 'bg-primary text-white shadow-md'
                                            : 'border border-gray-200 hover:bg-gray-50 text-gray-600'
                                    }`}
                                >
                                    {p}
                                </button>
                            );
                        })}
                        <button
                            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                            disabled={page >= totalPages}
                            className="w-10 h-10 rounded-xl flex items-center justify-center border border-gray-200 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                        >
                            <span className="material-symbols-outlined text-[20px]">chevron_right</span>
                        </button>
                    </div>
                )}
            </main>

            {/* ── Detail Drawer ───────────────────────────────────── */}
            <TripDetailDrawer
                trip={drawerTrip}
                open={!!drawerTrip}
                onClose={() => setDrawerTrip(null)}
                onNewProposal={handleNewProposal}
            />
        </div>
    );
}
