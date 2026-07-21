import { authFetch } from '../utils/authFetch';

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

// ── Hub Intake: QR lookup & Confirm Intake types ──

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

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ??
  import.meta.env.VITE_API_URL ??
  "http://localhost:5104";

// ── Existing: Create Draft ──

export async function createDraftShipment(
  payload: CreateDraftShipmentRequest
): Promise<DraftShipmentResponse> {
  const res = await authFetch(`${API_BASE_URL}/api/shipments/draft`, {
    method: "POST",
    body: JSON.stringify(payload),
  }, { includeJson: true });

  if (!res.ok) {
    const message = await res.text();
    throw new Error(message || "Không thể tạo đơn hàng.");
  }

  return res.json();
}

// ── Hub Intake: Get shipment by QR code ──

export async function getShipmentByQrCode(
  qrCode: string
): Promise<ShipmentQrLookupResponse> {
  const res = await authFetch(
    `${API_BASE_URL}/api/shipments/qr/${encodeURIComponent(qrCode)}`
  );

  if (!res.ok) {
    let message = "Không tìm thấy đơn hàng theo mã QR.";

    try {
      const error = await res.json();
      message = error.message ?? message;
    } catch {
      // Giữ message mặc định
    }

    throw new Error(message);
  }

  return res.json();
}

// ── Hub Intake: Confirm intake ──

export async function confirmShipmentIntake(
  shipmentId: string,
  payload: ConfirmIntakeRequest
): Promise<{
  message: string;
  status: string;
  currentHubId?: string;
  intakeConfirmedBy?: string;
  intakeConfirmedAt?: string;
}> {
  const res = await authFetch(
    `${API_BASE_URL}/api/shipments/${shipmentId}/confirm-intake`,
    {
      method: "PUT",
      body: JSON.stringify(payload),
    },
    { includeJson: true }
  );

  if (!res.ok) {
    let message = "Nhập kho thất bại.";

    try {
      const error = await res.json();
      message = error.message ?? message;
    } catch {
      const text = await res.text();
      if (text) message = text;
    }

    throw new Error(message);
  }

  return res.json();
}

// ── Geocoding ──

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
