import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';

// Định nghĩa Interface chuẩn theo dữ liệu thật từ Backend
interface ActiveTrip {
    id: string;          // TripId (GUID)
    vehicleId: string;
    driverId: string;
    driverName?: string;  // Có thể lấy qua liên kết bảng
    licensePlate?: string; // Có thể lấy qua liên kết bảng
    status: string;
    currentLat?: number;
    currentLng?: number;
}

interface AdminLiveMapPageProps {
    sidebar?: React.ReactNode;
}

const apiBaseUrl =
    import.meta.env.VITE_API_BASE_URL ??
    import.meta.env.VITE_API_URL ??
    'http://localhost:5104';

export default function AdminLiveMapPage({ sidebar }: AdminLiveMapPageProps) {
    // Thay thế mảng mock bằng State lưu danh sách chuyến đi thật từ DB
    const [activeTrips, setActiveTrips] = useState<ActiveTrip[]>([]);
    const [offlineTripIds, setOfflineTripIds] = useState<Set<string>>(new Set());
    
    
    const [loading, setLoading] = useState(true);
    const [connectionStatus, setConnectionStatus] = useState<string>('Đang kết nối...');

    // 1. LUỒNG LẤY DỮ LIỆU THẬT KHI MỞ TRANG (REST API)
    const loadActiveTrips = async () => {
        try {
            setLoading(true);
            // Gọi lên Endpoint lấy các chuyến đang chạy của module Transport
            const response = await fetch(`${apiBaseUrl}/api/transport/active-trips`, {
                headers: {
                    'Authorization': `Bearer ${localStorage.getItem('accessToken')}`
                }
            });
            if (response.ok) {
                const data = await response.json();
                setActiveTrips(data);
            }
        } catch (error) {
            console.error("Lỗi tải danh sách xe trực tuyến:", error);
        } finally {
            setLoading(false);
        }
    };

    // 2. LUỒNG LẮNG NGHE TÍN HIỆU THỜI GIAN THỰC (SIGNALR)
    useEffect(() => {
        // Tải dữ liệu ban đầu từ DB trước
        loadActiveTrips();

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(apiBaseUrl + "/hub/fleet", {
                accessTokenFactory: () => localStorage.getItem("accessToken") || ""
            })
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        // Bắt sự kiện xe mất sóng từ con FleetMonitorWorker bên C#
        connection.on("ReceiveVehicleAlert", (alertData: any) => {
            // SỬA LỖI CHÍ MẠNG: Đổi alertData.vehicleId thành alertData.tripId cho khớp với Backend Payload
            if (alertData.alertType === "Signal_Loss" || alertData.alertType === 0) {
                setOfflineTripIds(prev => new Set(prev).add(alertData.tripId));
            }
        });

        // Bắt thêm sự kiện GPS Ping để nếu xe có mạng lại -> Xóa trạng thái mất sóng ngay lập tức
        connection.on("ReceiveGpsPing", (pingData: any) => {
            setOfflineTripIds(prev => {
                const next = new Set(prev);
                next.delete(pingData.tripId);
                return next;
            });
            
            // Cập nhật vị trí xe chạy thời gian thực trên màn hình Admin
            setActiveTrips(prevTrips => 
                prevTrips.map(t => t.id === pingData.tripId 
                    ? { ...t, currentLat: pingData.lat, currentLng: pingData.lng } 
                    : t
                )
            );
        });

        const startConnection = async () => {
            try {
                await connection.start();
                setConnectionStatus('Connected');
            } catch (err) {
                console.error("Lỗi kết nối Hub:", err);
                setConnectionStatus('Lỗi kết nối');
            }
        };

        startConnection();
        return () => { connection.stop(); };
    }, []);

    return (
        <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden">
            {sidebar}

            <div className="flex-1 flex flex-col xl:ml-64 w-full">
                <header className="bg-surface-container-lowest border-b border-outline-variant h-16 w-full flex justify-between items-center px-8 sticky top-0 z-10">
                    <h2 className="text-headline-sm font-bold">Live Fleet Map</h2>
                    <div className="flex items-center gap-6">
                        <div className="sm:flex items-center gap-2 text-label-md font-label-md text-on-surface-variant bg-surface px-3 py-1 rounded-full border border-outline-variant/30">
                            <span className={`w-2 h-2 rounded-full ${connectionStatus === 'Connected' ? 'bg-secondary live-pulse' : 'bg-error'}`}></span>
                            {connectionStatus}
                        </div>
                    </div>
                </header>

                <main className="flex-1 p-8 overflow-y-auto bg-surface-container-lowest">
                    <div className="mb-6 flex justify-between items-center">
                        <div>
                            <h2 className="text-headline-lg font-headline-lg text-on-surface mb-2">Giám sát xe trực tuyến</h2>
                            <p className="text-body-md text-on-surface-variant">
                                Dữ liệu thực từ Database kết hợp tín hiệu WebSocket SignalR.
                            </p>
                        </div>
                        <button 
                            onClick={loadActiveTrips}
                            className="px-4 py-2 bg-surface-container border border-outline-variant rounded-lg text-xs font-bold hover:bg-surface-container-high transition-all flex items-center gap-1"
                        >
                            <span className="material-symbols-outlined text-sm">refresh</span> Làm tươi danh sách
                        </button>
                    </div>

                    {loading ? (
                        <div className="p-10 text-center text-slate-500 font-medium">Đang quét danh sách đội xe từ hệ thống...</div>
                    ) : activeTrips.length === 0 ? (
                        <div className="p-16 border-2 border-dashed border-outline-variant/60 rounded-xl text-center text-slate-400 italic">
                            Hiện không có chuyến xe (Trip) nào đang ở trạng thái hoạt động ('Active') dưới Database.
                        </div>
                    ) : (
                        /* Vùng bản đồ hiển thị xe thật */
                        <div className="relative w-full min-h-[500px] bg-slate-100 rounded-xl border border-outline-variant/30 overflow-hidden card-shadow p-6 flex flex-wrap gap-6 items-start">
                            {activeTrips.map((trip) => {
                                // Kiểm tra xem TripId này có nằm trong danh sách đen mất sóng không
                                const isSignalLost = offlineTripIds.has(trip.id);

                                return (
                                    <div key={trip.id} className="relative bg-white p-4 rounded-xl border border-outline-variant/50 shadow-sm w-64 transition-all">
                                        
                                        {/* ALERT BADGE CHUẨN KỊCH BẢN EX-04b */}
                                        {isSignalLost && (
                                            <div className="absolute -top-2 -right-2 bg-red-600 text-white text-xs font-bold w-6 h-6 rounded-full flex items-center justify-center shadow-md animate-pulse z-10">
                                                !
                                            </div>
                                        )}

                                        <div className="flex items-center gap-3 mb-3">
                                            <div className={`w-10 h-10 rounded-full flex items-center justify-center ${isSignalLost ? 'bg-slate-200 text-slate-500' : 'bg-[#1b39b7]/10 text-[#1b39b7]'}`}>
                                                <span className="material-symbols-outlined">local_shipping</span>
                                            </div>
                                            <div>
                                                <div className="font-bold text-slate-800">{trip.licensePlate ?? 'Chưa rõ biển số'}</div>
                                                <div className="text-xs text-slate-500 font-semibold">{trip.driverName ?? `Tài xế ID: ${trip.driverId.substring(0,6)}`}</div>
                                            </div>
                                        </div>
                                        
                                        <div className="text-[11px] font-mono bg-slate-50 p-2 rounded text-slate-500 mb-2">
                                            Trip: ...{trip.id.substring(24)}
                                        </div>

                                        <div className="border-t border-outline-variant/20 pt-2 mt-2">
                                            {isSignalLost ? (
                                                <div className="flex items-center gap-1 text-red-600 text-xs font-bold">
                                                    <span className="material-symbols-outlined text-sm">wifi_off</span>
                                                    MẤT TÍN HIỆU ({'>'} 3 phút)
                                                </div>
                                            ) : (
                                                <div className="flex items-center gap-1 text-emerald-600 text-xs font-bold">
                                                    <span className="material-symbols-outlined text-sm">wifi</span>
                                                    ĐANG HOẠT ĐỘNG
                                                </div>
                                            )}
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    )}
                </main>
            </div>
        </div>
    );
}