import React, { useState, useEffect } from 'react';
import { fetchHubs, createUser, fetchUsers, updateUser, deleteUser } from '../api/identityApi';

interface Hub {
    id: string;
    name: string;
    address: string;
}

interface Customer {
    id: string;
    fullName: string;
    phone: string;
    email: string;
    hubId: string;
    hubName: string;
    status: 'Active' | 'Inactive';
    createdAt: string;
}

interface CreateCustomerPageProps {
    sidebar?: React.ReactNode;
}

export default function CreateCustomerPage({ sidebar }: CreateCustomerPageProps) {
    const [fullName, setFullName] = useState('');
    const [phone, setPhone] = useState('');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');
    const [selectedHubId, setSelectedHubId] = useState('');
    const [status, setStatus] = useState<'Active' | 'Inactive'>('Active');
    const [editingId, setEditingId] = useState<string | null>(null);
    
    // Search & list states
    const [searchTerm, setSearchTerm] = useState('');
    const [customers, setCustomers] = useState<Customer[]>([]);
    const [hubs, setHubs] = useState<Hub[]>([]);
    const [submitting, setSubmitting] = useState(false);
    const [toasts, setToasts] = useState<Array<{ id: string; message: string; type: 'success' | 'error' }>>([]);

    const showToast = (message: string, type: 'success' | 'error' = 'success') => {
        const id = Math.random().toString(36).substring(2, 9);
        setToasts(prev => [...prev, { id, message, type }]);
        setTimeout(() => {
            setToasts(prev => prev.filter(t => t.id !== id));
        }, 3000);
    };

    // Load data from backend API
    const refreshData = async () => {
        try {
            const [hubsData, usersData] = await Promise.all([fetchHubs(), fetchUsers()]);
            setHubs(hubsData);
            
            // Map users to Customers list
            const customerUsers = usersData
                .filter(u => u.role === 'Customer')
                .map((u) => {
                    const hub = hubsData.find(h => h.id === u.hubId);
                    return {
                        id: u.id,
                        fullName: u.fullName,
                        phone: u.phone || '--',
                        email: u.email || '--',
                        hubId: u.hubId || '',
                        hubName: hub ? hub.name : 'Không liên kết',
                        status: 'Active' as const,
                        createdAt: new Date(u.createdAt).toLocaleDateString('vi-VN')
                    };
                });
            setCustomers(customerUsers);
        } catch (err: any) {
            console.error("Lỗi đồng bộ dữ liệu từ API:", err);
            showToast(err.message || 'Lỗi kết nối máy chủ API', 'error');
        }
    };

    useEffect(() => {
        refreshData();
    }, []);

    // Password strength check
    const getPasswordStrength = () => {
        if (!password) return { label: '', color: 'bg-slate-200', textClass: 'text-slate-400', width: 'w-0' };
        if (password.length < 6) return { label: 'Yếu', color: 'bg-error', textClass: 'text-error', width: 'w-1/3' };
        
        const hasNumbers = /\d/.test(password);
        const hasSpecial = /[!@#$%^&*(),.?":{}|<>]/.test(password);
        
        if (password.length >= 8 && hasNumbers && hasSpecial) {
            return { label: 'Mạnh', color: 'bg-secondary', textClass: 'text-secondary', width: 'w-full' };
        }
        return { label: 'Trung bình', color: 'bg-on-tertiary-container', textClass: 'text-on-tertiary-container', width: 'w-2/3' };
    };

    const strength = getPasswordStrength();

    // Generate random secure password
    const handleGeneratePassword = () => {
        const chars = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*';
        let generatedPassword = '';
        for (let i = 0; i < 12; i++) {
            generatedPassword += chars.charAt(Math.floor(Math.random() * chars.length));
        }
        setPassword(generatedPassword);
        setConfirmPassword(generatedPassword);
        showToast('Đã tạo mật khẩu ngẫu nhiên bảo mật', 'success');
    };

    const handleStartEdit = (cust: Customer) => {
        setEditingId(cust.id);
        setFullName(cust.fullName);
        setPhone(cust.phone === '--' ? '' : cust.phone);
        setEmail(cust.email === '--' ? '' : cust.email);
        setPassword('');
        setConfirmPassword('');
        setSelectedHubId(cust.hubId || '');
        showToast(`Đang sửa thông tin tài khoản: ${cust.fullName}`);
    };

    const handleDelete = async (id: string, name: string) => {
        if (!window.confirm(`Bạn có chắc chắn muốn xóa tài khoản "${name}" không?`)) {
            return;
        }
        try {
            await deleteUser(id);
            showToast(`Xóa tài khoản "${name}" thành công!`, 'success');
            await refreshData();
        } catch (err: any) {
            showToast(err.message || 'Xóa tài khoản thất bại.', 'error');
        }
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        // Validation
        if (!fullName || !phone || !email || (!editingId && (!password || !confirmPassword))) {
            showToast('Vui lòng điền đầy đủ thông tin bắt buộc!', 'error');
            return;
        }

        if (password && password !== confirmPassword) {
            showToast('Mật khẩu nhập lại không khớp!', 'error');
            return;
        }

        setSubmitting(true);

        try {
            if (editingId) {
                await updateUser(editingId, {
                    fullName,
                    phone,
                    email,
                    password: password || undefined,
                    hubId: selectedHubId || null,
                    role: 'Customer'
                });
                showToast(`Cập nhật tài khoản "${fullName}" thành công!`, 'success');
            } else {
                await createUser({
                    fullName,
                    phone,
                    email,
                    password,
                    hubId: selectedHubId || null,
                    role: 'Customer'
                });
                showToast(`Tạo tài khoản "${fullName}" thành công!`, 'success');
            }
            
            // Reset fields
            setFullName('');
            setPhone('');
            setEmail('');
            setPassword('');
            setConfirmPassword('');
            setSelectedHubId('');
            setStatus('Active');
            setEditingId(null);
            
            // Refresh listing from DB
            await refreshData();
        } catch (error: any) {
            showToast(error?.message || 'Có lỗi xảy ra.', 'error');
        } finally {
            setSubmitting(false);
        }
    };

    const filteredCustomers = customers.filter(c => 
        c.fullName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        c.phone.includes(searchTerm) ||
        c.email.toLowerCase().includes(searchTerm.toLowerCase())
    );

    return (
        <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
            {/* Sidebar */}
            {sidebar}

            {/* Main Content wrapper */}
            <div className="flex-1 flex flex-col xl:ml-64 w-full">
                {/* Header */}
                <header className="bg-surface-container-lowest border-b border-outline-variant h-16 w-full flex justify-between items-center px-8 sticky top-0 z-10">
                    <div className="text-headline-md font-bold text-primary flex items-center gap-2">
                        <span className="material-symbols-outlined">shield_person</span>
                        Hệ Thống Đăng Ký Tài Khoản Khách Hàng
                    </div>
                    <div className="w-8 h-8 rounded-full bg-surface-variant overflow-hidden border border-outline-variant/50 ml-2">
                        <img alt="User Avatar" className="w-full h-full object-cover" src="https://lh3.googleusercontent.com/aida/AP1WRLsAnOAwTMZ6WYlncjkQ1wDt3lh-5zxXSSS8JggN1WvIsN8EuRxgEIdFOjF9I1_IhNz75PX8mHdibja7ELV4_3v0bVkMJLEaGeaaQVolYGiFyeLnJ13AmHloSfAL5cv_9FiHGaCngsQzKDui3CrqaSwov1bKnbVGvda30ObggYAs_Di8Q_l54hEDSewYFtlGK4wk_bc_l7fKJoXtxssZT84eHh3fzZ9XbCLzIBt0x9yzQRo1g6lxMUhRoag" />
                    </div>
                </header>

                {/* Form Main Area */}
                <main className="flex-1 p-container-margin overflow-y-auto space-y-6">
                    {/* Page Title */}
                    <div>
                        <div className="flex items-center text-label-md font-label-md text-on-surface-variant mb-1">
                            <span className="hover:text-primary cursor-pointer transition-colors">Quản trị viên</span>
                            <span className="material-symbols-outlined text-[14px] mx-1">chevron_right</span>
                            <span className="text-primary font-bold">Tạo khách hàng</span>
                        </div>
                        <h2 className="text-headline-lg font-headline-lg text-on-surface">Tạo Mới Khách Hàng</h2>
                        <p className="text-body-md text-on-surface-variant mt-1">
                            Đăng ký tài khoản khách hàng gửi hàng mới (Customer) chưa thông qua API.
                        </p>
                    </div>

                    <div className="grid grid-cols-1 lg:grid-cols-3 gap-gutter">
                        {/* Form Card */}
                        <div className="lg:col-span-2 bg-surface-container-lowest rounded-2xl p-card-padding card-shadow border border-outline-variant/30 relative overflow-hidden group">
                            <div className="absolute top-0 left-0 w-2 h-full bg-primary transition-all duration-300 group-hover:w-3" />
                            
                            <form onSubmit={handleSubmit} className="space-y-6 pl-2">
                                <h3 className="text-headline-md font-bold text-on-surface flex items-center gap-2 mb-4">
                                    <span className="material-symbols-outlined text-primary">feed</span>
                                    Thông Tin Đăng Ký
                                </h3>

                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    {/* Full Name */}
                                    <div className="flex flex-col gap-1.5">
                                        <label className="text-label-md font-bold text-on-surface-variant flex items-center gap-1">
                                            Họ và tên khách hàng <span className="text-error">*</span>
                                        </label>
                                        <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-3 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                                            <span className="material-symbols-outlined text-on-surface-variant/70 mr-2 text-[20px]">person</span>
                                            <input 
                                                type="text" 
                                                placeholder="Ví dụ: Nguyễn Văn A" 
                                                className="bg-transparent border-none outline-none text-body-md w-full focus:ring-0 p-0 text-on-surface"
                                                value={fullName}
                                                onChange={(e) => setFullName(e.target.value)}
                                                required 
                                            />
                                        </div>
                                    </div>

                                    {/* Phone */}
                                    <div className="flex flex-col gap-1.5">
                                        <label className="text-label-md font-bold text-on-surface-variant flex items-center gap-1">
                                            Số điện thoại <span className="text-error">*</span>
                                        </label>
                                        <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-3 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                                            <span className="material-symbols-outlined text-on-surface-variant/70 mr-2 text-[20px]">call</span>
                                            <input 
                                                type="tel" 
                                                placeholder="Ví dụ: 0912345678" 
                                                className="bg-transparent border-none outline-none text-body-md w-full focus:ring-0 p-0 text-on-surface"
                                                value={phone}
                                                onChange={(e) => setPhone(e.target.value)}
                                                required 
                                            />
                                        </div>
                                    </div>
                                </div>

                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    {/* Email */}
                                    <div className="flex flex-col gap-1.5">
                                        <label className="text-label-md font-bold text-on-surface-variant flex items-center gap-1">
                                            Địa chỉ Email <span className="text-error">*</span>
                                        </label>
                                        <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-3 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                                            <span className="material-symbols-outlined text-on-surface-variant/70 mr-2 text-[20px]">mail</span>
                                            <input 
                                                type="email" 
                                                placeholder="customer@example.com" 
                                                className="bg-transparent border-none outline-none text-body-md w-full focus:ring-0 p-0 text-on-surface"
                                                value={email}
                                                onChange={(e) => setEmail(e.target.value)}
                                                required 
                                            />
                                        </div>
                                    </div>

                                    {/* Password */}
                                    <div className="flex flex-col gap-1.5">
                                        <label className="text-label-md font-bold text-on-surface-variant flex items-center justify-between">
                                            <span className="flex items-center gap-1">Mật khẩu <span className="text-error">*</span></span>
                                            <button 
                                                type="button" 
                                                onClick={handleGeneratePassword}
                                                className="text-primary hover:underline text-[12px] flex items-center gap-1 font-bold"
                                            >
                                                <span className="material-symbols-outlined text-[14px]">key</span> Tạo ngẫu nhiên
                                            </button>
                                        </label>
                                        <div className="flex flex-col gap-1">
                                            <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-3 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                                                <span className="material-symbols-outlined text-on-surface-variant/70 mr-2 text-[20px]">lock</span>
                                                <input 
                                                    type="text" 
                                                    placeholder="Nhập hoặc sinh mật khẩu" 
                                                    className="bg-transparent border-none outline-none text-body-md w-full focus:ring-0 p-0 text-on-surface"
                                                    value={password}
                                                    onChange={(e) => setPassword(e.target.value)}
                                                    required 
                                                />
                                            </div>
                                            {/* Password Strength Meter */}
                                            {password && (
                                                <div className="px-1 mt-1 flex flex-col gap-1">
                                                    <div className="w-full h-1.5 bg-slate-100 rounded-full overflow-hidden">
                                                        <div className={`h-full transition-all duration-300 ${strength.width} ${strength.color}`} />
                                                    </div>
                                                    <div className="flex justify-between items-center text-[11px]">
                                                        <span className="text-on-surface-variant/70">Độ mạnh mật khẩu</span>
                                                        <span className={`font-bold ${strength.textClass}`}>{strength.label}</span>
                                                    </div>
                                                </div>
                                            )}
                                        </div>
                                    </div>
                                </div>

                                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    {/* Hub Selection */}
                                    <div className="flex flex-col gap-1.5">
                                        <label className="text-label-md font-bold text-on-surface-variant">
                                            Kho hàng trực thuộc (Hub)
                                        </label>
                                        <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-3 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                                            <span className="material-symbols-outlined text-on-surface-variant/70 mr-2 text-[20px]">warehouse</span>
                                            <select 
                                                className="bg-transparent border-none outline-none text-body-md w-full focus:ring-0 p-0 text-on-surface"
                                                value={selectedHubId}
                                                onChange={(e) => setSelectedHubId(e.target.value)}
                                            >
                                                <option value="" className="bg-surface">-- Chọn kho hàng trực thuộc --</option>
                                                {hubs.map((hub) => (
                                                    <option key={hub.id} value={hub.id} className="bg-surface">
                                                        {hub.name}
                                                    </option>
                                                ))}
                                            </select>
                                        </div>
                                    </div>

                                    {/* Confirm Password */}
                                    <div className="flex flex-col gap-1.5">
                                        <label className="text-label-md font-bold text-on-surface-variant">
                                            Nhập lại mật khẩu <span className="text-error">*</span>
                                        </label>
                                        <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-3 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                                            <span className="material-symbols-outlined text-on-surface-variant/70 mr-2 text-[20px]">lock_reset</span>
                                            <input 
                                                type="text" 
                                                placeholder="Nhập lại mật khẩu để xác nhận" 
                                                className="bg-transparent border-none outline-none text-body-md w-full focus:ring-0 p-0 text-on-surface"
                                                value={confirmPassword}
                                                onChange={(e) => setConfirmPassword(e.target.value)}
                                                required 
                                            />
                                        </div>
                                    </div>

                                    {/* Status Switch */}
                                    <div className="flex flex-col gap-1.5">
                                        <label className="text-label-md font-bold text-on-surface-variant">
                                            Trạng thái tài khoản
                                        </label>
                                        <div className="flex gap-4 items-center h-[50px]">
                                            <label className="flex items-center gap-2 cursor-pointer">
                                                <input 
                                                    type="radio" 
                                                    name="status"
                                                    value="Active"
                                                    checked={status === 'Active'}
                                                    onChange={() => setStatus('Active')}
                                                    className="w-4 h-4 text-primary focus:ring-primary"
                                                />
                                                <span className="text-body-md font-semibold text-slate-700 flex items-center gap-1">
                                                    <span className="w-2.5 h-2.5 rounded-full bg-secondary"></span> Hoạt động
                                                </span>
                                            </label>

                                            <label className="flex items-center gap-2 cursor-pointer">
                                                <input 
                                                    type="radio" 
                                                    name="status"
                                                    value="Inactive"
                                                    checked={status === 'Inactive'}
                                                    onChange={() => setStatus('Inactive')}
                                                    className="w-4 h-4 text-primary focus:ring-primary"
                                                />
                                                <span className="text-body-md font-semibold text-slate-700 flex items-center gap-1">
                                                    <span className="w-2.5 h-2.5 rounded-full bg-outline-variant"></span> Tạm khóa
                                                </span>
                                            </label>
                                        </div>
                                    </div>
                                </div>

                                {/* Submit Button */}
                                <div className="flex justify-end gap-3 pt-4 border-t border-outline-variant/20">
                                    {editingId && (
                                        <button
                                            type="button"
                                            onClick={() => {
                                                setEditingId(null);
                                                setFullName('');
                                                setPhone('');
                                                setEmail('');
                                                setPassword('');
                                                setConfirmPassword('');
                                                setSelectedHubId('');
                                                setStatus('Active');
                                            }}
                                            className="border border-outline hover:bg-surface-container-low text-on-surface text-label-lg font-bold py-3 px-6 rounded-xl transition-all"
                                        >
                                            Hủy sửa
                                        </button>
                                    )}
                                    <button
                                        type="submit"
                                        disabled={submitting}
                                        className="bg-primary hover:bg-primary/90 text-on-primary text-label-lg font-bold py-3 px-8 rounded-xl transition-all shadow-md hover:shadow-lg disabled:opacity-50 flex items-center justify-center gap-2"
                                    >
                                        {submitting ? (
                                            <>
                                                <span className="material-symbols-outlined animate-spin">sync</span>
                                                Đang lưu dữ liệu...
                                            </>
                                        ) : (
                                            <>
                                                <span className="material-symbols-outlined">{editingId ? 'edit' : 'person_add'}</span>
                                                {editingId ? 'Cập nhật Khách Hàng' : 'Đăng Ký Khách Hàng'}
                                            </>
                                        )}
                                    </button>
                                </div>
                            </form>
                        </div>

                        {/* Banner & Statistics Panel */}
                        <div className="space-y-4">
                            {/* Summary Card */}
                            <div className="bg-gradient-to-br from-[#122c91] to-[#0a1e68] text-white rounded-2xl p-card-padding border border-primary-container shadow-lg flex flex-col justify-between h-[200px]">
                                <div>
                                    <div className="text-sm font-semibold tracking-wider text-primary-fixed-dim uppercase">Tổng Quan Khách Hàng</div>
                                    <div className="text-display-lg font-bold mt-2 text-[36px]">{customers.length}</div>
                                </div>
                                <div className="flex items-center justify-between text-xs text-primary-fixed-dim">
                                    <span>Đang hoạt động: {customers.filter(c => c.status === 'Active').length}</span>
                                    <span>Tạm khóa: {customers.filter(c => c.status === 'Inactive').length}</span>
                                </div>
                            </div>

                            {/* Help Box */}
                            <div className="bg-surface-container rounded-2xl p-card-padding border border-outline-variant/30 space-y-4">
                                <h3 className="text-label-lg font-bold text-on-surface flex items-center gap-2">
                                    <span className="material-symbols-outlined text-primary text-[20px]">help_center</span>
                                    Hướng dẫn tạo nhanh
                                </h3>
                                <ul className="text-xs text-on-surface-variant space-y-2 list-disc pl-4">
                                    <li>Mọi thông tin liên lạc sẽ được dùng để gửi mã vận đơn qua SMS.</li>
                                    <li>Liên kết Hub giúp việc thu gom hàng hóa của tài xế gần đó diễn ra tự động và nhanh hơn.</li>
                                    <li>Trạng thái tạm khóa sẽ khóa quyền truy cập ngay lập tức của khách hàng.</li>
                                </ul>
                            </div>
                        </div>
                    </div>

                    {/* Table View: Recently Created Users */}
                    <div className="bg-surface-container-lowest rounded-2xl p-card-padding card-shadow border border-outline-variant/30 space-y-4">
                        <div className="flex flex-col sm:flex-row sm:justify-between sm:items-center gap-3">
                            <div>
                                <h3 className="text-headline-md font-bold text-on-surface flex items-center gap-2">
                                    <span className="material-symbols-outlined text-primary">table_rows</span>
                                    Danh Sách Tài Khoản Mới Đăng Ký
                                </h3>
                                <p className="text-xs text-on-surface-variant">Dữ liệu được lưu trữ trong danh sách ảo (mất đi khi load lại trang).</p>
                            </div>
                            <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-1.5 border border-outline-variant/50 w-64">
                                <span className="material-symbols-outlined text-on-surface-variant mr-2 text-[18px]">search</span>
                                <input 
                                    type="text" 
                                    placeholder="Tìm kiếm khách hàng..." 
                                    className="bg-transparent border-none outline-none text-xs w-full focus:ring-0 p-0 text-on-surface"
                                    value={searchTerm}
                                    onChange={(e) => setSearchTerm(e.target.value)}
                                />
                            </div>
                        </div>

                        <div className="overflow-x-auto">
                            <table className="w-full text-left text-xs border-collapse">
                                <thead>
                                    <tr className="border-b border-outline-variant/30 text-on-surface-variant">
                                        <th className="py-3 px-4 font-bold">Mã khách hàng</th>
                                        <th className="py-3 px-4 font-bold">Tên khách hàng</th>
                                        <th className="py-3 px-4 font-bold">Số điện thoại</th>
                                        <th className="py-3 px-4 font-bold">Email</th>
                                        <th className="py-3 px-4 font-bold">Kho hàng (Hub)</th>
                                        <th className="py-3 px-4 font-bold">Trạng thái</th>
                                        <th className="py-3 px-4 font-bold">Ngày tạo</th>
                                        <th className="py-3 px-4 font-bold text-center">Thao tác</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {filteredCustomers.length === 0 ? (
                                        <tr>
                                            <td colSpan={8} className="py-8 text-center text-on-surface-variant/70 font-semibold">
                                                Không tìm thấy kết quả nào phù hợp.
                                            </td>
                                        </tr>
                                    ) : (
                                        filteredCustomers.map(cust => (
                                            <tr key={cust.id} className="border-b border-outline-variant/10 hover:bg-surface-container-low/30 transition-colors">
                                                <td className="py-3.5 px-4 font-bold text-primary">{cust.id.substring(0, 8).toUpperCase()}</td>
                                                <td className="py-3.5 px-4 font-bold flex items-center gap-2">
                                                    <div className="w-6 h-6 rounded-full bg-primary/10 text-primary flex items-center justify-center font-bold text-[10px]">
                                                        {cust.fullName.charAt(0)}
                                                    </div>
                                                    {cust.fullName}
                                                </td>
                                                <td className="py-3.5 px-4 font-semibold text-slate-700">{cust.phone}</td>
                                                <td className="py-3.5 px-4 text-slate-600">{cust.email}</td>
                                                <td className="py-3.5 px-4 font-medium text-slate-800">{cust.hubName}</td>
                                                <td className="py-3.5 px-4">
                                                    <span className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${
                                                        cust.status === 'Active' 
                                                        ? 'bg-secondary-container text-on-secondary-container' 
                                                        : 'bg-outline-variant/30 text-on-surface-variant'
                                                    }`}>
                                                        {cust.status === 'Active' ? 'Hoạt động' : 'Tạm khóa'}
                                                    </span>
                                                </td>
                                                <td className="py-3.5 px-4 text-slate-500">{cust.createdAt}</td>
                                                <td className="py-3.5 px-4 text-center">
                                                    <div className="flex justify-center gap-2">
                                                        <button 
                                                            type="button"
                                                            onClick={() => handleStartEdit(cust)}
                                                            className="text-primary hover:text-primary/80 transition-colors"
                                                            title="Sửa"
                                                        >
                                                            <span className="material-symbols-outlined text-[18px]">edit</span>
                                                        </button>
                                                        <button 
                                                            type="button"
                                                            onClick={() => handleDelete(cust.id, cust.fullName)}
                                                            className="text-error hover:text-error/80 transition-colors"
                                                            title="Xóa"
                                                        >
                                                            <span className="material-symbols-outlined text-[18px]">delete</span>
                                                        </button>
                                                    </div>
                                                </td>
                                            </tr>
                                        ))
                                    )}
                                </tbody>
                            </table>
                        </div>
                    </div>
                </main>
            </div>

            {/* Toast Container */}
            <div className="fixed right-6 bottom-6 z-50 flex flex-col gap-3">
                {toasts.map(toast => (
                    <div 
                        key={toast.id} 
                        className={`p-4 rounded-xl border shadow-lg flex gap-2 items-center min-w-[280px] bg-surface animate-bounce duration-500 ${
                            toast.type === 'success' ? 'border-secondary text-secondary' : 'border-error text-error'
                        }`}
                    >
                        <span className="material-symbols-outlined">
                            {toast.type === 'success' ? 'check_circle' : 'error'}
                        </span>
                        <span className="text-xs font-bold text-on-surface">{toast.message}</span>
                    </div>
                ))}
            </div>
        </div>
    );
}
