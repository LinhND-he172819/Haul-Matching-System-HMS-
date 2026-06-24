const API_BASE =
    import.meta.env.VITE_API_BASE_URL ??
    import.meta.env.VITE_API_URL ??
    'http://localhost:5104';

export interface LoginRequest {
    email: string;
    password: string;
    role?: string;
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

export interface RequestOtpResponse {
    message: string;
}

export async function requestLoginOtp(phone: string, role?: string): Promise<RequestOtpResponse> {
    const res = await fetch(`${API_BASE}/api/auth/login-otp/request`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ phone, role })
    });
    if (!res.ok) {
        let errorMessage = `Yêu cầu OTP thất bại (${res.status})`;
        try { const err = await res.json(); if (err.message) errorMessage = err.message; } catch (e) {}
        throw new Error(errorMessage);
    }
    return await res.json();
}

export async function verifyLoginOtp(phone: string, otp: string, role?: string): Promise<AuthResponse> {
    const res = await fetch(`${API_BASE}/api/auth/login-otp/verify`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ phone, otp, role })
    });
    if (!res.ok) {
        let errorMessage = `Xác thực OTP thất bại (${res.status})`;
        try { const err = await res.json(); if (err.message) errorMessage = err.message; } catch (e) {}
        throw new Error(errorMessage);
    }
    return await res.json();
}

export async function requestRegisterOtp(phone: string): Promise<RequestOtpResponse> {
    const res = await fetch(`${API_BASE}/api/auth/register-otp/request`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ phone })
    });
    if (!res.ok) {
        let errorMessage = `Yêu cầu OTP thất bại (${res.status})`;
        try { const err = await res.json(); if (err.message) errorMessage = err.message; } catch (e) {}
        throw new Error(errorMessage);
    }
    return await res.json();
}

export async function verifyRegisterOtp(phone: string, fullName: string, otp: string, role: string): Promise<AuthResponse> {
    const res = await fetch(`${API_BASE}/api/auth/register-otp/verify`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ phone, fullName, otp, role })
    });
    if (!res.ok) {
        let errorMessage = `Xác thực OTP thất bại (${res.status})`;
        try { const err = await res.json(); if (err.message) errorMessage = err.message; } catch (e) {}
        throw new Error(errorMessage);
    }
    return await res.json();
}
