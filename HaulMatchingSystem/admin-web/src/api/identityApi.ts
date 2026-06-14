const API_BASE = import.meta.env.VITE_API_URL ?? 'https://localhost:7059';

export interface HubDto {
    id: string;
    name: string;
    address: string;
}

export interface CreateUserPayload {
    fullName: string;
    phone: string;
    email: string;
    passwordHash?: string; // We can hash on backend, so we send plain password as `password`
    password?: string;
    hubId?: string | null;
    role: string;
    licensePlate?: string;
    truckType?: string;
    maxWeightKg?: number;
    maxVolumeCbm?: number;
}

export async function fetchHubs(): Promise<HubDto[]> {
    const res = await fetch(`${API_BASE}/api/identity/hubs`, { credentials: 'include' });
    if (!res.ok) {
        throw new Error(`Tải danh sách Hub thất bại: ${res.status}`);
    }
    return await res.json();
}

export async function createUser(payload: CreateUserPayload): Promise<any> {
    const res = await fetch(`${API_BASE}/api/identity/users`, {
        method: 'POST',
        credentials: 'include',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(payload)
    });

    if (!res.ok) {
        let errorData;
        try {
            errorData = await res.json();
        } catch {
            throw new Error(`Đăng ký tài khoản thất bại với mã lỗi: ${res.status}`);
        }
        throw new Error(errorData.Message || errorData.message || JSON.stringify(errorData));
    }
    
    return await res.json();
}

export interface UserDto {
    id: string;
    fullName: string;
    email?: string;
    phone?: string;
    role: string;
    hubId?: string;
    createdAt: string;
    licensePlate?: string;
    truckType?: string;
    maxWeightKg?: number;
    maxVolumeCbm?: number;
}

export async function fetchUsers(): Promise<UserDto[]> {
    const res = await fetch(`${API_BASE}/api/identity/users`, { credentials: 'include' });
    if (!res.ok) {
        throw new Error(`Tải danh sách tài khoản thất bại: ${res.status}`);
    }
    return await res.json();
}
