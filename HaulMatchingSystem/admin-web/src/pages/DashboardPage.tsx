import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';

interface AdminStats {
    activeTripCount: number;
    inTransitShipments: number;
    avgVehicleUtilisation: number;
    hubItemsWaitingOver3Days: number;
    lastUpdated: string;
}

export default function DashboardPage() {
    const [stats, setStats] = useState<AdminStats | null>(null);
    const [connectionStatus, setConnectionStatus] = useState<string>('Đang kết nối...');

    useEffect(() => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("https://localhost:7059/hub/fleet") // CHÚ Ý: Đổi port nếu backend chạy cổng khác
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
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

    return (
        <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden">
            {/* --- SIDEBAR --- */}
            <nav className="bg-surface-container-lowest border-r border-outline-variant fixed left-0 h-full w-64 flex flex-col py-6 px-4 z-20 hidden xl:flex">
                <div className="mb-8 flex items-center gap-3 px-2">
                    <div className="w-8 h-8 rounded bg-primary flex items-center justify-center text-on-primary">
                        <span className="material-symbols-outlined text-[20px]">local_shipping</span>
                    </div>
                    <div>
                        <h1 className="text-headline-lg font-headline-lg text-primary">Ghép Chuyến</h1>
                        <p className="text-label-md font-label-md text-on-surface-variant">Logistics Console</p>
                    </div>
                </div>
                <button className="w-full bg-primary hover:bg-primary-container text-on-primary text-label-lg font-label-lg py-3 rounded-lg mb-6 transition-colors flex items-center justify-center gap-2">
                    <span className="material-symbols-outlined">add</span> New Shipment
                </button>
                <div className="flex-1 space-y-1">
                    <a className="flex items-center gap-3 px-3 py-2 rounded-lg text-primary font-bold border-r-4 border-primary bg-surface-container-low transition-colors duration-200 group" href="#">
                        <span className="material-symbols-outlined group-hover:scale-98 transition-transform">dashboard</span>
                        <span className="text-label-lg font-label-lg">Dashboard</span>
                    </a>
                </div>
            </nav>

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
                        <div className="w-8 h-8 rounded-full bg-surface-variant overflow-hidden border border-outline-variant/50 ml-2">
                            <img alt="User Avatar" className="w-full h-full object-cover" src="https://lh3.googleusercontent.com/aida/AP1WRLsAnOAwTMZ6WYlncjkQ1wDt3lh-5zxXSSS8JggN1WvIsN8EuRxgEIdFOjF9I1_IhNz75PX8mHdibja7ELV4_3v0bVkMJLEaGeaaQVolYGiFyeLnJ13AmHloSfAL5cv_9FiHGaCngsQzKDui3CrqaSwov1bKnbVGvda30ObggYAs_Di8Q_l54hEDSewYFtlGK4wk_bc_l7fKJoXtxssZT84eHh3fzZ9XbCLzIBt0x9yzQRo1g6lxMUhRoag" />
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
                            <span className="text-display-lg font-display-lg text-on-surface">{stats?.activeTripCount || '--'}</span>
                        </div>

                        <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col justify-between">
                            <div className="flex justify-between items-start mb-4">
                                <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">In-Transit Shipments</span>
                                <div className="w-8 h-8 rounded-full bg-surface-container-high flex items-center justify-center text-primary relative">
                                    <span className="material-symbols-outlined text-[20px] relative z-10">package_2</span>
                                    <span className="absolute inset-0 bg-primary/20 rounded-full live-pulse"></span>
                                </div>
                            </div>
                            <span className="text-display-lg font-display-lg text-on-surface">{stats?.inTransitShipments || '--'}</span>
                        </div>

                        <div className="bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col justify-between">
                            <div className="flex justify-between items-start mb-2">
                                <span className="text-label-md font-label-md text-on-surface-variant uppercase tracking-wider">Vehicle Utilisation</span>
                                <span className="text-headline-md font-headline-md text-on-tertiary-container">{stats?.avgVehicleUtilisation || 0}%</span>
                            </div>
                            <div className="w-full bg-surface-container-high rounded-full h-2 mb-3 overflow-hidden">
                                <div className="bg-on-tertiary-container h-2 rounded-full transition-all duration-500" style={{ width: `${stats?.avgVehicleUtilisation || 0}%` }}></div>
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
                                <span className="text-display-lg font-display-lg text-error">{stats?.hubItemsWaitingOver3Days || '--'}</span>
                                <span className="text-label-md font-label-md text-error ml-2">shipments at risk</span>
                            </div>
                        </div>
                    </div>

                    {/* Operational Alerts Section (Bottom) */}
                    <div className="grid grid-cols-1 lg:grid-cols-12 gap-gutter mb-8">
                        <div className="lg:col-span-12 bg-surface-container-lowest rounded-xl p-card-padding card-shadow border border-outline-variant/20 flex flex-col h-[350px]">
                            <div className="flex justify-between items-center mb-4 pb-2 border-b border-outline-variant/30">
                                <h3 className="text-headline-md font-headline-md text-on-surface flex items-center gap-2">
                                    <span className="material-symbols-outlined text-error">campaign</span> Operational Alerts
                                </h3>
                            </div>
                            <div className="flex-1 overflow-y-auto space-y-3 pr-2 custom-scrollbar">
                                <div className="bg-surface-container p-3 rounded border-l-4 border-outline flex gap-3 items-start opacity-70">
                                    <span className="material-symbols-outlined text-outline text-[20px] mt-0.5">info</span>
                                    <div>
                                        <h4 className="text-label-lg font-label-lg text-on-surface">System Running</h4>
                                        <p className="text-body-md font-body-md text-on-surface-variant mt-1">Real-time socket listening for anomaly alerts.</p>
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