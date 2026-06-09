import React, { useState, useEffect } from 'react';

interface Hub {
    id: string;
    name: string;
    address: string;
}

interface DriverFleet {
    id: string;
    fullName: string;
    phone: string;
    email: string;
    hubName: string;
    licensePlate: string;
    truckType: string;
    maxWeight: number;
    maxVolume: number;
    createdAt: string;
}

interface CreateDriverPageProps {
    sidebar?: React.ReactNode;
}

export default function CreateDriverPage({ sidebar }: CreateDriverPageProps) {
    const [fullName, setFullName] = useState('');
    const [phone, setPhone] = useState('');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [selectedHubId, setSelectedHubId] = useState('');
    
    // Vehicle fields
    const [licensePlate, setLicensePlate] = useState('');
    const [truckType, setTruckType] = useState('Xe Tải Nhẹ 1.5 Tấn');
    const [maxWeight, setMaxWeight] = useState(1500);
    const [maxVolume, setMaxVolume] = useState(8.5);

    // Mock Fleet List
    const [fleet, setFleet] = useState<DriverFleet[]>([
        { id: 'DRIV-001', fullName: 'Phạm Minh Chiến', phone: '0987654321', email: 'chien.pham@gmail.com', hubName: 'Kho Gò Vấp - TP.HCM', licensePlate: '51C-998.76', truckType: 'Xe Tải Nhẹ 1.5 Tấn', maxWeight: 1500, maxVolume: 8.5, createdAt: '10/06/2026' },
        { id: 'DRIV-002', fullName: 'Ngô Quốc Bảo', phone: '0901223344', email: 'bao.ngo@hms.com', hubName: 'Kho Tân Bình - TP.HCM', licensePlate: '51D-123.45', truckType: 'Xe Tải Trung 5 Tấn', maxWeight: 5000, maxVolume: 22.0, createdAt: '09/06/2026' },
        { id: 'DRIV-003', fullName: 'Hoàng Văn Hải', phone: '0966554433', email: 'hai.hoang@yahoo.com', hubName: 'Kho Hà Nội', licensePlate: '29C-555.22', truckType: 'Xe Tải Nặng 15 Tấn', maxWeight: 15000, maxVolume: 45.0, createdAt: '07/06/2026' }
    ]);

    const [hubs] = useState<Hub[]>([
        { id: 'h1', name: 'Kho Gò Vấp - TP.HCM', address: '12 Nguyễn Oanh, Gò Vấp, HCMC' },
        { id: 'h2', name: 'Kho Tân Bình - TP.HCM', address: '45 Cộng Hòa, Tân Bình, HCMC' },
        { id: 'h3', name: 'Kho Hà Nội', address: '102 Giải Phóng, Đống Đa, Hà Nội' },
        { id: 'h4', name: 'Kho Đà Nẵng', address: '88 Nguyễn Lương Bằng, Liên Chiểu, Đà Nẵng' }
    ]);

    const [searchTerm, setSearchTerm] = useState('');
    const [submitting, setSubmitting] = useState(false);
    const [toasts, setToasts] = useState<Array<{ id: string; message: string; type: 'success' | 'error' }>>([]);

    const showToast = (message: string, type: 'success' | 'error' = 'success') => {
        const id = Math.random().toString(36).substring(2, 9);
        setToasts(prev => [...prev, { id, message, type }]);
        setTimeout(() => {
            setToasts(prev => prev.filter(t => t.id !== id));
        }, 3000);
    };

    // Auto calculate capacity defaults when truck type changes
    useEffect(() => {
        if (truckType === 'Xe Tải Nhẹ 1.5 Tấn') {
            setMaxWeight(1500);
            setMaxVolume(8.5);
        } else if (truckType === 'Xe Tải Trung 5 Tấn') {
            setMaxWeight(5000);
            setMaxVolume(22);
        } else if (truckType === 'Xe Tải Nặng 15 Tấn') {
            setMaxWeight(15000);
            setMaxVolume(45);
        } else if (truckType === 'Xe Container 32 Tấn') {
            setMaxWeight(32000);
            setMaxVolume(80);
        }
    }, [truckType]);

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
        showToast('Đã tạo mật khẩu ngẫu nhiên bảo mật', 'success');
    };

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();

        // Validation
        if (!fullName || !phone || !email || !password || !selectedHubId || !licensePlate) {
            showToast('Vui lòng điền đầy đủ các thông tin bắt buộc!', 'error');
            return;
        }

        setSubmitting(true);

        setTimeout(() => {
            const selectedHub = hubs.find(h => h.id === selectedHubId);
            const newDriver: DriverFleet = {
                id: `DRIV-0${fleet.length + 1}`,
                fullName,
                phone,
                email,
                hubName: selectedHub ? selectedHub.name : 'Chưa liên kết',
                licensePlate,
                truckType,
                maxWeight,
                maxVolume,
                createdAt: new Date().toLocaleDateString('vi-VN')
            };

            setFleet(prev => [newDriver, ...prev]);
            showToast(`Tạo tài xế "${fullName}" thành công!`, 'success');
            
            // Reset form
            setFullName('');
            setPhone('');
            setEmail('');
            setPassword('');
            setSelectedHubId('');
            setLicensePlate('');
            setTruckType('Xe Tải Nhẹ 1.5 Tấn');
            setSubmitting(false);
        }, 1200);
    };

    const filteredFleet = fleet.filter(d => 
        d.fullName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        d.phone.includes(searchTerm) ||
        d.licensePlate.toLowerCase().includes(searchTerm.toLowerCase()) ||
        d.truckType.toLowerCase().includes(searchTerm.toLowerCase())
    );

    // Get Truck Icon/Color decoration
    const getTruckVisuals = () => {
        switch (truckType) {
            case 'Xe Tải Nhẹ 1.5 Tấn':
                return { color: 'from-[#65a30d] to-[#4d7c0f]', icon: 'local_shipping', label: 'Xe tải nhẹ đô thị' };
            case 'Xe Tải Trung 5 Tấn':
                return { color: 'from-[#2563eb] to-[#1d4ed8]', icon: 'rv_hookup', label: 'Xe tải đường dài cỡ trung' };
            case 'Xe Tải Nặng 15 Tấn':
                return { color: 'from-[#ea580c] to-[#c2410c]', icon: 'local_shipping', label: 'Xe tải nặng liên tỉnh' };
            case 'Xe Container 32 Tấn':
                return { color: 'from-[#7c3aed] to-[#6d28d9]', icon: 'format_align_justify', label: 'Đầu kéo siêu trường' };
            default:
                return { color: 'from-primary to-primary-900', icon: 'local_shipping', label: 'Phương tiện vận tải' };
        }
    };

    const truckVisual = getTruckVisuals();

    return (
        <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
            {/* Sidebar */}
            {sidebar}

            {/* Main Content wrapper */}
            <div className="flex-1 flex flex-col xl:ml-64 w-full">
                {/* Header */}
                <header className="bg-surface-container-lowest border-b border-outline-variant h-16 w-full flex justify-between items-center px-8 sticky top-0 z-10">
                    <div className="text-headline-md font-bold text-primary flex items-center gap-2">
                        <span className="material-symbols-outlined">local_shipping</span>
                        Hệ Thống Đăng Ký Tài Xế & Xe Tải
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
                            <span className="text-primary font-bold">Tạo tài xế</span>
                        </div>
                        <h2 className="text-headline-lg font-headline-lg text-on-surface">Đăng Ký Tài Xế & Phương Tiện</h2>
                        <p className="text-body-md text-on-surface-variant mt-1">
                            Đăng ký tài khoản tài xế (Driver) mới kèm theo đăng ký xe tải để phục vụ thuật toán ghép chuyến (Offline Mock).
                        </p>
                    </div>

                    <div className="grid grid-cols-1 lg:grid-cols-3 gap-gutter">
                        {/* Form Card */}
                        <div className="lg:col-span-2 bg-surface-container-lowest rounded-2xl p-card-padding card-shadow border border-outline-variant/30 relative overflow-hidden group">
                            <div className="absolute top-0 left-0 w-2 h-full bg-primary transition-all duration-300 group-hover:w-3" />
                            
                            <form onSubmit={handleSubmit} className="space-y-6 pl-2">
                                {/* SECTION 1: Driver Information */}
                                <div>
                                    <h3 className="text-headline-md font-bold text-primary mb-4 pb-2 border-b border-outline-variant/20 flex items-center gap-2">
                                        <span className="material-symbols-outlined text-[22px]">badge</span>
                                        1. Thông Tin Tài Xế
                                    </h3>
                                    
                                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
                                        {/* Full Name */}
                                        <div className="flex flex-col gap-1.5">
                                            <label className="text-label-md font-bold text-on-surface-variant flex items-center gap-1">
                                                Họ và tên tài xế <span className="text-error">*</span>
                                            </label>
                                            <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-3 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                                                <span className="material-symbols-outlined text-on-surface-variant/70 mr-2 text-[20px]">person</span>
                                                <input 
                                                    type="text" 
                                                    placeholder="Nhập họ và tên tài xế" 
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
                                                    placeholder="Ví dụ: 0987654321" 
                                                    className="bg-transparent border-none outline-none text-body-md w-full focus:ring-0 p-0 text-on-surface"
                                                    value={phone}
                                                    onChange={(e) => setPhone(e.target.value)}
                                                    required 
                                                />
                                            </div>
                                        </div>
                                    </div>

                                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
                                        {/* Email */}
                                        <div className="flex flex-col gap-1.5">
                                            <label className="text-label-md font-bold text-on-surface-variant flex items-center gap-1">
                                                Địa chỉ Email <span className="text-error">*</span>
                                            </label>
                                            <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-3 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                                                <span className="material-symbols-outlined text-on-surface-variant/70 mr-2 text-[20px]">mail</span>
                                                <input 
                                                    type="email" 
                                                    placeholder="driver@example.com" 
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
                                                        placeholder="Nhập hoặc tạo mật khẩu" 
                                                        className="bg-transparent border-none outline-none text-body-md w-full focus:ring-0 p-0 text-on-surface"
                                                        value={password}
                                                        onChange={(e) => setPassword(e.target.value)}
                                                        required 
                                                    />
                                                </div>
                                                {password && (
                                                    <div className="px-1 mt-1 flex flex-col gap-1">
                                                        <div className="w-full h-1.5 bg-slate-100 rounded-full overflow-hidden">
                                                            <div className={`h-full ${strength.color} ${strength.width} transition-all duration-300`}></div>
                                                        </div>
                                                        <span className={`text-[11px] font-bold ${strength.textClass}`}>
                                                            Mật khẩu: {strength.label}
                                                        </span>
                                                    </div>
                                                )}
                                            </div>
                                        </div>
                                    </div>

                                    {/* Hub Selection */}
                                    <div className="flex flex-col gap-1.5">
                                        <label className="text-label-md font-bold text-on-surface-variant flex items-center gap-1">
                                            Kho hàng trực thuộc (Hub) quản lý <span className="text-error">*</span>
                                        </label>
                                        <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-3 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                                            <span className="material-symbols-outlined text-on-surface-variant/70 mr-2 text-[20px]">warehouse</span>
                                            <select 
                                                className="bg-transparent border-none outline-none text-body-md w-full focus:ring-0 p-0 text-on-surface"
                                                value={selectedHubId}
                                                onChange={(e) => setSelectedHubId(e.target.value)}
                                                required
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
                                </div>

                                {/* SECTION 2: Vehicle Information */}
                                <div className="pt-4">
                                    <h3 className="text-headline-md font-bold text-primary mb-4 pb-2 border-b border-outline-variant/20 flex items-center gap-2">
                                        <span className="material-symbols-outlined text-[22px]">local_shipping</span>
                                        2. Đăng Ký Phương Tiện (Xe Tải)
                                    </h3>

                                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
                                        {/* License Plate */}
                                        <div className="flex flex-col gap-1.5">
                                            <label className="text-label-md font-bold text-on-surface-variant flex items-center gap-1">
                                                Biển số xe tải <span className="text-error">*</span>
                                            </label>
                                            <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-3 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                                                <span className="material-symbols-outlined text-on-surface-variant/70 mr-2 text-[20px]">featured_play_list</span>
                                                <input 
                                                    type="text" 
                                                    placeholder="Ví dụ: 29C-123.45" 
                                                    className="bg-transparent border-none outline-none text-body-md w-full focus:ring-0 p-0 text-on-surface"
                                                    value={licensePlate}
                                                    onChange={(e) => setLicensePlate(e.target.value)}
                                                    required 
                                                />
                                            </div>
                                        </div>

                                        {/* Truck Type */}
                                        <div className="flex flex-col gap-1.5">
                                            <label className="text-label-md font-bold text-on-surface-variant flex items-center gap-1">
                                                Phân loại xe tải <span className="text-error">*</span>
                                            </label>
                                            <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-3 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                                                <span className="material-symbols-outlined text-on-surface-variant/70 mr-2 text-[20px]">category</span>
                                                <select 
                                                    className="bg-transparent border-none outline-none text-body-md w-full focus:ring-0 p-0 text-on-surface"
                                                    value={truckType}
                                                    onChange={(e) => setTruckType(e.target.value)}
                                                    required
                                                >
                                                    <option value="Xe Tải Nhẹ 1.5 Tấn" className="bg-surface">Xe Tải Nhẹ (1.5 Tấn)</option>
                                                    <option value="Xe Tải Trung 5 Tấn" className="bg-surface">Xe Tải Trung (5.0 Tấn)</option>
                                                    <option value="Xe Tải Nặng 15 Tấn" className="bg-surface">Xe Tải Nặng (15.0 Tấn)</option>
                                                    <option value="Xe Container 32 Tấn" className="bg-surface">Xe Đầu Kéo Container (32.0 Tấn)</option>
                                                </select>
                                            </div>
                                        </div>
                                    </div>

                                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                        {/* Max Weight */}
                                        <div className="flex flex-col gap-1.5">
                                            <label className="text-label-md font-bold text-on-surface-variant flex items-center gap-1">
                                                Tải trọng tối đa (kg) <span className="text-error">*</span>
                                            </label>
                                            <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-3 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                                                <span className="material-symbols-outlined text-on-surface-variant/70 mr-2 text-[20px]">weight</span>
                                                <input 
                                                    type="number" 
                                                    className="bg-transparent border-none outline-none text-body-md w-full focus:ring-0 p-0 text-on-surface"
                                                    value={maxWeight}
                                                    onChange={(e) => setMaxWeight(Number(e.target.value))}
                                                    required 
                                                />
                                            </div>
                                        </div>

                                        {/* Max Volume */}
                                        <div className="flex flex-col gap-1.5">
                                            <label className="text-label-md font-bold text-on-surface-variant flex items-center gap-1">
                                                Thể tích thùng hàng (CBM) <span className="text-error">*</span>
                                            </label>
                                            <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-3 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                                                <span className="material-symbols-outlined text-on-surface-variant/70 mr-2 text-[20px]">view_in_ar</span>
                                                <input 
                                                    type="number" 
                                                    step="0.1"
                                                    className="bg-transparent border-none outline-none text-body-md w-full focus:ring-0 p-0 text-on-surface"
                                                    value={maxVolume}
                                                    onChange={(e) => setMaxVolume(Number(e.target.value))}
                                                    required 
                                                />
                                            </div>
                                        </div>
                                    </div>
                                </div>

                                {/* Submit Button */}
                                <div className="flex justify-end pt-4 border-t border-outline-variant/20">
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
                                                <span className="material-symbols-outlined">local_shipping</span>
                                                Đăng Ký Tài Xế & Xe
                                            </>
                                        )}
                                    </button>
                                </div>
                            </form>
                        </div>

                        {/* Truck capacity visualizer card */}
                        <div className="space-y-4">
                            {/* Visual Truck Spec Card */}
                            <div className={`bg-gradient-to-br ${truckVisual.color} text-white rounded-2xl p-card-padding shadow-lg flex flex-col justify-between h-[250px] relative overflow-hidden transition-all duration-300 group`}>
                                <div className="absolute right-0 bottom-0 opacity-10 translate-x-4 translate-y-4 transition-transform group-hover:scale-105 duration-300">
                                    <span className="material-symbols-outlined text-[150px]">{truckVisual.icon}</span>
                                </div>

                                <div className="relative z-10 space-y-1">
                                    <span className="text-[10px] uppercase tracking-wider bg-white/20 px-2 py-0.5 rounded-full font-bold">Thông số phương tiện</span>
                                    <h4 className="text-headline-md font-bold pt-1">{truckType}</h4>
                                    <p className="text-xs text-white/70">{truckVisual.label}</p>
                                </div>

                                <div className="relative z-10 grid grid-cols-2 gap-4 border-t border-white/20 pt-4">
                                    <div>
                                        <span className="text-[10px] text-white/60 block uppercase">Trọng tải tối đa</span>
                                        <span className="text-headline-sm font-bold">{maxWeight.toLocaleString('vi-VN')} kg</span>
                                    </div>
                                    <div>
                                        <span className="text-[10px] text-white/60 block uppercase">Thể tích thùng</span>
                                        <span className="text-headline-sm font-bold">{maxVolume} CBM</span>
                                    </div>
                                </div>
                            </div>

                            {/* Help Box */}
                            <div className="bg-surface-container rounded-2xl p-card-padding border border-outline-variant/30 space-y-4">
                                <h3 className="text-label-lg font-bold text-on-surface flex items-center gap-2">
                                    <span className="material-symbols-outlined text-primary text-[20px]">info</span>
                                    Ghép chuyến tối ưu
                                </h3>
                                <p className="text-xs text-on-surface-variant leading-relaxed">
                                    Khi tài xế được thêm vào Hub, thuật toán sẽ dựa trên Trọng tải và Thể tích của xe để gợi ý các lô hàng gom tối ưu nhất trên cùng một chuyến hành trình nhằm tối đa hóa hiệu suất xe.
                                </p>
                            </div>
                        </div>
                    </div>

                    {/* Table View: Drivers Fleet */}
                    <div className="bg-surface-container-lowest rounded-2xl p-card-padding card-shadow border border-outline-variant/30 space-y-4">
                        <div className="flex flex-col sm:flex-row sm:justify-between sm:items-center gap-3">
                            <div>
                                <h3 className="text-headline-md font-bold text-on-surface flex items-center gap-2">
                                    <span className="material-symbols-outlined text-primary">airport_shuttle</span>
                                    Danh Sách Tài Xế & Đội Xe Mới
                                </h3>
                                <p className="text-xs text-on-surface-variant">Dữ liệu ảo hiển thị offline (sẽ reset khi reload lại trình duyệt).</p>
                            </div>
                            <div className="flex items-center bg-surface-container-low rounded-xl px-3 py-1.5 border border-outline-variant/50 w-64">
                                <span className="material-symbols-outlined text-on-surface-variant mr-2 text-[18px]">search</span>
                                <input 
                                    type="text" 
                                    placeholder="Tìm biển số, tên tài xế..." 
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
                                        <th className="py-3 px-4 font-bold">Mã tài xế</th>
                                        <th className="py-3 px-4 font-bold">Tên tài xế</th>
                                        <th className="py-3 px-4 font-bold">Số điện thoại</th>
                                        <th className="py-3 px-4 font-bold">Biển số xe</th>
                                        <th className="py-3 px-4 font-bold">Loại xe tải</th>
                                        <th className="py-3 px-4 font-bold">Trọng tải (kg)</th>
                                        <th className="py-3 px-4 font-bold">Thể tích (CBM)</th>
                                        <th className="py-3 px-4 font-bold">Kho hàng (Hub)</th>
                                        <th className="py-3 px-4 font-bold">Ngày tạo</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {filteredFleet.length === 0 ? (
                                        <tr>
                                            <td colSpan={9} className="py-8 text-center text-on-surface-variant/70 font-semibold">
                                                Không có tài xế nào khớp với từ khóa tìm kiếm.
                                            </td>
                                        </tr>
                                    ) : (
                                        filteredFleet.map(driv => (
                                            <tr key={driv.id} className="border-b border-outline-variant/10 hover:bg-surface-container-low/30 transition-colors">
                                                <td className="py-3.5 px-4 font-bold text-primary">{driv.id}</td>
                                                <td className="py-3.5 px-4 font-bold flex items-center gap-2">
                                                    <div className="w-6 h-6 rounded-full bg-primary/10 text-primary flex items-center justify-center font-bold text-[10px]">
                                                        {driv.fullName.charAt(0)}
                                                    </div>
                                                    {driv.fullName}
                                                </td>
                                                <td className="py-3.5 px-4 font-semibold text-slate-700">{driv.phone}</td>
                                                <td className="py-3.5 px-4 font-bold text-slate-800">
                                                    <span className="bg-slate-100 border border-slate-300 rounded px-1.5 py-0.5 text-[10px]">
                                                        {driv.licensePlate}
                                                    </span>
                                                </td>
                                                <td className="py-3.5 px-4 text-slate-600 font-medium">{driv.truckType}</td>
                                                <td className="py-3.5 px-4 text-slate-700 font-semibold">{driv.maxWeight.toLocaleString('vi-VN')}</td>
                                                <td className="py-3.5 px-4 text-slate-700 font-semibold">{driv.maxVolume}</td>
                                                <td className="py-3.5 px-4 font-medium text-slate-800">{driv.hubName}</td>
                                                <td className="py-3.5 px-4 text-slate-500">{driv.createdAt}</td>
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
