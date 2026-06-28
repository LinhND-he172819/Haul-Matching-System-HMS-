export type CreateDraftShipmentRequest = {
  customerId: string;
  cargoType: string;
  weightKg: number;
  volumeCbm: number;
  receiverName: string;
  receiverPhone: string;
  destAddress: string;
  destLat: number;
  destLng: number;
  specialHandlingNote?: string;
};

export type DraftShipmentResponse = {
  id: string;
  qrCode: string;
  status: string;
  createdAt: string;
};

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  "http://localhost:5104";

export async function createDraftShipment(
  payload: CreateDraftShipmentRequest
): Promise<DraftShipmentResponse> {
  const res = await fetch(`${API_BASE_URL}/api/shipments/draft`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(payload),
  });

  if (!res.ok) {
    const message = await res.text();
    throw new Error(message || "Không thể tạo đơn hàng.");
  }

  return res.json();
}
export type GeocodeResponse = {
  lat: number;
  lng: number;
  displayName: string;
};

export async function geocodeAddress(address: string): Promise<GeocodeResponse> {
  const res = await fetch(`${API_BASE_URL}/api/geocoding/search`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ address }),
  });

  if (!res.ok) {
    throw new Error("Không tìm thấy địa chỉ.");
  }

  return res.json();
}

export type ShipmentQrLookupResponse = {
  id: string;
  qrCode: string;
  cargoType: string;
  weightKg: number;
  volumeCbm: number;
  receiverName: string;
  receiverPhone: string;
  destAddress: string;
  status: string;
  specialHandlingNote?: string;
};

export type ConfirmIntakeRequest = {
  actualWeightKg: number;
  actualVolumeCbm: number;
};

export async function getShipmentByQrCode(
  qrCode: string
): Promise<ShipmentQrLookupResponse> {
  const res = await fetch(`${API_BASE_URL}/api/shipments/qr/${qrCode}`);

  if (!res.ok) {
    throw new Error("Không tìm thấy đơn hàng theo mã QR.");
  }

  return res.json();
}

export async function confirmShipmentIntake(
  shipmentId: string,
  payload: ConfirmIntakeRequest
): Promise<{ message: string; status: string }> {
  const token = localStorage.getItem("accessToken");

  const res = await fetch(
    `${API_BASE_URL}/api/shipments/${shipmentId}/confirm-intake`,
    {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify(payload),
    }
  );

  if (!res.ok) {
    const message = await res.text();
    throw new Error(message || "Nhập kho thất bại.");
  }

  return res.json();
}