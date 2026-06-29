import React, { useState, useEffect } from 'react';
import { fetchUserById, updateUser } from '../api/identityApi';

interface ProfilePageProps {
    onNavigate: (page: 'login' | 'register' | 'home' | 'create-shipment' | 'profile') => void;
    onLogout?: () => void;
}

export default function ProfilePage({ onNavigate, onLogout }: ProfilePageProps) {
    const [fullName, setFullName] = useState('');
    const [phone, setPhone] = useState('');
    const [email, setEmail] = useState('');
    const [role, setRole] = useState('');
    const [loading, setLoading] = useState(true);
    const [submitting, setSubmitting] = useState(false);
    const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

    const showToast = (message: string, type: 'success' | 'error' = 'success') => {
        setToast({ message, type });
        setTimeout(() => setToast(null), 3000);
    };

    const loadProfile = async () => {
        const userId = localStorage.getItem('userId');
        if (!userId) {
            onNavigate('login');
            return;
        }

        try {
            setLoading(true);
            const userData = await fetchUserById(userId);
            setFullName(userData.fullName);
            setPhone(userData.phone || '');
            setEmail(userData.email || '');
            setRole(userData.role);
        } catch (error: any) {
            console.error('Lỗi khi tải thông tin hồ sơ:', error);
            showToast(error.message || 'Lỗi khi tải thông tin cá nhân', 'error');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        loadProfile();
    }, []);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        const userId = localStorage.getItem('userId');
        if (!userId) return;

        if (!fullName || !phone) {
            showToast('Họ tên và Số điện thoại là bắt buộc!', 'error');
            return;
        }

        try {
            setSubmitting(true);
            await updateUser(userId, {
                fullName,
                phone,
                email: email || '', // Email is optional
                role: role // Keep the existing role
            });
            
            showToast('Cập nhật hồ sơ thành công!', 'success');
            
            // Update local storage so Header can reflect immediately
            localStorage.setItem('fullName', fullName);
            // Optionally dispatch a custom event to notify other components (like HomePage Header)
            window.dispatchEvent(new Event('profileUpdated'));
            
        } catch (error: any) {
            showToast(error.message || 'Cập nhật thất bại.', 'error');
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div className="bg-[#f2f4f7] min-h-screen font-sans flex flex-col">
            {/* Header */}
            <header className="bg-white shadow-sm border-b border-gray-100 px-6 xl:px-12 py-4 flex justify-between items-center z-50">
                <div 
                    className="flex items-center gap-3 cursor-pointer"
                    onClick={() => onNavigate('home')}
                >
                    <div className="w-10 h-10 bg-primary rounded-lg flex items-center justify-center p-1 text-white shadow-sm">
                        <span className="material-symbols-outlined text-[20px]">local_shipping</span>
                    </div>
                    <span className="text-gray-800 font-bold text-lg hidden sm:block">Hệ thống ghép chuyến</span>
                </div>

                <nav className="hidden md:flex items-center gap-8 text-gray-500 font-medium text-sm">
                    <button onClick={() => onNavigate('home')} className="hover:text-primary transition-colors pb-1">Trang chủ</button>
                    <button className="text-primary border-b-2 border-primary pb-1">Hồ sơ của tôi</button>
                </nav>

                <div className="flex items-center gap-4">
                    {onLogout && (
                        <button
                            onClick={onLogout}
                            className="w-10 h-10 rounded-full flex items-center justify-center hover:bg-gray-50 text-red-600 hover:text-red-800 transition-colors"
                            title="Đăng xuất"
                        >
                            <span className="material-symbols-outlined text-[24px]">logout</span>
                        </button>
                    )}
                    <div className="flex items-center gap-2 bg-gray-50 pl-2 pr-4 py-1.5 rounded-full border border-gray-200">
                        <div className="w-8 h-8 bg-primary text-white rounded-full flex items-center justify-center font-bold text-sm uppercase">
                            {localStorage.getItem('fullName')?.charAt(0) || fullName?.charAt(0) || 'U'}
                        </div>
                        <span className="text-gray-700 font-medium text-sm hidden sm:block">
                            {localStorage.getItem('fullName') || fullName || 'User'}
                        </span>
                    </div>
                </div>
            </header>

            {/* Main Content */}
            <main className="flex-1 w-full max-w-3xl mx-auto px-4 py-10 relative z-20">
                <div className="bg-white rounded-2xl shadow-xl border border-gray-50 p-6 md:p-10">
                    <div className="flex items-center gap-3 mb-8 pb-4 border-b border-gray-100">
                        <div className="w-12 h-12 bg-primary/10 text-primary rounded-full flex items-center justify-center">
                            <span className="material-symbols-outlined text-[28px]">person</span>
                        </div>
                        <div>
                            <h1 className="text-2xl font-bold text-gray-800">Thông tin tài khoản</h1>
                            <p className="text-sm text-gray-500">Cập nhật hồ sơ cá nhân của bạn</p>
                        </div>
                    </div>

                    {toast && (
                        <div className={`p-4 mb-6 rounded-xl border flex items-center gap-3 ${toast.type === 'success' ? 'bg-green-50 border-green-200 text-green-700' : 'bg-red-50 border-red-200 text-red-700'}`}>
                            <span className="material-symbols-outlined">
                                {toast.type === 'success' ? 'check_circle' : 'error'}
                            </span>
                            <p className="font-medium text-sm">{toast.message}</p>
                        </div>
                    )}

                    {loading ? (
                        <div className="flex flex-col items-center justify-center py-12">
                            <span className="material-symbols-outlined animate-spin text-[40px] text-primary mb-4">sync</span>
                            <p className="text-gray-500 font-medium">Đang tải thông tin...</p>
                        </div>
                    ) : (
                        <form onSubmit={handleSubmit} className="space-y-6">
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                                <div className="space-y-2">
                                    <label className="text-sm font-semibold text-gray-700 block">Họ và tên <span className="text-red-500">*</span></label>
                                    <div className="relative">
                                        <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                            <span className="material-symbols-outlined text-gray-400 text-[20px]">badge</span>
                                        </div>
                                        <input 
                                            type="text" 
                                            value={fullName}
                                            onChange={(e) => setFullName(e.target.value)}
                                            required
                                            placeholder="Nhập họ và tên"
                                            className="w-full pl-10 pr-4 py-3 bg-gray-50 border border-gray-200 rounded-xl outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all text-gray-800"
                                        />
                                    </div>
                                </div>

                                <div className="space-y-2">
                                    <label className="text-sm font-semibold text-gray-700 block">Số điện thoại <span className="text-red-500">*</span></label>
                                    <div className="relative">
                                        <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                            <span className="material-symbols-outlined text-gray-400 text-[20px]">phone_iphone</span>
                                        </div>
                                        <input 
                                            type="text" 
                                            value={phone}
                                            disabled
                                            className="w-full pl-10 pr-4 py-3 bg-gray-100 border border-gray-200 rounded-xl outline-none text-gray-500 cursor-not-allowed"
                                        />
                                    </div>
                                </div>

                                <div className="space-y-2 md:col-span-2">
                                    <label className="text-sm font-semibold text-gray-700 block">Email <span className="text-gray-400 font-normal ml-1">(Không bắt buộc)</span></label>
                                    <div className="relative">
                                        <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                            <span className="material-symbols-outlined text-gray-400 text-[20px]">mail</span>
                                        </div>
                                        <input 
                                            type="email" 
                                            value={email}
                                            onChange={(e) => setEmail(e.target.value)}
                                            placeholder="Nhập địa chỉ email"
                                            className="w-full pl-10 pr-4 py-3 bg-gray-50 border border-gray-200 rounded-xl outline-none focus:border-primary focus:ring-1 focus:ring-primary transition-all text-gray-800"
                                        />
                                    </div>
                                </div>
                            </div>

                            <div className="pt-6 mt-6 border-t border-gray-100 flex items-center justify-end gap-4">
                                <button
                                    type="button"
                                    onClick={() => onNavigate('home')}
                                    className="px-6 py-3 rounded-xl font-bold text-gray-600 hover:bg-gray-100 transition-colors"
                                >
                                    Hủy
                                </button>
                                <button
                                    type="submit"
                                    disabled={submitting}
                                    className="px-8 py-3 bg-primary text-white rounded-xl font-bold hover:bg-primary/90 transition-colors shadow-lg shadow-primary/30 flex items-center gap-2 disabled:opacity-70 disabled:cursor-not-allowed"
                                >
                                    {submitting ? (
                                        <>
                                            <span className="material-symbols-outlined animate-spin text-[20px]">sync</span>
                                            Đang lưu...
                                        </>
                                    ) : (
                                        <>
                                            <span className="material-symbols-outlined text-[20px]">save</span>
                                            Lưu thay đổi
                                        </>
                                    )}
                                </button>
                            </div>
                        </form>
                    )}
                </div>
            </main>
        </div>
    );
}
