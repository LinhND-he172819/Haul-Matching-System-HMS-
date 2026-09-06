import { authFetch } from '../utils/authFetch';

const API_BASE =
    import.meta.env.VITE_API_BASE_URL ??
    import.meta.env.VITE_API_URL ??
    'http://localhost:5104';

export interface HubDto {
    id: string;
    name: string;
    address: string;
}

export interface CreateUserPayload {
    fullName: string;
    phone: string;
    email: string;
    password: string;
    hubId?: string | null;
    role: string;
}

export type UpdateUserPayload = Omit<CreateUserPayload, 'password'> & {
    password?: string;
};

export async function fetchHubs(): Promise<HubDto[]> {
    const res = await authFetch(`${API_BASE}/api/identity/hubs`);
    if (!res.ok) {
        throw new Error(`Tải danh sách Hub thất bại: ${res.status}`);
    }
    return await res.json();
}

export async function createUser(payload: CreateUserPayload): Promise<any> {
    const res = await authFetch(`${API_BASE}/api/identity/users`, {
        method: 'POST',
        body: JSON.stringify(payload),
    }, { includeJson: true });

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
    hubId?: string | null;
    createdAt: string;
    licensePlate?: string;
    truckType?: string;
    maxWeightKg?: number;
    maxVolumeCbm?: number;
}

export async function fetchUsers(): Promise<UserDto[]> {
    const res = await authFetch(`${API_BASE}/api/identity/users`);
    if (!res.ok) {
        throw new Error(`Tải danh sách tài khoản thất bại: ${res.status}`);
    }
    return await res.json();
}

export async function updateUser(id: string, payload: UpdateUserPayload): Promise<any> {
    const res = await authFetch(`${API_BASE}/api/identity/users/${id}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
    }, { includeJson: true });

    if (!res.ok) {
        let errorData;
        try {
            errorData = await res.json();
        } catch {
            throw new Error(`Cập nhật tài khoản thất bại với mã lỗi: ${res.status}`);
        }
        throw new Error(errorData.Message || errorData.message || JSON.stringify(errorData));
    }
    return await res.json();
}

export async function deleteUser(id: string): Promise<any> {
    const res = await authFetch(`${API_BASE}/api/identity/users/${id}`, {
        method: 'DELETE',
    });

    if (!res.ok) {
        let errorData;
        try {
            errorData = await res.json();
        } catch {
            throw new Error(`Xóa tài khoản thất bại với mã lỗi: ${res.status}`);
        }
        throw new Error(errorData.Message || errorData.message || JSON.stringify(errorData));
    }
    return await res.json();
}
