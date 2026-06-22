import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';

interface AdminStats {
    activeTripCount: number;
    inTransitShipments: number;
    avgVehicleUtilisation: number;
    hubItemsWaitingOver3Days: number;
    lastUpdated: string;
}

interface DashboardPageProps {
    sidebar?: React.ReactNode;
}

export default function DashboardPage({ sidebar }: DashboardPageProps) {
    const [stats, setStats] = useState<AdminStats | null>({
        activeTripCount: 12,
        inTransitShipments: 342,
        avgVehicleUtilisation: 78.5,
        hubItemsWaitingOver3Days: 4,
        lastUpdated: new Date().toISOString()
    });
    const [connectionStatus, setConnectionStatus] = useState<string>('Đang kết nối...');

    useEffect(() => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl((import.meta.env.VITE_API_URL ?? "https://localhost:7059") + "/hub/fleet")
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connection.on("ReceiveAdminStats", (data: AdminStats) => {
            setStats(data);
        });

        const startConnection = async () => {
            try {
                await connection.start();
                setConnectionStatus('Connected');
            } catch (err) {
                console.error("Lỗi kết nối:", err);
                setConnectionStatus('Lỗi kết nối');
            }
        };

        startConnection();
        return () => { connection.stop(); };
    }, []);

    const formattedTime = stats ? new Date(stats.lastUpdated).toLocaleTimeString('vi-VN') : '--:--:--';

    const hubsData = [
        { name: 'Kho Gò Vấp - TP.HCM', value: 85, color: 'bg-primary' },
        { name: 'Kho Tân Bình - TP.HCM', value: 72, color: 'bg-secondary' },
        { name: 'Kho Hà Nội', value: 90, color: 'bg-primary-container text-primary' },
        { name: 'Kho Đà Nẵng', value: 65, color: 'bg-error' },
    ];

    const activeDrivers = [
        { name: 'Nguyễn Văn Hùng', vehicle: '51C-123.45', route: 'TP.HCM -> Đà Nẵng', status: 'Đang di chuyển', color: 'text-secondary bg-secondary/10' },
        { name: 'Phạm Minh Chiến', vehicle: '51C-998.76', route: 'Kho Gò Vấp -> Kho Tân Bình', status: 'Đang xếp hàng', color: 'text-primary bg-primary/10' },
        { name: 'Hoàng Văn Hải', vehicle: '29C-555.22', route: 'Hà Nội -> Hà Tĩnh', status: 'Sự cố (Breakdown)', color: 'text-error bg-error/10' },
        { name: 'Trần Thanh Sơn', vehicle: '43C-888.11', route: 'Đà Nẵng -> Quy Nhơn', status: 'Nghỉ ngơi', color: 'text-slate-500 bg-slate-100' },
    ];

    return (
        <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden">
            {/* --- SIDEBAR --- */}
            {sidebar}

            {/* --- MAIN CONTENT --- */}
            <div className="flex-1 flex flex-col xl:ml-64 w-full">
                {/* Header */}
                <header className="bg-surface-container-lowest border-b border-outline-variant h-16 w-full flex justify-between items-center px-8 sticky top-0 z-10">
                    <div className="hidden md:flex items-center bg-surface-container-low rounded-lg px-3 py-1.5 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 w-64 transition-all">
                        <span className="material-symbols-outlined text-on-surface-variant text-[20px] mr-2">search</span>
                        <input className="bg-transparent border-none outline-none text-body-md font-body-md w-full placeholder-on-surface-variant/70 focus:ring-0 p-0 text-on-surface" placeholder="Search trips..." type="text" />
                    </div>
                    <div className="flex items-center gap-6">
                        <div className="hidden sm:flex items-center gap-2 text-label-md font-label-md text-on-surface-variant bg-surface px-3 py-1 rounded-full border border-outline-variant/30">
                            <span className={`w-2 h-2 rounded-full ${connectionStatus === 'Connected' ? 'bg-secondary live-pulse' : 'bg-error'}`}></span>
                            {connectionStatus}
                        </div>
                        <div className="w-8 h-8 rounded-full bg-primary-fixed text-primary overflow-hidden border border-outline-variant/50 flex items-center justify-center">
                            <span className="material-symbols-outlined text-[20px]">person</span>
                        </div>
                    </div>
                </header>

                <main className="flex-1 p-container-margin overflow-y-auto">
                    <div className="flex flex-col sm:flex-row sm:justify-between sm:items-end mb-6 gap-4">
                        <div>
                            <div className="flex items-center text-label-md font-label-md text-on-surface-variant mb-1">
                                <span className="hover:text-primary cursor-pointer transition-colors">Analytics</span>
                                <span className="material-symbols-outlined text-[14px] mx-1">chevron_right</span>
                                <span className="text-primary font-bold">Real-time Operations</span>
                            </div>
                            <h2 className="text-headline-lg font-headline-lg text-on-surface">Operations Overview</h2>
                        </div>
                        <div className="text-label-md font-label-md text-on-surface-variant bg-surface-container-lowest px-3 py-1.5 rounded border border-outline-variant/30 card-shadow inline-flex items-center gap-2">
                            <span className="material-symbols-outlined text-[16px]">schedule</span> Cập nhật lúc: <strong className="text-primary">{formattedTime}</strong>
                        </div>
                    </div>

                    {/* KPI Row */}
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-gutter mb-gutter">
                        <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col justify-between">
                            <div className="flex justify-between items-start mb-4">
                                <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Active Trips</span>
                                <div className="w-8 h-8 rounded-full bg-primary-fixed flex items-center justify-center text-primary">
                                    <span className="material-symbols-outlined text-[20px]">route</span>
                                </div>
                            </div>
                            <span className="text-display-lg font-display-lg text-on-surface">{stats?.activeTripCount ?? '--'}</span>
                        </div>

                        <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col justify-between">
                            <div className="flex justify-between items-start mb-4">
                                <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">In-Transit Shipments</span>
                                <div className="w-8 h-8 rounded-full bg-surface-container-high flex items-center justify-center text-primary relative">
                                    <span className="material-symbols-outlined text-[20px] relative z-10">package_2</span>
                                    <span className="absolute inset-0 bg-primary/20 rounded-full live-pulse"></span>
                                </div>
                            </div>
                            <span className="text-display-lg font-display-lg text-on-surface">{stats?.inTransitShipments ?? '--'}</span>
                        </div>

                        <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col justify-between">
                            <div className="flex justify-between items-start mb-2">
                                <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Vehicle Utilisation</span>
                                <span className="text-headline-md font-headline-md text-on-tertiary-container">{stats?.avgVehicleUtilisation ?? 0}%</span>
                            </div>
                            <div className="w-full bg-surface-container-high rounded-full h-2 mb-3 overflow-hidden">
                                <div className="bg-on-tertiary-container h-2 rounded-full transition-all duration-500" style={{ width: `${stats?.avgVehicleUtilisation ?? 0}%` }}></div>
                            </div>
                        </div>

                        <div className="bg-error-container/20 rounded-xl p-card-padding card-shadow border border-error/20 flex flex-col justify-between relative overflow-hidden">
                            <div className="absolute top-0 right-0 w-24 h-24 bg-error/5 rounded-full -mr-8 -mt-8"></div>
                            <div className="flex justify-between items-start mb-4 relative z-10">
                                <span className="text-label-md font-label-md text-error uppercase tracking-wider font-bold">Aging Hub Inventory (&gt;3d)</span>
                                <div className="w-8 h-8 rounded-full bg-error text-on-error flex items-center justify-center">
                                    <span className="material-symbols-outlined text-[20px]">warning</span>
                                </div>
                            </div>
                            <div className="relative z-10">
                                <span className="text-display-lg font-display-lg text-error">{stats?.hubItemsWaitingOver3Days ?? '--'}</span>
                                <span className="text-label-md font-label-md text-error ml-2">shipments at risk</span>
                            </div>
                        </div>
                    </div>

                    {/* Charts Row */}
                    <div className="grid grid-cols-1 lg:grid-cols-2 gap-gutter mb-gutter">
                        {/* Line Chart: Performance */}
                        <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col h-[350px]">
                            <div className="flex justify-between items-center mb-4">
                                <h3 className="text-headline-md font-headline-md text-on-surface flex items-center gap-2">
                                    <span className="material-symbols-outlined text-primary">timeline</span> Hiệu Suất Ghép Chuyến (Trips Completed)
                                </h3>
                                <span className="text-xs text-on-surface-variant bg-surface-container px-2.5 py-1 rounded-full font-bold">Theo tháng</span>
                            </div>
                            <div className="flex-1 flex items-center justify-center p-2">
                                <svg viewBox="0 0 500 180" className="w-full h-full">
                                    <defs>
                                        <linearGradient id="line-grad" x1="0" y1="0" x2="0" y2="1">
                                            <stop offset="0%" stopColor="var(--md-sys-color-primary)" stopOpacity="0.3" />
                                            <stop offset="100%" stopColor="var(--md-sys-color-primary)" stopOpacity="0.0" />
                                        </linearGradient>
                                    </defs>
                                    <line x1="30" y1="20" x2="480" y2="20" stroke="var(--md-sys-color-outline-variant)" strokeOpacity="0.2" strokeDasharray="4" />
                                    <line x1="30" y1="65" x2="480" y2="65" stroke="var(--md-sys-color-outline-variant)" strokeOpacity="0.2" strokeDasharray="4" />
                                    <line x1="30" y1="110" x2="480" y2="110" stroke="var(--md-sys-color-outline-variant)" strokeOpacity="0.2" strokeDasharray="4" />
                                    <line x1="30" y1="150" x2="480" y2="150" stroke="var(--md-sys-color-outline)" strokeOpacity="0.5" />

                                    <path d="M 30 150 L 30 110 Q 110 70 190 120 T 350 50 Q 420 100 480 40 L 480 150 Z" fill="url(#line-grad)" />
                                    <path d="M 30 110 Q 110 70 190 120 T 350 50 Q 420 100 480 40" fill="none" stroke="var(--md-sys-color-primary)" strokeWidth="3" />

                                    <circle cx="30" cy="110" r="4" fill="var(--md-sys-color-primary)" stroke="var(--md-sys-color-surface)" strokeWidth="2" />
                                    <circle cx="142" cy="95" r="4" fill="var(--md-sys-color-primary)" stroke="var(--md-sys-color-surface)" strokeWidth="2" />
                                    <circle cx="255" cy="80" r="4" fill="var(--md-sys-color-primary)" stroke="var(--md-sys-color-surface)" strokeWidth="2" />
                                    <circle cx="368" cy="72" r="4" fill="var(--md-sys-color-primary)" stroke="var(--md-sys-color-surface)" strokeWidth="2" />
                                    <circle cx="480" cy="40" r="4" fill="var(--md-sys-color-primary)" stroke="var(--md-sys-color-surface)" strokeWidth="2" />

                                    <text x="30" y="170" fill="var(--md-sys-color-on-surface-variant)" fontSize="10" textAnchor="middle" fontWeight="bold">Tháng 2</text>
                                    <text x="142" y="170" fill="var(--md-sys-color-on-surface-variant)" fontSize="10" textAnchor="middle" fontWeight="bold">Tháng 3</text>
                                    <text x="255" y="170" fill="var(--md-sys-color-on-surface-variant)" fontSize="10" textAnchor="middle" fontWeight="bold">Tháng 4</text>
                                    <text x="368" y="170" fill="var(--md-sys-color-on-surface-variant)" fontSize="10" textAnchor="middle" fontWeight="bold">Tháng 5</text>
                                    <text x="480" y="170" fill="var(--md-sys-color-on-surface-variant)" fontSize="10" textAnchor="middle" fontWeight="bold">Tháng 6</text>
                                </svg>
                            </div>
                        </div>

                        {/* Bar Chart: Capacity Utilisation by Hub */}
                        <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col h-[350px]">
                            <div className="flex justify-between items-center mb-6">
                                <h3 className="text-headline-md font-headline-md text-on-surface flex items-center gap-2">
                                    <span className="material-symbols-outlined text-primary">bar_chart</span> Hiệu Suất Sử Dụng Xe Theo Kho (Hub Utilisation)
                                </h3>
                                <span className="text-xs text-on-surface-variant bg-surface-container px-2.5 py-1 rounded-full font-bold">Thời gian thực</span>
                            </div>
                            <div className="flex-1 flex flex-col justify-center space-y-5 px-2">
                                {hubsData.map((hub) => (
                                    <div key={hub.name} className="space-y-1.5">
                                        <div className="flex justify-between items-center text-body-md font-bold">
                                            <span className="text-slate-700">{hub.name}</span>
                                            <span className="text-primary">{hub.value}%</span>
                                        </div>
                                        <div className="w-full bg-slate-100 rounded-full h-3 overflow-hidden">
                                            <div 
                                                className={`${hub.color} h-3 rounded-full transition-all duration-500`} 
                                                style={{ width: `${hub.value}%` }}
                                            />
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </div>
                    </div>

                    {/* Middle Row: Donut Chart & Driver Fleet */}
                    <div className="grid grid-cols-1 lg:grid-cols-12 gap-gutter mb-gutter">
                        {/* Donut Chart: Trip Status */}
                        <div className="lg:col-span-5 bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col h-[350px]">
                            <div className="flex justify-between items-center mb-6">
                                <h3 className="text-headline-md font-headline-md text-on-surface flex items-center gap-2">
                                    <span className="material-symbols-outlined text-primary">pie_chart</span> Trạng Thái Chuyến Đi
                                </h3>
                            </div>
                            <div className="flex-1 flex flex-col sm:flex-row items-center gap-6 justify-center">
                                <div className="relative w-[130px] h-[130px]">
                                    <svg width="100%" height="100%" viewBox="0 0 36 36" className="transform -rotate-90">
                                        <circle cx="18" cy="18" r="15.915" fill="none" stroke="var(--md-sys-color-surface-container-high)" strokeWidth="3.5" />
                                        
                                        {/* Completed (65%) */}
                                        <circle cx="18" cy="18" r="15.915" fill="none" stroke="var(--md-sys-color-primary)" strokeWidth="3.5" 
                                            strokeDasharray="65 35" strokeDashoffset="0" />
                                            
                                        {/* Active (25%) */}
                                        <circle cx="18" cy="18" r="15.915" fill="none" stroke="var(--md-sys-color-secondary)" strokeWidth="3.5" 
                                            strokeDasharray="25 75" strokeDashoffset="-65" />
                                            
                                        {/* Breakdown (10%) */}
                                        <circle cx="18" cy="18" r="15.915" fill="none" stroke="var(--md-sys-color-error)" strokeWidth="3.5" 
                                            strokeDasharray="10 90" strokeDashoffset="-90" />
                                    </svg>
                                    <div className="absolute inset-0 flex flex-col items-center justify-center">
                                        <span className="text-headline-md font-bold text-slate-800">12</span>
                                        <span className="text-[9px] text-slate-500 font-bold uppercase tracking-wider">Tổng trips</span>
                                    </div>
                                </div>
                                <div className="space-y-2.5 text-body-md">
                                    <div className="flex items-center gap-2">
                                        <span className="w-3 h-3 rounded-full bg-primary" />
                                        <span className="font-bold text-slate-700">Đã hoàn thành: 65%</span>
                                    </div>
                                    <div className="flex items-center gap-2">
                                        <span className="w-3 h-3 rounded-full bg-secondary" />
                                        <span className="font-bold text-slate-700">Đang chạy: 25%</span>
                                    </div>
                                    <div className="flex items-center gap-2">
                                        <span className="w-3 h-3 rounded-full bg-error" />
                                        <span className="font-bold text-slate-700">Sự cố: 10%</span>
                                    </div>
                                </div>
                            </div>
                        </div>

                        {/* Active Fleet Logger */}
                        <div className="lg:col-span-7 bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col h-[350px]">
                            <div className="flex justify-between items-center mb-4 pb-2 border-b border-outline-variant/30">
                                <h3 className="text-headline-md font-headline-md text-on-surface flex items-center gap-2">
                                    <span className="material-symbols-outlined text-primary">local_shipping</span> Trạng Thái Đội Xe (Active Fleet Overview)
                                </h3>
                                <span className="text-label-md font-label-md text-on-surface-variant font-bold">4 hoạt động</span>
                            </div>
                            <div className="flex-1 overflow-y-auto pr-1 custom-scrollbar">
                                <table className="w-full text-left text-body-md">
                                    <thead>
                                        <tr className="border-b border-outline-variant/30 text-on-surface-variant font-bold text-label-md">
                                            <th className="py-2.5">Tài xế & Xe</th>
                                            <th className="py-2.5">Tuyến đường</th>
                                            <th className="py-2.5 text-right">Trạng thái</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {activeDrivers.map((driver) => (
                                            <tr key={driver.vehicle} className="border-b border-outline-variant/10 last:border-0 hover:bg-slate-50">
                                                <td className="py-3">
                                                    <div className="font-bold text-slate-800">{driver.name}</div>
                                                    <div className="text-xs text-slate-500 font-semibold">{driver.vehicle}</div>
                                                </td>
                                                <td className="py-3 text-slate-600 font-semibold text-body-sm">{driver.route}</td>
                                                <td className="py-3 text-right">
                                                    <span className={`px-2.5 py-1 rounded-full text-[11px] font-bold ${driver.color}`}>
                                                        {driver.status}
                                                    </span>
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    </div>

                    {/* Operational Alerts Section (Bottom) */}
                    <div className="grid grid-cols-1 lg:grid-cols-12 gap-gutter mb-8">
                        <div className="lg:col-span-12 bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col h-[350px]">
                            <div className="flex justify-between items-center mb-4 pb-2 border-b border-outline-variant/30">
                                <h3 className="text-headline-md font-headline-md text-on-surface flex items-center gap-2">
                                    <span className="material-symbols-outlined text-error">campaign</span> Operational Alerts (Cảnh Báo Vận Hành)
                                </h3>
                            </div>
                            <div className="flex-1 overflow-y-auto space-y-3 pr-2 custom-scrollbar">
                                {/* Overload warning */}
                                <div className="bg-surface-container p-3 rounded border-l-4 border-on-tertiary-container flex gap-3 items-start">
                                    <span className="material-symbols-outlined text-on-tertiary-container text-[20px] mt-0.5">warning</span>
                                    <div>
                                        <h4 className="text-label-lg font-bold text-on-surface">Cảnh Báo Vận Hành (Overload)</h4>
                                        <p className="text-body-md text-on-surface-variant mt-1">
                                            Xe tải của tài xế <strong>Phạm Minh Chiến (51C-998.76)</strong> tại Kho Gò Vấp vượt quá tải trọng cho phép (105%). Đề xuất ghép chuyến đã bị tạm ngưng để kiểm tra.
                                        </p>
                                    </div>
                                </div>

                                {/* Vehicle breakdown incident */}
                                <div className="bg-surface-container p-3 rounded border-l-4 border-error flex gap-3 items-start">
                                    <span className="material-symbols-outlined text-error text-[20px] mt-0.5">report</span>
                                    <div>
                                        <h4 className="text-label-lg font-bold text-error">Sự Cố Kỹ Thuật (Breakdown)</h4>
                                        <p className="text-body-md text-on-surface-variant mt-1">
                                            Tài xế <strong>Hoàng Văn Hải (Xe Container 29C-555.22)</strong> báo cáo sự cố nổ lốp trên Quốc Lộ 1A. Điều phối viên cần liên hệ đội cứu hộ và điều chuyển hàng hóa.
                                        </p>
                                    </div>
                                </div>

                                {/* Success trip completion */}
                                <div className="bg-surface-container p-3 rounded border-l-4 border-secondary flex gap-3 items-start">
                                    <span className="material-symbols-outlined text-secondary text-[20px] mt-0.5">check_circle</span>
                                    <div>
                                        <h4 className="text-label-lg font-bold text-on-surface">Hoàn Thành Chuyến Đi</h4>
                                        <p className="text-body-md text-on-surface-variant mt-1">
                                            Tài xế <strong>Ngô Quốc Bảo (51D-123.45)</strong> đã hoàn thành chuyến ghép hàng <strong>TRIP-049</strong> về Kho Tân Bình thành công và an toàn.
                                        </p>
                                    </div>
                                </div>

                                {/* Socket listening message */}
                                <div className="bg-surface-container p-3 rounded border-l-4 border-outline flex gap-3 items-start opacity-70">
                                    <span className="material-symbols-outlined text-outline text-[20px] mt-0.5">info</span>
                                    <div>
                                        <h4 className="text-label-lg font-label-lg text-on-surface">Kết Nối Hệ Thống</h4>
                                        <p className="text-body-md text-on-surface-variant mt-1">Đang lắng nghe tín hiệu thời gian thực (SignalR socket) để cập nhật danh mục cảnh báo tiếp theo.</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </main>
            </div>
        </div>
    );
}