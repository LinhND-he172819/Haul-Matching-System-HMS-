import { useState } from 'react';
import { register, type RegisterRequest } from '../api/authApi';

interface RegisterPageProps {
    onNavigate: (page: 'login' | 'register' | 'home') => void;
}

export default function RegisterPage({ onNavigate }: RegisterPageProps) {
    const [formData, setFormData] = useState<RegisterRequest>({ 
        fullName: '', 
        email: '', 
        password: '', 
        phone: '', 
        role: 'User' 
    });
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [isLoading, setIsLoading] = useState(false);

    const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
        setFormData({ ...formData, [e.target.name]: e.target.value });
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError('');
        setSuccess('');
        setIsLoading(true);
        try {
            const res = await register(formData);
            setSuccess(res.message || 'Đăng ký thành công!');
            setTimeout(() => {
                onNavigate('login');
            }, 2000);
        } catch (err: unknown) {
            const message = err instanceof Error ? err.message : 'Đã xảy ra lỗi. Vui lòng thử lại.';
            setError(message);
        } finally {
            setIsLoading(false);
        }
    };

    return (
        <div className="bg-surface text-on-surface font-body-md min-h-screen flex items-center justify-center p-4">
            <div className="bg-surface-container-lowest border border-outline-variant rounded-xl p-8 card-shadow w-full max-w-md">
                <div className="flex flex-col items-center mb-8">
                    <div className="w-12 h-12 rounded-lg bg-primary flex items-center justify-center text-on-primary mb-4">
                        <span className="material-symbols-outlined text-[24px]">person_add</span>
                    </div>
                    <h1 className="text-headline-md font-headline-md text-on-surface">Đăng ký tài khoản</h1>
                </div>

                {error && (
                    <div className="bg-error-container/20 border border-error/30 text-error p-3 rounded-lg mb-6 text-body-sm flex items-center gap-2">
                        <span className="material-symbols-outlined text-[20px]">error</span>
                        {error}
                    </div>
                )}

                {success && (
                    <div className="bg-secondary/20 border border-secondary/50 text-secondary p-3 rounded-lg mb-6 text-body-sm flex items-center gap-2">
                        <span className="material-symbols-outlined text-[20px]">check_circle</span>
                        {success}
                    </div>
                )}

                <form onSubmit={handleSubmit} className="space-y-4">
                    <div>
                        <label className="block text-label-md font-label-md text-on-surface-variant mb-1">Họ và tên</label>
                        <div className="flex items-center bg-surface-container-low rounded-lg px-3 py-2 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary transition-all">
                            <span className="material-symbols-outlined text-on-surface-variant text-[20px] mr-2">badge</span>
                            <input
                                type="text"
                                name="fullName"
                                value={formData.fullName}
                                onChange={handleChange}
                                required
                                className="bg-transparent border-none outline-none w-full text-on-surface"
                                placeholder="VD: Nguyễn Văn A"
                            />
                        </div>
                    </div>

                    <div>
                        <label className="block text-label-md font-label-md text-on-surface-variant mb-1">Email</label>
                        <div className="flex items-center bg-surface-container-low rounded-lg px-3 py-2 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary transition-all">
                            <span className="material-symbols-outlined text-on-surface-variant text-[20px] mr-2">mail</span>
                            <input
                                type="email"
                                name="email"
                                value={formData.email}
                                onChange={handleChange}
                                required
                                className="bg-transparent border-none outline-none w-full text-on-surface"
                                placeholder="Nhập email"
                            />
                        </div>
                    </div>

                    <div>
                        <label className="block text-label-md font-label-md text-on-surface-variant mb-1">Số điện thoại</label>
                        <div className="flex items-center bg-surface-container-low rounded-lg px-3 py-2 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary transition-all">
                            <span className="material-symbols-outlined text-on-surface-variant text-[20px] mr-2">call</span>
                            <input
                                type="text"
                                name="phone"
                                value={formData.phone}
                                onChange={handleChange}
                                className="bg-transparent border-none outline-none w-full text-on-surface"
                                placeholder="Nhập số điện thoại (tùy chọn)"
                            />
                        </div>
                    </div>

                    <div>
                        <label className="block text-label-md font-label-md text-on-surface-variant mb-1">Vai trò</label>
                        <div className="flex items-center bg-surface-container-low rounded-lg px-3 py-2 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary transition-all">
                            <span className="material-symbols-outlined text-on-surface-variant text-[20px] mr-2">group</span>
                            <select
                                name="role"
                                value={formData.role}
                                onChange={handleChange}
                                className="bg-transparent border-none outline-none w-full text-on-surface cursor-pointer"
                            >
                                <option value="User">User</option>
                                <option value="Driver">Driver</option>
                                <option value="Admin">Admin</option>
                            </select>
                        </div>
                    </div>

                    <div>
                        <label className="block text-label-md font-label-md text-on-surface-variant mb-1">Mật khẩu</label>
                        <div className="flex items-center bg-surface-container-low rounded-lg px-3 py-2 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary transition-all">
                            <span className="material-symbols-outlined text-on-surface-variant text-[20px] mr-2">lock</span>
                            <input
                                type="password"
                                name="password"
                                value={formData.password}
                                onChange={handleChange}
                                required
                                minLength={6}
                                className="bg-transparent border-none outline-none w-full text-on-surface"
                                placeholder="Tối thiểu 6 ký tự"
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
                            <span className="material-symbols-outlined">person_add</span>
                        )}
                        {isLoading ? 'Đang đăng ký...' : 'Đăng ký'}
                    </button>
                </form>

                <div className="mt-6 text-center text-body-sm text-on-surface-variant">
                    Đã có tài khoản?{' '}
                    <button
                        onClick={() => onNavigate('login')}
                        className="text-primary hover:underline font-bold"
                    >
                        Đăng nhập
                    </button>
                </div>
            </div>
        </div>
    );
}
