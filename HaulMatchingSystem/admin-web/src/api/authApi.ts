const API_BASE = import.meta.env.VITE_API_URL ?? 'https://localhost:7059';

export interface LoginRequest {
    email: string;
    password: string;
}

export interface RegisterRequest {
    fullName: string;
    email: string;
    password: string;
    phone?: string;
    role?: string;
    hubId?: string;
}

export interface AuthResponse {
    userId: string;
    fullName: string;
    role: string;
    accessToken: string;
    refreshToken: string;
}

export async function login(request: LoginRequest): Promise<AuthResponse> {
    const res = await fetch(`${API_BASE}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request)
    });
    
    if (!res.ok) {
        let errorMessage = `Đăng nhập thất bại (${res.status})`;
        try {
            const err = await res.json();
            if (err.message) errorMessage = err.message;
        } catch (e) {
            // ignore
        }
        throw new Error(errorMessage);
    }
    
    return await res.json();
}

export async function register(request: RegisterRequest): Promise<{ message: string }> {
    const res = await fetch(`${API_BASE}/api/auth/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request)
    });

    if (!res.ok) {
        let errorMessage = `Đăng ký thất bại (${res.status})`;
        try {
            const err = await res.json();
            if (err.message) errorMessage = err.message;
        } catch (e) {
            // ignore
        }
        throw new Error(errorMessage);
    }

    return await res.json();
}
