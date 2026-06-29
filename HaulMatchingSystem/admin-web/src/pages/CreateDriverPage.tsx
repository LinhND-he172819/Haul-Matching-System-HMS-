import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from 'react';
import {
    createUser,
    fetchHubs,
    fetchUsers,
    updateUser,
    type HubDto,
    type UserDto
} from '../api/identityApi';

interface CreateDriverPageProps {
    sidebar?: ReactNode;
}

interface DriverForm {
    fullName: string;
    phone: string;
    email: string;
    hubId: string;
    password: string;
    confirmPassword: string;
}

interface Toast {
    id: string;
    message: string;
    type: 'success' | 'error';
}

const emptyForm = (hubId = ''): DriverForm => ({
    fullName: '',
    phone: '',
    email: '',
    hubId,
    password: '',
    confirmPassword: ''
});

export default function CreateDriverPage({ sidebar }: CreateDriverPageProps) {
    const [drivers, setDrivers] = useState<UserDto[]>([]);
    const [hubs, setHubs] = useState<HubDto[]>([]);
    const [form, setForm] = useState<DriverForm>(emptyForm());
    const [editingId, setEditingId] = useState<string | null>(null);
    const [search, setSearch] = useState('');
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [showPassword, setShowPassword] = useState(false);
    const [toasts, setToasts] = useState<Toast[]>([]);

    const showToast = (message: string, type: Toast['type'] = 'success') => {
        const id = Math.random().toString(36).substring(2, 9);
        setToasts(previous => [...previous, { id, message, type }]);
        window.setTimeout(() => {
            setToasts(previous => previous.filter(toast => toast.id !== id));
        }, 3000);
    };

    const loadData = async () => {
        setLoading(true);
        try {
            const [hubData, userData] = await Promise.all([fetchHubs(), fetchUsers()]);
            setHubs(hubData);
            setDrivers(userData.filter(user => user.role === 'Driver'));
            setForm(current => ({ ...current, hubId: current.hubId || hubData[0]?.id || '' }));
        } catch (error) {
            showToast(error instanceof Error ? error.message : 'Không thể tải dữ liệu tài xế.', 'error');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        void loadData();
    }, []);

    const hubNames = useMemo(
        () => Object.fromEntries(hubs.map(hub => [hub.id, hub.name])),
        [hubs]
    );

    const filteredDrivers = useMemo(() => {
        const keyword = search.trim().toLowerCase();
        if (!keyword) return drivers;

        return drivers.filter(driver => {
            const hubName = driver.hubId ? hubNames[driver.hubId] || '' : '';
            return driver.fullName.toLowerCase().includes(keyword) ||
                (driver.phone || '').toLowerCase().includes(keyword) ||
                (driver.email || '').toLowerCase().includes(keyword) ||
                hubName.toLowerCase().includes(keyword);
        });
    }, [drivers, hubNames, search]);

    const passwordStrength = useMemo(() => {
        if (!form.password) return { label: '', width: 'w-0', color: 'bg-outline-variant' };
        const score = [
            form.password.length >= 8,
            /\d/.test(form.password),
            /[A-Z]/.test(form.password),
            /[^A-Za-z0-9]/.test(form.password)
        ].filter(Boolean).length;

        if (score >= 4) return { label: 'Mạnh', width: 'w-full', color: 'bg-secondary' };
        if (score >= 2) return { label: 'Trung bình', width: 'w-2/3', color: 'bg-tertiary' };
        return { label: 'Yếu', width: 'w-1/3', color: 'bg-error' };
    }, [form.password]);

    const updateForm = (field: keyof DriverForm, value: string) => {
        setForm(current => ({ ...current, [field]: value }));
    };

    const resetForm = () => {
        setEditingId(null);
        setForm(emptyForm(hubs[0]?.id || ''));
        setShowPassword(false);
    };

    const editDriver = (driver: UserDto) => {
        setEditingId(driver.id);
        setForm({
            fullName: driver.fullName,
            phone: driver.phone || '',
            email: driver.email || '',
            hubId: driver.hubId || hubs[0]?.id || '',
            password: '',
            confirmPassword: ''
        });
        setShowPassword(false);
        window.scrollTo({ top: 0, behavior: 'smooth' });
    };

    const generatePassword = () => {
        const characters = 'abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$%';
        let generated = '';
        for (let index = 0; index < 12; index += 1) {
            generated += characters.charAt(Math.floor(Math.random() * characters.length));
        }
        setForm(current => ({ ...current, password: generated, confirmPassword: generated }));
        setShowPassword(true);
    };

    const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();

        if (!form.fullName.trim() || !form.phone.trim() || !form.email.trim() || !form.hubId) {
            showToast('Vui lòng điền đầy đủ thông tin tài xế.', 'error');
            return;
        }
        if (!editingId && !form.password) {
            showToast('Mật khẩu là bắt buộc khi tạo tài xế.', 'error');
            return;
        }
        if (form.password && form.password.length < 6) {
            showToast('Mật khẩu phải có ít nhất 6 ký tự.', 'error');
            return;
        }
        if (form.password !== form.confirmPassword) {
            showToast('Mật khẩu nhập lại không khớp.', 'error');
            return;
        }

        try {
            setSaving(true);
            const payload = {
                fullName: form.fullName.trim(),
                phone: form.phone.trim(),
                email: form.email.trim(),
                hubId: form.hubId,
                role: 'Driver'
            };

            if (editingId) {
                await updateUser(editingId, {
                    ...payload,
                    password: form.password || undefined
                });
                showToast(`Đã cập nhật tài xế "${payload.fullName}".`);
            } else {
                await createUser({ ...payload, password: form.password });
                showToast(`Đã tạo tài xế "${payload.fullName}".`);
            }

            resetForm();
            await loadData();
        } catch (error) {
            showToast(error instanceof Error ? error.message : 'Không thể lưu tài xế.', 'error');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div className="min-h-screen bg-surface text-on-surface font-body-md flex overflow-x-hidden">
            {sidebar}
            <div className="flex min-w-0 flex-1 flex-col xl:ml-64">
                <header className="sticky top-0 z-20 flex min-h-16 items-center border-b border-outline-variant bg-surface-container-lowest px-5 md:px-8">
                    <div className="flex items-center gap-2 text-headline-md font-bold text-primary">
                        <span className="material-symbols-outlined">badge</span>
                        Quản lý Tài Xế
                    </div>
                </header>

                <main className="mx-auto grid w-full max-w-[1500px] grid-cols-1 gap-4 p-4 md:p-8 xl:grid-cols-[minmax(0,1fr)_430px]">
                    <section className="order-2 min-w-0 rounded-lg border border-outline-variant/30 bg-surface-container-lowest p-4 card-shadow xl:order-1 md:p-5">
                        <div className="mb-4 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                            <div>
                                <h1 className="text-xl font-bold text-on-surface">Danh sách tài xế</h1>
                                <p className="mt-1 text-sm text-on-surface-variant">{drivers.length} tài khoản tài xế</p>
                            </div>
                            <div className="flex w-full items-center gap-2 lg:w-auto">
                                <label className="flex h-10 flex-1 items-center rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 lg:w-80">
                                    <span className="material-symbols-outlined mr-2 text-[19px] text-on-surface-variant">search</span>
                                    <input className="w-full bg-transparent text-sm outline-none" onChange={event => setSearch(event.target.value)} placeholder="Tìm tên, SĐT, email hoặc Hub" type="search" value={search} />
                                </label>
                                <button aria-label="Tải lại danh sách" className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg border border-outline-variant/50 text-primary hover:bg-surface-container-low disabled:opacity-50" disabled={loading} onClick={() => void loadData()} title="Tải lại" type="button">
                                    <span className={`material-symbols-outlined text-[20px] ${loading ? 'animate-spin' : ''}`}>refresh</span>
                                </button>
                            </div>
                        </div>

                        <div className="overflow-x-auto rounded-lg border border-outline-variant/20">
                            <table className="w-full min-w-[760px] border-collapse text-left text-sm">
                                <thead className="bg-surface-container-low text-xs text-on-surface-variant">
                                    <tr>
                                        <th className="px-4 py-3 font-bold">Tài xế</th>
                                        <th className="px-4 py-3 font-bold">Liên hệ</th>
                                        <th className="px-4 py-3 font-bold">Hub trực thuộc</th>
                                        <th className="px-4 py-3 font-bold">Ngày tạo</th>
                                        <th className="px-4 py-3 text-center font-bold">Thao tác</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {loading ? (
                                        <tr><td className="px-4 py-12 text-center text-on-surface-variant" colSpan={5}>Đang tải danh sách tài xế...</td></tr>
                                    ) : filteredDrivers.length === 0 ? (
                                        <tr><td className="px-4 py-12 text-center text-on-surface-variant" colSpan={5}>Không tìm thấy tài xế phù hợp.</td></tr>
                                    ) : filteredDrivers.map(driver => (
                                        <tr className={`border-t border-outline-variant/15 hover:bg-surface-container-low/50 ${editingId === driver.id ? 'bg-primary/5' : ''}`} key={driver.id}>
                                            <td className="px-4 py-3">
                                                <div className="flex items-center gap-3">
                                                    <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary/10 font-bold text-primary">{driver.fullName.charAt(0).toUpperCase()}</div>
                                                    <div><p className="font-bold text-on-surface">{driver.fullName}</p><p className="text-xs text-on-surface-variant">{driver.id.slice(0, 8).toUpperCase()}</p></div>
                                                </div>
                                            </td>
                                            <td className="px-4 py-3"><p className="font-medium">{driver.phone || '--'}</p><p className="mt-0.5 text-xs text-on-surface-variant">{driver.email || '--'}</p></td>
                                            <td className="px-4 py-3 font-medium">{driver.hubId ? hubNames[driver.hubId] || 'Hub không xác định' : 'Chưa liên kết'}</td>
                                            <td className="px-4 py-3 text-on-surface-variant">{new Date(driver.createdAt).toLocaleDateString('vi-VN')}</td>
                                            <td className="px-4 py-3 text-center">
                                                <button aria-label={`Sửa tài xế ${driver.fullName}`} className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-primary hover:bg-primary/10" onClick={() => editDriver(driver)} title="Sửa tài xế" type="button">
                                                    <span className="material-symbols-outlined text-[20px]">edit</span>
                                                </button>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    </section>

                    <aside className="order-1 xl:order-2">
                        <form className="rounded-lg border border-outline-variant/30 bg-surface-container-lowest p-5 card-shadow xl:sticky xl:top-20" onSubmit={handleSubmit}>
                            <div className="mb-5 flex items-start justify-between gap-3 border-b border-outline-variant/20 pb-4">
                                <div><h2 className="text-xl font-bold">{editingId ? 'Cập nhật tài xế' : 'Tạo tài xế'}</h2><p className="mt-1 text-sm text-on-surface-variant">{editingId ? 'Để trống mật khẩu nếu muốn giữ nguyên.' : 'Tạo tài khoản Driver mới.'}</p></div>
                                {editingId && <button aria-label="Hủy chỉnh sửa" className="flex h-9 w-9 items-center justify-center rounded-lg border border-outline-variant/50 text-on-surface-variant hover:bg-surface-container-low" onClick={resetForm} title="Hủy chỉnh sửa" type="button"><span className="material-symbols-outlined text-[20px]">close</span></button>}
                            </div>

                            <div className="space-y-4">
                                <TextField autoComplete="name" icon="person" label="Họ và tên *" onChange={value => updateForm('fullName', value)} placeholder="Nguyễn Văn A" value={form.fullName} />
                                <TextField autoComplete="tel" icon="call" label="Số điện thoại *" onChange={value => updateForm('phone', value)} placeholder="0987654321" type="tel" value={form.phone} />
                                <TextField autoComplete="email" icon="mail" label="Email *" onChange={value => updateForm('email', value)} placeholder="driver@example.com" type="email" value={form.email} />

                                <label className="flex flex-col gap-2"><span className="text-sm font-bold text-on-surface-variant">Hub trực thuộc *</span><div className="flex h-11 items-center rounded-lg border border-outline-variant/50 bg-surface-container-low px-3"><span className="material-symbols-outlined mr-2 text-[20px] text-on-surface-variant">warehouse</span><select className="w-full bg-transparent text-sm outline-none" disabled={hubs.length === 0} onChange={event => updateForm('hubId', event.target.value)} required value={form.hubId}>{hubs.length === 0 ? <option value="">Chưa có Hub</option> : hubs.map(hub => <option key={hub.id} value={hub.id}>{hub.name}</option>)}</select></div></label>

                                <div className="flex flex-col gap-2"><div className="flex items-center justify-between"><span className="text-sm font-bold text-on-surface-variant">Mật khẩu {editingId ? 'mới' : '*'}</span><button className="text-xs font-bold text-primary hover:underline" onClick={generatePassword} type="button">Tạo ngẫu nhiên</button></div><PasswordField onChange={value => updateForm('password', value)} onToggleVisibility={() => setShowPassword(current => !current)} placeholder={editingId ? 'Để trống để giữ nguyên' : 'Tối thiểu 6 ký tự'} required={!editingId} showPassword={showPassword} value={form.password} /><div className="flex items-center gap-3 px-1"><div className="h-1.5 flex-1 overflow-hidden rounded-full bg-surface-container-high"><div className={`h-full transition-all ${passwordStrength.width} ${passwordStrength.color}`} /></div><span className="min-w-16 text-right text-xs font-semibold text-on-surface-variant">{passwordStrength.label}</span></div></div>
                                <div className="flex flex-col gap-2"><span className="text-sm font-bold text-on-surface-variant">Nhập lại mật khẩu {editingId ? 'mới' : '*'}</span><PasswordField onChange={value => updateForm('confirmPassword', value)} onToggleVisibility={() => setShowPassword(current => !current)} placeholder="Nhập lại mật khẩu" required={!editingId || Boolean(form.password)} showPassword={showPassword} value={form.confirmPassword} /></div>
                            </div>

                            <div className="mt-6 flex gap-3 border-t border-outline-variant/20 pt-5"><button className="flex-1 rounded-lg border border-outline-variant px-4 py-3 text-sm font-bold hover:bg-surface-container-low" onClick={resetForm} type="button">Làm mới</button><button className="flex flex-1 items-center justify-center gap-2 rounded-lg bg-primary px-4 py-3 text-sm font-bold text-on-primary hover:bg-primary/90 disabled:opacity-50" disabled={saving || hubs.length === 0} type="submit"><span className={`material-symbols-outlined text-[20px] ${saving ? 'animate-spin' : ''}`}>{saving ? 'progress_activity' : 'save'}</span>{saving ? 'Đang lưu...' : editingId ? 'Cập nhật' : 'Tạo tài xế'}</button></div>
                        </form>
                    </aside>
                </main>
            </div>

            <div className="fixed bottom-6 right-6 z-50 flex flex-col gap-3">{toasts.map(toast => <div className={`flex min-w-[280px] items-center gap-2 rounded-lg border bg-surface p-4 shadow-lg ${toast.type === 'success' ? 'border-secondary' : 'border-error'}`} key={toast.id}><span className={`material-symbols-outlined ${toast.type === 'success' ? 'text-secondary' : 'text-error'}`}>{toast.type === 'success' ? 'check_circle' : 'error'}</span><span className="text-xs font-bold">{toast.message}</span></div>)}</div>
        </div>
    );
}

function TextField({ autoComplete, icon, label, onChange, placeholder, type = 'text', value }: { autoComplete: string; icon: string; label: string; onChange: (value: string) => void; placeholder: string; type?: string; value: string }) {
    return <label className="flex flex-col gap-2"><span className="text-sm font-bold text-on-surface-variant">{label}</span><div className="flex h-11 items-center rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/20"><span className="material-symbols-outlined mr-2 text-[20px] text-on-surface-variant">{icon}</span><input autoComplete={autoComplete} className="w-full bg-transparent text-sm outline-none" onChange={event => onChange(event.target.value)} placeholder={placeholder} required type={type} value={value} /></div></label>;
}

function PasswordField({ onChange, onToggleVisibility, placeholder, required, showPassword, value }: { onChange: (value: string) => void; onToggleVisibility: () => void; placeholder: string; required: boolean; showPassword: boolean; value: string }) {
    return <div className="flex h-11 items-center rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/20"><span className="material-symbols-outlined mr-2 text-[20px] text-on-surface-variant">lock</span><input autoComplete="new-password" className="w-full bg-transparent text-sm outline-none" minLength={value ? 6 : undefined} onChange={event => onChange(event.target.value)} placeholder={placeholder} required={required} type={showPassword ? 'text' : 'password'} value={value} /><button aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} className="ml-2 flex h-8 w-8 items-center justify-center text-on-surface-variant hover:text-primary" onClick={onToggleVisibility} title={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} type="button"><span className="material-symbols-outlined text-[19px]">{showPassword ? 'visibility_off' : 'visibility'}</span></button></div>;
}
