import { useState, useEffect } from 'react';
import { db, OfflineActionType, type OfflineAction } from '../services/offlineDb';
import { v4 as uuidv4 } from 'uuid';

export function useNetworkSync() {
  // Trình duyệt tự biết trạng thái mạng hiện tại
  const [isOnline, setIsOnline] = useState(navigator.onLine);

  useEffect(() => {
    const handleOnline = async () => {
      setIsOnline(true);
      console.log("🟢 Đã có mạng. Bắt đầu đồng bộ dữ liệu...");
      
      // 1. Lấy toàn bộ hàng đợi đang lưu local
      const pendingActions = await db.offlineQueue.orderBy('deviceTimestamp').toArray();
      
      if (pendingActions.length > 0) {
        try {
          // 2. Gửi cục batch này lên API của Backend
          const response = await fetch('https://localhost:7059/api/transport/sync-offline', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              'Authorization': `Bearer ${localStorage.getItem("jwt_token") || ""}`
            },
            body: JSON.stringify(pendingActions)
          });
          if (response.ok) {
            console.log(`✅ Đồng bộ thành công ${pendingActions.length} hành động.`);
            // 3. Xóa dữ liệu cục bộ sau khi Server xác nhận (HTTP 200/202)
            await db.offlineQueue.clear();
          }
        } catch (error) {
          console.error("Lỗi đồng bộ, sẽ thử lại sau:", error);
        }
      }
    };

    const handleOffline = () => {
      setIsOnline(false);
      console.log("🔴 Mất mạng. Chuyển sang Offline Mode.");
    };

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, []);

  // Hàm dành cho UI gọi khi muốn gửi GPS hoặc Xác nhận giao hàng
  const safePushAction = async (actionType: OfflineActionType, data: unknown) => {
    const payloadObject: OfflineAction = {
      id: uuidv4(), // Tạo UUID (idempotency_key) chống trùng lặp
      actionType,
      payload: JSON.stringify(data),
      deviceTimestamp: new Date().toISOString()
    };

    if (isOnline) {
      // Nếu có mạng: Bắn thẳng API (hoặc SignalR)
      console.log(`[Online] Đẩy trực tiếp: ${actionType}`);
      // await axios.post('/api/...', payloadObject);
    } else {
      // Nếu mất mạng: Nhét vào IndexedDB
      await db.offlineQueue.add(payloadObject);
      console.log(`[Offline] Đã lưu vào hàng đợi cục bộ: ${actionType}`);
    }
  };

  return { isOnline, safePushAction };
}