import type { Table } from 'dexie';
import Dexie from 'dexie';

export enum OfflineActionType {
  GPS_PING = 'GpsPing', // Giá trị này phải khớp chính xác  tên biến bên C#
  DELIVERY_CONFIRM = 'DeliveryConfirm'
}

export interface OfflineAction {
  id: string; // Đóng vai trò là idempotency_key
  actionType: OfflineActionType;
  payload: string; // Dữ liệu JSON (Tọa độ, Shipment ID...)
  deviceTimestamp: string;
}

export class HmsLocalDatabase extends Dexie {
  offlineQueue!: Table<OfflineAction>; 

  constructor() {
    super('HMS_OfflineDB');
    // Khai báo bảng offlineQueue với khóa chính là 'id' và đánh index cho 'deviceTimestamp'
    this.version(1).stores({
      offlineQueue: 'id, actionType, deviceTimestamp' 
    });
  }
}

export const offlineDb = new HmsLocalDatabase();