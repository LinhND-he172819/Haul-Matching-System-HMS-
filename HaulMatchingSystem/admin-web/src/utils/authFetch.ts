/**
 * Shared auth-aware fetch utility.
 * - Attaches Bearer token from localStorage.
 * - On 401, attempts a single token-refresh via /api/auth/refresh-token.
 * - If refresh succeeds, retries the original request once.
 * - If refresh fails, clears localStorage and redirects to login.
 */

const API_BASE =
    import.meta.env.VITE_API_BASE_URL ??
    import.meta.env.VITE_API_URL ??
    'http://localhost:5104';

let refreshPromise: Promise<string> | null = null;

async function doRefreshToken(): Promise<string> {
    const accessToken = localStorage.getItem('accessToken');
    const refreshToken = localStorage.getItem('refreshToken');

    if (!accessToken || !refreshToken) {
        throw new Error('No tokens');
    }

    const res = await fetch(`${API_BASE}/api/auth/refresh-token`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ accessToken, refreshToken }),
    });

    if (!res.ok) throw new Error('Refresh failed');

    const data = await res.json();
    localStorage.setItem('accessToken', data.accessToken);
    localStorage.setItem('refreshToken', data.refreshToken);
    return data.accessToken;
}

/**
 * Attempt to refresh the token, deduplicating concurrent calls.
 */
function tryRefreshToken(): Promise<string> {
    if (!refreshPromise) {
        refreshPromise = doRefreshToken().finally(() => {
            refreshPromise = null;
        });
    }
    return refreshPromise;
}

function logout() {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('fullName');
    localStorage.removeItem('role');
    window.location.href = '/';
}

function buildHeaders(
    includeJson: boolean,
    token: string | null
): Record<string, string> {
    const h: Record<string, string> = {};
    if (token) h['Authorization'] = `Bearer ${token}`;
    if (includeJson) h['Content-Type'] = 'application/json';
    return h;
}

/**
 * authFetch – drop-in replacement for fetch() that:
 * 1. Adds Authorization header automatically
 * 2. On 401, refreshes token and retries once
 * 3. On refresh failure, logs user out
 */
export async function authFetch(
    input: RequestInfo | URL,
    init: RequestInit = {},
    options: { includeJson?: boolean } = {}
): Promise<Response> {
    const { includeJson = false } = options;

    const token = localStorage.getItem('accessToken');
    const mergedHeaders = {
        ...buildHeaders(includeJson, token),
        ...(init.headers as Record<string, string> | undefined),
    };

    let res = await fetch(input, { ...init, headers: mergedHeaders });

    // On 401, try refresh and retry once
    if (res.status === 401) {
        try {
            const newToken = await tryRefreshToken();
            const retryHeaders = {
                ...buildHeaders(includeJson, newToken),
                ...(init.headers as Record<string, string> | undefined),
            };
            res = await fetch(input, { ...init, headers: retryHeaders });
        } catch {
            // Refresh failed → force logout
            logout();
            throw new Error('Phiên đăng nhập đã hết hạn. Đang chuyển hướng đến trang đăng nhập...');
        }
    }

    return res;
}
