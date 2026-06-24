export type Hub = {
    id: string;
    name: string;
    address: string;
    latitude: number;
    longitude: number;
    createdAt: string;
    updatedAt: string;
};

export type HubPayload = {
    name: string;
    address: string;
    latitude: number;
    longitude: number;
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

export async function fetchHubs(search = ''): Promise<Hub[]> {
    const url = new URL(`${API_BASE_URL}/api/hubs`);
    if (search.trim()) {
        url.searchParams.set('search', search.trim());
    }

    const response = await fetch(url);
    if (!response.ok) {
        throw new Error(await readApiError(response, `Cannot load hubs (${response.status}).`));
    }

    return response.json();
}

export async function createHub(payload: HubPayload): Promise<Hub> {
    const response = await fetch(`${API_BASE_URL}/api/hubs`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });

    if (!response.ok) {
        throw new Error(await readApiError(response, `Cannot create hub (${response.status}).`));
    }

    return response.json();
}

export async function updateHub(id: string, payload: HubPayload): Promise<Hub> {
    const response = await fetch(`${API_BASE_URL}/api/hubs/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });

    if (!response.ok) {
        throw new Error(await readApiError(response, `Cannot update hub (${response.status}).`));
    }

    return response.json();
}
