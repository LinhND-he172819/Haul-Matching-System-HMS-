import { useState, useEffect, useCallback } from 'react';
import { decodeJWT } from '../utils/jwt';
import { fetchPublicTripPosts, type PublicTripPost } from '../api/tripPostApi';
import TripSearchBar from '../components/customer/TripSearchBar';
import TripMarketplace from '../components/customer/TripMarketplace';
import TripDetailDrawer from '../components/customer/TripDetailDrawer';

interface HomePageProps {
    onNavigate: (page: 'login' | 'register' | 'home' | 'create-shipment') => void;
    onNewProposal?: (tripPostId: string, tripId: string, pickupMode?: string) => void;
    onLogout?: () => void;
}

export default function HomePage({ onNavigate, onNewProposal, onLogout }: HomePageProps) {
    const [fullName, setFullName] = useState('Khách');

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

    // Load user name
    useEffect(() => {
        const token = localStorage.getItem('accessToken');
        if (token) {
            const payload = decodeJWT(token);
            if (payload) {
                const name = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'];
                if (name) setFullName(name);
            }
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

    // Proposal navigation — open DraftShipment in Proposal Mode
    const handleNewProposal = (trip: PublicTripPost) => {
        if (onNewProposal) {
            onNewProposal(trip.id, trip.id, trip.pickupMode);
        }
    };

    return (
        <div className="bg-[#f2f4f7] min-h-screen font-sans flex flex-col">
            {/* ── Full Web Header ─────────────────────────────────── */}
            <header className="bg-white shadow-sm border-b border-gray-100 px-6 xl:px-12 py-4 flex justify-between items-center z-50">
                <div className="flex items-center gap-3">
                    <div className="w-10 h-10 bg-primary rounded-lg flex items-center justify-center p-1 text-white shadow-sm">
                        <span className="material-symbols-outlined text-[20px]">local_shipping</span>
                    </div>
                    <span className="text-gray-800 font-bold text-lg hidden sm:block">Hệ thống ghép chuyến</span>
                </div>

                <nav className="hidden md:flex items-center gap-8 text-gray-500 font-medium text-sm">
                    <a href="#" className="text-primary border-b-2 border-primary pb-1">Trang chủ</a>
                    <button
                        onClick={() => onNavigate('create-shipment')}
                        className="hover:text-primary transition-colors pb-1"
                    >
                        Tạo đơn gửi hàng
                    </button>
                </nav>

                <div className="flex items-center gap-4">
                    {/* Tạo đơn gửi hàng – mobile */}
                    <button
                        onClick={() => onNavigate('create-shipment')}
                        className="md:hidden flex items-center gap-1.5 px-3 py-2 bg-primary/10 text-primary text-xs font-bold rounded-lg hover:bg-primary/20 transition-colors"
                    >
                        <span className="material-symbols-outlined text-[16px]">add</span>
                        Tạo đơn
                    </button>

                    <button className="w-10 h-10 rounded-full flex items-center justify-center hover:bg-gray-50 text-gray-600 transition-colors">
                        <span className="material-symbols-outlined text-[24px]">notifications</span>
                    </button>
                    {onLogout && (
                        <button
                            onClick={onLogout}
                            className="w-10 h-10 rounded-full flex items-center justify-center hover:bg-gray-50 text-red-600 hover:text-red-800 transition-colors"
                            title="Đăng xuất"
                        >
                            <span className="material-symbols-outlined text-[24px]">logout</span>
                        </button>
                    )}
                    <div className="flex items-center gap-2 bg-gray-50 pl-2 pr-4 py-1.5 rounded-full border border-gray-200 cursor-pointer">
                        <div className="w-8 h-8 bg-primary text-white rounded-full flex items-center justify-center font-bold text-sm">
                            {fullName.charAt(0).toUpperCase()}
                        </div>
                        <span className="text-gray-700 font-medium text-sm hidden sm:block">{fullName}</span>
                    </div>
                </div>
            </header>

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
