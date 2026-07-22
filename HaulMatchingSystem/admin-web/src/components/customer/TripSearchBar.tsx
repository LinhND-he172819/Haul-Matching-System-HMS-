import { useState, useEffect } from 'react';
import { fetchHubs, type Hub } from '../../api/tripPostApi';

interface TripSearchBarProps {
    keyword: string;
    originHubName: string;
    destinationHubName: string;
    departureDate: string;
    onSearch: (filters: {
        keyword: string;
        originHubId: string;
        destinationHubId: string;
        departureFrom: string;
        departureTo: string;
    }) => void;
}

export default function TripSearchBar({
    keyword: initKeyword,
    originHubName: initOrigin,
    destinationHubName: initDest,
    departureDate: initDate,
    onSearch,
}: TripSearchBarProps) {
    const [keyword, setKeyword] = useState(initKeyword);
    const [originHubId, setOriginHubId] = useState('');
    const [destinationHubId, setDestinationHubId] = useState('');
    const [departureDate, setDepartureDate] = useState(initDate);
    const [hubs, setHubs] = useState<Hub[]>([]);

    useEffect(() => {
        fetchHubs()
            .then(setHubs)
            .catch(() => setHubs([]));
    }, []);

    const handleSearch = () => {
        onSearch({
            keyword: keyword.trim(),
            originHubId,
            destinationHubId,
            departureFrom: departureDate ? `${departureDate}T00:00:00Z` : '',
            departureTo: departureDate ? `${departureDate}T23:59:59Z` : '',
        });
    };

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (e.key === 'Enter') handleSearch();
    };

    const handleReset = () => {
        setKeyword('');
        setOriginHubId('');
        setDestinationHubId('');
        setDepartureDate('');
        onSearch({
            keyword: '',
            originHubId: '',
            destinationHubId: '',
            departureFrom: '',
            departureTo: '',
        });
    };

    return (
        <div className="bg-white rounded-2xl shadow-md border border-gray-100 p-5 md:p-6">
            {/* Row 1: Keyword */}
            <div className="mb-4">
                <div className="flex items-center gap-3 bg-gray-50 hover:bg-gray-100 transition-colors rounded-xl px-4 py-3 border border-gray-200">
                    <span className="material-symbols-outlined text-primary text-[22px]">search</span>
                    <div className="flex flex-col w-full">
                        <label className="text-xs text-gray-500 font-medium">Tìm kiếm</label>
                        <input
                            type="text"
                            className="bg-transparent outline-none text-gray-800 font-medium text-[15px] placeholder-gray-400 w-full"
                            placeholder="Tìm theo tài xế, biển số, tuyến đường..."
                            value={keyword}
                            onChange={(e) => setKeyword(e.target.value)}
                            onKeyDown={handleKeyDown}
                        />
                    </div>
                </div>
            </div>

            {/* Row 2: Hub filters + Date */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-4">
                {/* Hub đi */}
                <div className="flex items-center gap-3 bg-gray-50 hover:bg-gray-100 transition-colors rounded-xl px-4 py-3 border border-gray-200">
                    <span className="material-symbols-outlined text-primary text-[22px]">location_on</span>
                    <div className="flex flex-col w-full">
                        <label className="text-xs text-gray-500 font-medium">Hub đi</label>
                        <select
                            className="bg-transparent outline-none text-gray-800 font-medium text-[15px] w-full cursor-pointer"
                            value={originHubId}
                            onChange={(e) => setOriginHubId(e.target.value)}
                        >
                            <option value="">Tất cả</option>
                            {hubs.map((hub) => (
                                <option key={hub.id} value={hub.id}>{hub.name}</option>
                            ))}
                        </select>
                    </div>
                </div>

                {/* Hub đến */}
                <div className="flex items-center gap-3 bg-gray-50 hover:bg-gray-100 transition-colors rounded-xl px-4 py-3 border border-gray-200">
                    <span className="material-symbols-outlined text-primary text-[22px]" style={{ transform: 'rotate(-45deg)' }}>send</span>
                    <div className="flex flex-col w-full">
                        <label className="text-xs text-gray-500 font-medium">Hub đến</label>
                        <select
                            className="bg-transparent outline-none text-gray-800 font-medium text-[15px] w-full cursor-pointer"
                            value={destinationHubId}
                            onChange={(e) => setDestinationHubId(e.target.value)}
                        >
                            <option value="">Tất cả</option>
                            {hubs.map((hub) => (
                                <option key={hub.id} value={hub.id}>{hub.name}</option>
                            ))}
                        </select>
                    </div>
                </div>

                {/* Ngày khởi hành */}
                <div className="flex items-center gap-3 bg-gray-50 hover:bg-gray-100 transition-colors rounded-xl px-4 py-3 border border-gray-200">
                    <span className="material-symbols-outlined text-primary text-[22px]">calendar_month</span>
                    <div className="flex flex-col w-full">
                        <label className="text-xs text-gray-500 font-medium">Ngày khởi hành</label>
                        <input
                            type="date"
                            className="bg-transparent outline-none text-gray-800 font-medium text-[15px] w-full cursor-pointer"
                            value={departureDate}
                            onChange={(e) => setDepartureDate(e.target.value)}
                        />
                    </div>
                </div>
            </div>

            {/* Action buttons */}
            <div className="flex items-center gap-3">
                <button
                    onClick={handleSearch}
                    className="flex items-center justify-center gap-2 px-6 py-2.5 bg-primary text-white font-bold text-sm rounded-xl hover:bg-primary-container transition-colors shadow-sm"
                >
                    <span className="material-symbols-outlined text-[18px]">search</span>
                    Tìm kiếm
                </button>
                <button
                    onClick={handleReset}
                    className="flex items-center justify-center gap-2 px-5 py-2.5 border border-gray-200 text-gray-600 font-medium text-sm rounded-xl hover:bg-gray-50 transition-colors"
                >
                    <span className="material-symbols-outlined text-[18px]">refresh</span>
                    Đặt lại
                </button>
            </div>
        </div>
    );
}
