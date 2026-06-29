import React, { useEffect, useState } from 'react';
import { deleteUser, fetchHubs, fetchUsers } from '../api/identityApi';

interface Customer {
    id: string;
    fullName: string;
    phone: string;
    email: string;
    hubName: string;
    createdAt: string;
}

interface CreateCustomerPageProps {
    sidebar?: React.ReactNode;
}

interface Toast {
    id: string;
    message: string;
    type: 'success' | 'error';
}

export default function CreateCustomerPage({ sidebar }: CreateCustomerPageProps) {
    const [searchTerm, setSearchTerm] = useState('');
    const [customers, setCustomers] = useState<Customer[]>([]);
    const [loading, setLoading] = useState(true);
    const [toasts, setToasts] = useState<Toast[]>([]);

    const showToast = (message: string, type: Toast['type'] = 'success') => {
        const id = Math.random().toString(36).substring(2, 9);
        setToasts(previous => [...previous, { id, message, type }]);
        window.setTimeout(() => {
            setToasts(previous => previous.filter(toast => toast.id !== id));
        }, 3000);
    };

    const refreshData = async () => {
        setLoading(true);

        try {
            const [hubs, users] = await Promise.all([fetchHubs(), fetchUsers()]);
            const customerUsers = users
                .filter(user => user.role === 'Customer')
                .map(user => ({
                    id: user.id,
                    fullName: user.fullName,
                    phone: user.phone || '--',
                    email: user.email || '--',
                    hubName: hubs.find(hub => hub.id === user.hubId)?.name || 'Không liên kết',
                    createdAt: new Date(user.createdAt).toLocaleDateString('vi-VN')
                }));

            setCustomers(customerUsers);
        } catch (error) {
            console.error('Không thể tải danh sách khách hàng:', error);
            showToast(error instanceof Error ? error.message : 'Không thể kết nối máy chủ API', 'error');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        void refreshData();
    }, []);

    const handleDelete = async (id: string, name: string) => {
        if (!window.confirm(`Bạn có chắc chắn muốn xóa tài khoản "${name}" không?`)) {
            return;
        }

        try {
            await deleteUser(id);
            showToast(`Đã xóa tài khoản "${name}".`);
            await refreshData();
        } catch (error) {
            showToast(error instanceof Error ? error.message : 'Xóa tài khoản thất bại.', 'error');
        }
    };

    const normalizedSearch = searchTerm.trim().toLowerCase();
    const filteredCustomers = customers.filter(customer =>
        customer.fullName.toLowerCase().includes(normalizedSearch) ||
        customer.phone.toLowerCase().includes(normalizedSearch) ||
        customer.email.toLowerCase().includes(normalizedSearch) ||
        customer.hubName.toLowerCase().includes(normalizedSearch)
    );

    return (
        <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
            {sidebar}

            <div className="flex-1 flex flex-col xl:ml-64 w-full min-w-0">
                <header className="bg-surface-container-lowest border-b border-outline-variant min-h-16 w-full flex items-center px-5 md:px-8 sticky top-0 z-10">
                    <div className="text-headline-md font-bold text-primary flex items-center gap-2">
                        <span className="material-symbols-outlined">group</span>
                        Quản lý khách hàng
                    </div>
                </header>

                <main className="flex-1 p-4 md:p-container-margin overflow-y-auto">
                    <section className="bg-surface-container-lowest rounded-lg p-4 md:p-card-padding card-shadow border border-outline-variant/30 space-y-4">
                        <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
                            <div>
                                <h1 className="text-headline-md font-bold text-on-surface flex items-center gap-2">
                                    <span className="material-symbols-outlined text-primary">table_rows</span>
                                    Danh sách khách hàng
                                </h1>
                                <p className="text-sm text-on-surface-variant mt-1">
                                    {customers.length} tài khoản khách hàng trong hệ thống
                                </p>
                            </div>

                            <div className="flex items-center gap-2 w-full lg:w-auto">
                                <div className="flex items-center bg-surface-container-low rounded-lg px-3 h-10 border border-outline-variant/50 flex-1 lg:w-80">
                                    <span className="material-symbols-outlined text-on-surface-variant mr-2 text-[18px]">search</span>
                                    <input
                                        type="search"
                                        placeholder="Tìm theo tên, SĐT, email hoặc Hub"
                                        className="bg-transparent border-none outline-none text-sm w-full focus:ring-0 p-0 text-on-surface"
                                        value={searchTerm}
                                        onChange={event => setSearchTerm(event.target.value)}
                                    />
                                </div>
                                <button
                                    type="button"
                                    onClick={() => void refreshData()}
                                    disabled={loading}
                                    className="h-10 w-10 shrink-0 inline-flex items-center justify-center rounded-lg border border-outline-variant text-primary hover:bg-surface-container-low disabled:opacity-50 transition-colors"
                                    title="Tải lại danh sách"
                                    aria-label="Tải lại danh sách khách hàng"
                                >
                                    <span className={`material-symbols-outlined text-[20px] ${loading ? 'animate-spin' : ''}`}>refresh</span>
                                </button>
                            </div>
                        </div>

                        <div className="overflow-x-auto border border-outline-variant/20 rounded-lg">
                            <table className="w-full min-w-[900px] text-left text-xs border-collapse">
                                <thead className="bg-surface-container-low">
                                    <tr className="border-b border-outline-variant/30 text-on-surface-variant">
                                        <th className="py-3 px-4 font-bold">Mã khách hàng</th>
                                        <th className="py-3 px-4 font-bold">Tên khách hàng</th>
                                        <th className="py-3 px-4 font-bold">Số điện thoại</th>
                                        <th className="py-3 px-4 font-bold">Email</th>
                                        <th className="py-3 px-4 font-bold">Hub liên kết</th>
                                        <th className="py-3 px-4 font-bold">Ngày tạo</th>
                                        <th className="py-3 px-4 font-bold text-center">Thao tác</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {loading ? (
                                        <tr>
                                            <td colSpan={7} className="py-12 text-center text-on-surface-variant">
                                                <span className="material-symbols-outlined animate-spin align-middle mr-2">progress_activity</span>
                                                Đang tải danh sách khách hàng...
                                            </td>
                                        </tr>
                                    ) : filteredCustomers.length === 0 ? (
                                        <tr>
                                            <td colSpan={7} className="py-12 text-center text-on-surface-variant">
                                                Không tìm thấy khách hàng phù hợp.
                                            </td>
                                        </tr>
                                    ) : (
                                        filteredCustomers.map(customer => (
                                            <tr key={customer.id} className="border-b border-outline-variant/10 last:border-b-0 hover:bg-surface-container-low/40 transition-colors">
                                                <td className="py-3.5 px-4 font-bold text-primary">{customer.id.substring(0, 8).toUpperCase()}</td>
                                                <td className="py-3.5 px-4 font-bold">
                                                    <div className="flex items-center gap-2">
                                                        <div className="w-7 h-7 rounded-full bg-primary/10 text-primary flex items-center justify-center font-bold">
                                                            {customer.fullName.charAt(0).toUpperCase()}
                                                        </div>
                                                        {customer.fullName}
                                                    </div>
                                                </td>
                                                <td className="py-3.5 px-4 font-semibold text-on-surface-variant">{customer.phone}</td>
                                                <td className="py-3.5 px-4 text-on-surface-variant">{customer.email}</td>
                                                <td className="py-3.5 px-4 font-medium">{customer.hubName}</td>
                                                <td className="py-3.5 px-4 text-on-surface-variant">{customer.createdAt}</td>
                                                <td className="py-3.5 px-4 text-center">
                                                    <button
                                                        type="button"
                                                        onClick={() => void handleDelete(customer.id, customer.fullName)}
                                                        className="h-8 w-8 inline-flex items-center justify-center text-error hover:bg-error/10 rounded-lg transition-colors"
                                                        title="Xóa khách hàng"
                                                        aria-label={`Xóa khách hàng ${customer.fullName}`}
                                                    >
                                                        <span className="material-symbols-outlined text-[18px]">delete</span>
                                                    </button>
                                                </td>
                                            </tr>
                                        ))
                                    )}
                                </tbody>
                            </table>
                        </div>
                    </section>
                </main>
            </div>

            <div className="fixed right-6 bottom-6 z-50 flex flex-col gap-3">
                {toasts.map(toast => (
                    <div
                        key={toast.id}
                        className={`p-4 rounded-lg border shadow-lg flex gap-2 items-center min-w-[280px] bg-surface ${
                            toast.type === 'success' ? 'border-secondary text-secondary' : 'border-error text-error'
                        }`}
                    >
                        <span className="material-symbols-outlined">
                            {toast.type === 'success' ? 'check_circle' : 'error'}
                        </span>
                        <span className="text-xs font-bold text-on-surface">{toast.message}</span>
                    </div>
                ))}
            </div>
        </div>
    );
}
