import { useState } from 'react';
import { requestRegisterOtp, verifyRegisterOtp } from '../api/authApi';


interface RegisterPageProps {
    onNavigate: (page: 'login' | 'register' | 'home') => void;
}

export default function RegisterPage({ onNavigate }: RegisterPageProps) {
    const role = 'Customer';

    // OTP states
    const [phone, setPhone] = useState('');
    const [fullName, setFullName] = useState('');
    const [otp, setOtp] = useState('');
    const [otpSent, setOtpSent] = useState(false);

    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [isLoading, setIsLoading] = useState(false);


    const handleRequestOtp = async () => {
        if (!phone) {
            setError('Vui lòng nhập số điện thoại');
            return;
        }
        if (!fullName) {
            setError('Vui lòng nhập họ và tên');
            return;
        }
        setError('');
        setSuccess('');
        setIsLoading(true);
        try {
            const res = await requestRegisterOtp(phone);
            setSuccess(res.message);
            setOtpSent(true);
        } catch (err: unknown) {
            const message = err instanceof Error ? err.message : 'Đã xảy ra lỗi khi yêu cầu OTP.';
            setError(message);
        } finally {
            setIsLoading(false);
        }
    };

    const handleVerifyOtp = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!otp) {
            setError('Vui lòng nhập mã OTP');
            return;
        }
        setError('');
        setSuccess('');
        setIsLoading(true);
        try {
            const res = await verifyRegisterOtp(phone, fullName, otp, role);

            // Login after register
            localStorage.setItem('accessToken', res.accessToken);
            localStorage.setItem('refreshToken', res.refreshToken);
            localStorage.setItem('fullName', res.fullName);
            localStorage.setItem('role', res.role);

            setSuccess('Đăng ký thành công!');
            setTimeout(() => {
                onNavigate('home');
            }, 1000);
        } catch (err: unknown) {
            const message = err instanceof Error ? err.message : 'Đã xảy ra lỗi khi xác thực OTP.';
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


                <form onSubmit={handleVerifyOtp} className="space-y-4">
                    <div>
                        <label className="block text-label-md font-label-md text-on-surface-variant mb-1">Số điện thoại</label>
                        <div className="flex items-center bg-surface-container-low rounded-lg px-3 py-2 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary transition-all">
                            <span className="material-symbols-outlined text-on-surface-variant text-[20px] mr-2">call</span>
                            <input
                                type="text"
                                value={phone}
                                onChange={(e) => setPhone(e.target.value)}
                                required
                                disabled={otpSent}
                                className="bg-transparent border-none outline-none w-full text-on-surface disabled:opacity-50"
                                placeholder="Nhập số điện thoại"
                            />
                        </div>
                    </div>

                    <div>
                        <label className="block text-label-md font-label-md text-on-surface-variant mb-1">Họ và tên</label>
                        <div className="flex items-center bg-surface-container-low rounded-lg px-3 py-2 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary transition-all">
                            <span className="material-symbols-outlined text-on-surface-variant text-[20px] mr-2">badge</span>
                            <input
                                type="text"
                                value={fullName}
                                onChange={(e) => setFullName(e.target.value)}
                                required
                                disabled={otpSent}
                                className="bg-transparent border-none outline-none w-full text-on-surface disabled:opacity-50"
                                placeholder="VD: Nguyễn Văn A"
                            />
                        </div>
                    </div>

                    {otpSent && (
                        <div>
                            <label className="block text-label-md font-label-md text-on-surface-variant mb-1">Mã OTP</label>
                            <div className="flex items-center bg-surface-container-low rounded-lg px-3 py-2 border border-outline-variant/50 focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50 transition-all">
                                <span className="material-symbols-outlined text-on-surface-variant text-[20px] mr-2">pin</span>
                                <input
                                    type="text"
                                    value={otp}
                                    onChange={(e) => setOtp(e.target.value)}
                                    required
                                    className="bg-transparent border-none outline-none text-body-md w-full placeholder-on-surface-variant/70 focus:ring-0 p-0 text-on-surface"
                                    placeholder="Nhập mã 6 số"
                                />
                            </div>
                        </div>
                    )}

                    {!otpSent ? (
                        <button
                            type="button"
                            onClick={handleRequestOtp}
                            disabled={isLoading}
                            className="w-full bg-secondary hover:bg-secondary-container text-on-secondary text-label-lg font-label-lg py-3 rounded-lg mt-6 transition-colors flex items-center justify-center gap-2 disabled:opacity-70"
                        >
                            {isLoading ? (
                                <span className="material-symbols-outlined animate-spin">refresh</span>
                            ) : (
                                <span className="material-symbols-outlined">send</span>
                            )}
                            Nhận OTP
                        </button>
                    ) : (
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
                            Đăng ký
                        </button>
                    )}
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
