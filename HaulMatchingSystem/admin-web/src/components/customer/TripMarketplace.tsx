import type { PublicTripPost } from '../../api/tripPostApi';
import TripCard from './TripCard';

interface TripMarketplaceProps {
    trips: PublicTripPost[];
    loading: boolean;
    onViewDetail: (trip: PublicTripPost) => void;
    onNewProposal: (trip: PublicTripPost) => void;
}

/* ------------------------------------------------------------------ */
/*  Skeleton Card                                                      */
/* ------------------------------------------------------------------ */
function SkeletonCard() {
    return (
        <div className="bg-white rounded-2xl shadow-md border border-gray-100 overflow-hidden animate-pulse">
            <div className="px-5 pt-5 pb-3">
                <div className="flex items-start justify-between mb-3">
                    <div className="h-5 bg-gray-200 rounded-lg w-3/4" />
                    <div className="h-5 bg-gray-200 rounded-full w-20 shrink-0 ml-2" />
                </div>
                <div className="flex items-center gap-2 mb-3">
                    <div className="h-4 bg-gray-200 rounded w-4" />
                    <div className="h-4 bg-gray-200 rounded w-20" />
                    <div className="h-4 bg-gray-200 rounded w-4" />
                    <div className="h-4 bg-gray-200 rounded w-20" />
                </div>
                <div className="grid grid-cols-2 gap-x-4 gap-y-2">
                    {[1, 2, 3, 4].map((i) => (
                        <div key={i} className="h-4 bg-gray-200 rounded w-full" />
                    ))}
                </div>
            </div>
            <div className="px-5 pb-4 pt-2 space-y-2">
                <div className="h-1.5 bg-gray-200 rounded-full w-full" />
                <div className="h-1.5 bg-gray-200 rounded-full w-full" />
            </div>
            <div className="px-5 pb-5 flex gap-3">
                <div className="h-10 bg-gray-200 rounded-xl flex-1" />
                <div className="h-10 bg-gray-200 rounded-xl flex-1" />
            </div>
        </div>
    );
}

/* ------------------------------------------------------------------ */
/*  Empty State                                                        */
/* ------------------------------------------------------------------ */
function EmptyState() {
    return (
        <div className="flex flex-col items-center justify-center py-20">
            <div className="w-20 h-20 bg-gray-100 rounded-full flex items-center justify-center mb-5">
                <span className="material-symbols-outlined text-gray-300 text-[48px]">local_shipping</span>
            </div>
            <p className="text-gray-500 font-medium text-lg mb-1">
                Hiện chưa có chuyến xe nào đang nhận hàng.
            </p>
            <p className="text-gray-400 text-sm">
                Hãy quay lại sau hoặc thử tìm kiếm với từ khóa khác.
            </p>
        </div>
    );
}

/* ------------------------------------------------------------------ */
/*  Marketplace Grid                                                   */
/* ------------------------------------------------------------------ */
export default function TripMarketplace({ trips, loading, onViewDetail, onNewProposal }: TripMarketplaceProps) {
    if (loading) {
        return (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5">
                {Array.from({ length: 6 }).map((_, i) => (
                    <SkeletonCard key={i} />
                ))}
            </div>
        );
    }

    if (trips.length === 0) {
        return <EmptyState />;
    }

    return (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5">
            {trips.map((trip) => (
                <TripCard
                    key={trip.id}
                    trip={trip}
                    onViewDetail={onViewDetail}
                    onNewProposal={onNewProposal}
                />
            ))}
        </div>
    );
}
