import { useState, useEffect } from 'react';
import { decodeJWT } from '../utils/jwt';

interface HomePageProps {
    onNavigate: (page: 'login' | 'register' | 'home') => void;
    onLogout?: () => void;
}

export default function HomePage({ onNavigate: _onNavigate, onLogout }: HomePageProps) {
    const [pickup, setPickup] = useState('');
    const [dropoff, setDropoff] = useState('');
    const [date, setDate] = useState('06/06/2026');
    const [fullName, setFullName] = useState('Khách');

    useEffect(() => {
        const loadName = () => {
            const localName = localStorage.getItem('fullName');
            if (localName) {
                setFullName(localName);
                return;
            }

            const token = localStorage.getItem('accessToken');
            if (token) {
                const payload = decodeJWT(token);
                if (payload) {
                    const name = payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'];
                    if (name) {
                        setFullName(name);
                    }
                }
            }
        };

        loadName();
        window.addEventListener('profileUpdated', loadName);
        return () => window.removeEventListener('profileUpdated', loadName);
    }, []);

    return (
        <div className="bg-[#f2f4f7] min-h-screen font-sans flex flex-col">
            {/* Full Web Header */}
            <header className="bg-white shadow-sm border-b border-gray-100 px-6 xl:px-12 py-4 flex justify-between items-center z-50">
                <div className="flex items-center gap-3">
                    <div className="w-10 h-10 bg-primary rounded-lg flex items-center justify-center p-1 text-white shadow-sm">
                        <span className="material-symbols-outlined text-[20px]">local_shipping</span>
                    </div>
                    <span className="text-gray-800 font-bold text-lg hidden sm:block">Hệ thống ghép chuyến</span>
                </div>

                <nav className="hidden md:flex items-center gap-8 text-gray-500 font-medium text-sm">
                    <a href="#" className="text-primary border-b-2 border-primary pb-1">Trang chủ</a>
                    <a href="#" className="hover:text-primary transition-colors pb-1">Vé của tôi</a>
                    <a href="#" className="hover:text-primary transition-colors pb-1">Tin tức</a>
                    <a href="#" className="hover:text-primary transition-colors pb-1">Liên hệ</a>
                </nav>

                <div className="flex items-center gap-4">
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
                    <div 
                        className="flex items-center gap-2 bg-gray-50 pl-2 pr-4 py-1.5 rounded-full border border-gray-200 cursor-pointer hover:bg-gray-100 transition-colors"
                        onClick={() => _onNavigate('profile' as any)}
                        title="Hồ sơ cá nhân"
                    >
                        <div className="w-8 h-8 bg-primary text-white rounded-full flex items-center justify-center font-bold text-sm uppercase">
                            {fullName.charAt(0)}
                        </div>
                        <span className="text-gray-700 font-medium text-sm hidden sm:block">{fullName}</span>
                    </div>
                </div>
            </header>

            {/* Hero Section */}
            <div className="bg-primary h-[350px] relative w-full flex flex-col items-center pt-16">
                <div className="absolute inset-0 opacity-10" 
                     style={{ backgroundImage: 'radial-gradient(circle, #fff 2px, transparent 2px)', backgroundSize: '24px 24px' }}>
                </div>
                <div className="relative z-10 text-center px-4">
                    <h1 className="text-4xl md:text-5xl font-bold text-white mb-4">Bạn Đang Muốn Đi Đâu?</h1>
                    <p className="text-green-100 text-lg md:text-xl">Đặt vé trực tuyến nhanh chóng, tiện lợi cùng hệ thống</p>
                </div>
            </div>

            {/* Booking Search Card - Floating Web Version */}
            <main className="flex-1 w-full max-w-6xl mx-auto px-4 -mt-24 relative z-20 pb-20">
                <div className="bg-white rounded-2xl shadow-xl border border-gray-50 p-6 md:p-8">
                    
                    {/* Trip Type Selector */}
                    <div className="flex mb-6">
                        <button className="bg-primary/10 text-primary px-6 py-2.5 rounded-xl text-sm font-bold border border-transparent">
                            Một chiều
                        </button>
                        <button className="text-gray-500 hover:text-gray-700 hover:bg-gray-50 px-6 py-2.5 rounded-xl text-sm font-medium transition-colors ml-2">
                            Khứ hồi
                        </button>
                    </div>

                    {/* Web Horizontal Inputs */}
                    <div className="flex flex-col md:flex-row gap-4">
                        
                        {/* Pickup */}
                        <div className="flex-1 flex items-center gap-3 bg-gray-50 hover:bg-gray-100 transition-colors rounded-xl px-4 py-3 border border-gray-200 cursor-pointer">
                            <span className="material-symbols-outlined text-primary text-[24px]">location_on</span>
                            <div className="flex flex-col w-full">
                                <label className="text-xs text-gray-500 font-medium">Điểm đón</label>
                                <input type="text" 
                                    className="bg-transparent outline-none text-gray-800 font-semibold text-[15px] placeholder-gray-400 w-full"
                                    placeholder="Chọn điểm đón"
                                    value={pickup}
                                    onChange={(e) => setPickup(e.target.value)}
                                />
                            </div>
                        </div>

                        {/* Switch Icon (Visible on web) */}
                        <div className="hidden md:flex items-center justify-center -mx-2 z-10">
                            <div className="w-10 h-10 bg-white border border-gray-200 shadow-sm rounded-full flex items-center justify-center cursor-pointer hover:bg-gray-50 text-primary">
                                <span className="material-symbols-outlined">sync_alt</span>
                            </div>
                        </div>

                        {/* Dropoff */}
                        <div className="flex-1 flex items-center gap-3 bg-gray-50 hover:bg-gray-100 transition-colors rounded-xl px-4 py-3 border border-gray-200 cursor-pointer">
                            <span className="material-symbols-outlined text-primary text-[24px]" style={{ transform: 'rotate(-45deg)' }}>send</span>
                            <div className="flex flex-col w-full">
                                <label className="text-xs text-gray-500 font-medium">Điểm trả</label>
                                <input type="text" 
                                    className="bg-transparent outline-none text-gray-800 font-semibold text-[15px] placeholder-gray-400 w-full"
                                    placeholder="Chọn điểm trả"
                                    value={dropoff}
                                    onChange={(e) => setDropoff(e.target.value)}
                                />
                            </div>
                        </div>

                        {/* Date */}
                        <div className="flex-[0.8] flex items-center gap-3 bg-gray-50 hover:bg-gray-100 transition-colors rounded-xl px-4 py-3 border border-gray-200 cursor-pointer">
                            <span className="material-symbols-outlined text-primary text-[24px]">calendar_month</span>
                            <div className="flex flex-col w-full">
                                <label className="text-xs text-gray-500 font-medium">Ngày đi</label>
                                <input type="text" 
                                    className="bg-transparent outline-none text-gray-800 font-semibold text-[15px] placeholder-gray-400 w-full"
                                    value={date}
                                    onChange={(e) => setDate(e.target.value)}
                                />
                            </div>
                        </div>

                        {/* Search Button */}
                        <button className="md:w-auto w-full px-8 bg-primary text-white font-bold text-lg py-3 md:py-0 rounded-xl hover:bg-primary-container hover:text-on-primary-container transition-colors shadow-lg shadow-primary/30 flex items-center justify-center gap-2">
                            <span className="material-symbols-outlined">search</span>
                            <span className="md:hidden lg:inline">Tìm vé</span>
                        </button>
                    </div>
                </div>
            </main>
        </div>
    );
}
