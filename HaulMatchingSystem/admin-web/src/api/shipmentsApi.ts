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

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7059";

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