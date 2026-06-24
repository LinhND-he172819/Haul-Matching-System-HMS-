import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';

interface AdminLiveMapPageProps {
    sidebar?: React.ReactNode;
}

export default function AdminLiveMapPage({ sidebar }: AdminLiveMapPageProps) {
    // State lưu danh sách xe bị mất sóng (từ SignalR)
    const [offlineVehicles, setOfflineVehicles] = useState<Set<string>>(new Set());
    const [connectionStatus, setConnectionStatus] = useState<string>('Đang kết nối...');

    // Mock data đội xe để test UI
    const fleet = [
        { id: 'V01', licensePlate: '51C-123.45', driver: 'Nguyễn Văn Hùng', lat: 10.8231, lng: 106.6297 },
        { id: 'V02', licensePlate: '51C-998.76', driver: 'Phạm Minh Chiến', lat: 10.7626, lng: 106.6602 },
        { id: 'V03', licensePlate: '29C-555.22', driver: 'Hoàng Văn Hải', lat: 21.0285, lng: 105.8542 },
    ];

    useEffect(() => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl((import.meta.env.VITE_API_URL ?? "https://localhost:7059") + "/hub/fleet", {
                accessTokenFactory: () => localStorage.getItem("jwt_token") || ""
            })
            .withAutomaticReconnect()
            .build();

        // Bắt sự kiện xe mất sóng từ con FleetMonitorWorker bên C#
        connection.on("ReceiveVehicleAlert", (alertData) => {
            if (alertData.alertType === "Signal_Loss") {
                setOfflineVehicles(prev => new Set(prev).add(alertData.vehicleId));
            }
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

    return (
        <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden">
            {/* --- SIDEBAR TRUYỀN TỪ APP.TSX SANG --- */}
            {sidebar}

            {/* --- MAIN CONTENT --- */}
            <div className="flex-1 flex flex-col xl:ml-64 w-full">
                
                {/* Giữ lại Header giống hệt Dashboard */}
                <header className="bg-surface-container-lowest border-b border-outline-variant h-16 w-full flex justify-between items-center px-8 sticky top-0 z-10">
                    <h2 className="text-headline-sm font-bold">Live Fleet Map</h2>
                    <div className="flex items-center gap-6">
                        <div className="hidden sm:flex items-center gap-2 text-label-md font-label-md text-on-surface-variant bg-surface px-3 py-1 rounded-full border border-outline-variant/30">
                            <span className={`w-2 h-2 rounded-full ${connectionStatus === 'Connected' ? 'bg-secondary live-pulse' : 'bg-error'}`}></span>
                            {connectionStatus}
                        </div>
                    </div>
                </header>

                <main className="flex-1 p-container-margin overflow-y-auto bg-surface-container-lowest">
                    
                    <div className="mb-6">
                        <h2 className="text-headline-lg font-headline-lg text-on-surface mb-2">Giám sát xe trực tuyến</h2>
                        <p className="text-body-md text-on-surface-variant">
                            Mô phỏng bản đồ vệ tinh. Khi Worker phát hiện mất sóng quá 3 phút, xe sẽ chuyển màu xám.
                        </p>
                    </div>

                    {/* Vùng bản đồ giả lập */}
                    <div className="relative w-full h-[600px] bg-slate-100 rounded-xl border border-outline-variant/30 overflow-hidden card-shadow p-6 flex flex-wrap gap-6 items-start">
                        
                        {fleet.map((vehicle) => {
                            const isSignalLost = offlineVehicles.has(vehicle.id);

                            return (
                                <div key={vehicle.id} className="relative bg-white p-4 rounded-xl border border-outline-variant/50 shadow-sm w-64">
                                    
                                    {/* ALERT BADGE (Task EX-04b yêu cầu) */}
                                    {isSignalLost && (
                                        <div className="absolute -top-3 -right-3 bg-error text-white text-xs font-bold w-6 h-6 rounded-full flex items-center justify-center shadow-md animate-pulse">
                                            !
                                        </div>
                                    )}

                                    <div className="flex items-center gap-3 mb-3">
                                        <div className={`w-10 h-10 rounded-full flex items-center justify-center ${isSignalLost ? 'bg-slate-200 text-slate-500' : 'bg-primary-fixed text-primary'}`}>
                                            <span className="material-symbols-outlined">local_shipping</span>
                                        </div>
                                        <div>
                                            <div className="font-bold text-slate-800">{vehicle.licensePlate}</div>
                                            <div className="text-xs text-slate-500">{vehicle.driver}</div>
                                        </div>
                                    </div>
                                    
                                    <div className="border-t border-outline-variant/20 pt-2 mt-2">
                                        {isSignalLost ? (
                                            <div className="flex items-center gap-1 text-error text-sm font-bold">
                                                <span className="material-symbols-outlined text-[16px]">wifi_off</span>
                                                Signal Lost ({'>'} 3 min)
                                            </div>
                                        ) : (
                                            <div className="flex items-center gap-1 text-secondary text-sm font-bold">
                                                <span className="material-symbols-outlined text-[16px]">wifi</span>
                                                Active Online
                                            </div>
                                        )}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </main>
            </div>
        </div>
    );
}