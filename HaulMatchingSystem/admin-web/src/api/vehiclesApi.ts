export type VehicleStatus = 'Available' | 'InMaintenance' | 'Inactive';

export type Vehicle = {
    id: string;
    code: string;
    licensePlate: string;
    hubId: string;
    vehicleType: string;
    maxWeightKg: number;
    maxVolumeCbm: number;
    status: VehicleStatus;
    createdAt: string;
    updatedAt: string;
};

export type VehiclePayload = {
    code: string;
    licensePlate: string;
    hubId: string;
    vehicleType: string;
    maxWeightKg: number;
    maxVolumeCbm: number;
    status: VehicleStatus;
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5104';

async function readApiError(response: Response, fallback: string) {
    const text = await response.text();
    if (!text) {
        return fallback;
    }

    try {
        const body = JSON.parse(text) as { title?: string; detail?: string; errors?: Record<string, string[]> };
        const validationMessage = body.errors ? Object.values(body.errors).flat().join(' ') : '';

        return validationMessage || body.detail || body.title || fallback;
    } catch {
        return text;
    }
}

export async function fetchVehicles(search = '', status = ''): Promise<Vehicle[]> {
    const url = new URL(`${API_BASE_URL}/api/vehicles`);
    if (search.trim()) {
        url.searchParams.set('search', search.trim());
    }

    if (status.trim()) {
        url.searchParams.set('status', status.trim());
    }

    const response = await fetch(url);
    if (!response.ok) {
        throw new Error(await readApiError(response, `Cannot load vehicles (${response.status}).`));
    }

    return response.json();
}

export async function createVehicle(payload: VehiclePayload): Promise<Vehicle> {
    const response = await fetch(`${API_BASE_URL}/api/vehicles`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });

    if (!response.ok) {
        throw new Error(await readApiError(response, `Cannot create vehicle (${response.status}).`));
    }

    return response.json();
}

export async function updateVehicle(id: string, payload: VehiclePayload): Promise<Vehicle> {
    const response = await fetch(`${API_BASE_URL}/api/vehicles/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });

    if (!response.ok) {
        throw new Error(await readApiError(response, `Cannot update vehicle (${response.status}).`));
    }

    return response.json();
}
