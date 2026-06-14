import { useState } from 'react';
import { login } from '../api/authApi';
import type { LoginRequest } from '../api/authApi';

interface LoginPageProps {
    onNavigate: (page: 'login' | 'register' | 'home') => void;
}

export default function LoginPage({ onNavigate }: LoginPageProps) {
    const [formData, setFormData] = useState<LoginRequest>({ email: '', password: '' });
    const [error, setError] = useState('');
    const [isLoading, setIsLoading] = useState(false);

    const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setFormData({ ...formData, [e.target.name]: e.target.value });
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError('');
        setIsLoading(true);
        try {
            const res = await login(formData);
            localStorage.setItem('accessToken', res.accessToken);
            localStorage.setItem('refreshToken', res.refreshToken);
            localStorage.setItem('fullName', res.fullName);
            localStorage.setItem('role', res.role);
            onNavigate('home');
        } catch (err) {
            if (err instanceof Error) {
                setError(err.message);
            } else {
                setError('Đã xảy ra lỗi khi đăng nhập.');
            }
        } finally {
            setIsLoading(false);
        }
    };

    return (
        <div className="bg-surface text-on-surface font-body-md min-h-screen flex items-center justify-center p-4">
            <div className="bg-surface-container-lowest border border-outline-variant rounded-xl p-8 card-shadow w-full max-w-md">
                <div className="flex flex-col items-center mb-8">
                    <div className="w-12 h-12 rounded-lg bg-primary flex items-center justify-center text-on-primary mb-4">
                        <span className="material-symbols-outlined text-[24px]">local_shipping</span>
                    </div>
                    <h1 className="text-headline-md font-headline-md text-on-surface">Đăng nhập</h1>
                    <p className="text-body-md text-on-surface-variant mt-2">Hệ thống quản lý ghép chuyến</p>
                </div>

                {error && (
                    <div className="bg-error-container/20 border border-error/30 text-error p-3 rounded-lg mb-6 text-body-sm flex items-center gap-2">
                        <span className="material-symbols-outlined text-[20px]">error</span>
                        {error}
                    </div>
                )}

                <form onSubmit={handleSubmit} className="space-y-4">
                    <div>
                        <label className="block text-label-md font-label-md text-on-surface-variant mb-1">Email</label>
                        <div className="flex items-center bg-surface-container-low rounded-lg px-3 py-2 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                            <span className="material-symbols-outlined text-on-surface-variant text-[20px] mr-2">mail</span>
                            <input
                                type="email"
                                name="email"
                                value={formData.email}
                                onChange={handleChange}
                                required
                                className="bg-transparent border-none outline-none text-body-md w-full placeholder-on-surface-variant/70 focus:ring-0 p-0 text-on-surface"
                                placeholder="Nhập email của bạn"
                            />
                        </div>
                    </div>

                    <div>
                        <label className="block text-label-md font-label-md text-on-surface-variant mb-1">Mật khẩu</label>
                        <div className="flex items-center bg-surface-container-low rounded-lg px-3 py-2 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                            <span className="material-symbols-outlined text-on-surface-variant text-[20px] mr-2">lock</span>
                            <input
                                type="password"
                                name="password"
                                value={formData.password}
                                onChange={handleChange}
                                required
                                className="bg-transparent border-none outline-none text-body-md w-full placeholder-on-surface-variant/70 focus:ring-0 p-0 text-on-surface"
                                placeholder="Nhập mật khẩu"
                            />
                        </div>
                    </div>

                    <button
                        type="submit"
                        disabled={isLoading}
                        className="w-full bg-primary hover:bg-primary-container text-on-primary text-label-lg font-label-lg py-3 rounded-lg mt-6 transition-colors flex items-center justify-center gap-2 disabled:opacity-70"
                    >
                        {isLoading ? (
                            <span className="material-symbols-outlined animate-spin">refresh</span>
                        ) : (
                            <span className="material-symbols-outlined">login</span>
                        )}
                        {isLoading ? 'Đang xử lý...' : 'Đăng nhập'}
                    </button>
                </form>

                <div className="mt-6 text-center text-body-sm text-on-surface-variant">
                    Chưa có tài khoản?{' '}
                    <button
                        onClick={() => onNavigate('register')}
                        className="text-primary hover:underline font-bold"
                    >
                        Đăng ký ngay
                    </button>
                </div>
            </div>
        </div>
    );
}
