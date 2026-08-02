import { useState, useEffect } from 'react';

/* ─── Types ──────────────────────────────────────────────────────── */

type NavItem = {
    label: string;
    onClick: () => void;
    active?: boolean;
};

type AppHeaderProps = {
    onLogout?: () => void;
    /** Pages available for this role */
    pages?: NavItem[];
};

/* ─── Component ──────────────────────────────────────────────────── */

export default function AppHeader({ onLogout, pages = [] }: AppHeaderProps) {
    const [fullName, setFullName] = useState('Khách');

    useEffect(() => {
        // Prefer localStorage fullName (set by login flow)
        const stored = localStorage.getItem('fullName');
        if (stored) {
            setFullName(stored);
        }
    }, []);

    const initial = fullName.charAt(0).toUpperCase();

    return (
        <header className="bg-white shadow-sm border-b border-gray-100 px-6 xl:px-12 py-4 flex justify-between items-center z-50 sticky top-0">
            {/* Logo */}
            <div className="flex items-center gap-3">
                <div className="w-10 h-10 bg-primary rounded-lg flex items-center justify-center p-1 text-white shadow-sm">
                    <span className="material-symbols-outlined text-[20px]">local_shipping</span>
                </div>
                <span className="text-gray-800 font-bold text-lg hidden sm:block">Hệ thống ghép chuyến</span>
            </div>

            {/* Desktop Nav */}
            <nav className="hidden md:flex items-center gap-8 text-gray-500 font-medium text-sm">
                {pages.map((item) => (
                    <button
                        key={item.label}
                        onClick={item.onClick}
                        className={`transition-colors pb-1 ${
                            item.active
                                ? 'text-primary border-b-2 border-primary'
                                : 'hover:text-primary'
                        }`}
                    >
                        {item.label}
                    </button>
                ))}
            </nav>

            {/* Right side */}
            <div className="flex items-center gap-4">
                {/* Mobile nav: first item as quick action */}
                {pages.length > 1 && (
                    <button
                        onClick={pages[1].onClick}
                        className="md:hidden flex items-center gap-1.5 px-3 py-2 bg-primary/10 text-primary text-xs font-bold rounded-lg hover:bg-primary/20 transition-colors"
                    >
                        <span className="material-symbols-outlined text-[16px]">add</span>
                        {pages[1].label}
                    </button>
                )}

                <button className="w-10 h-10 rounded-full flex items-center justify-center hover:bg-gray-50 text-gray-600 transition-colors">
                    <span className="material-symbols-outlined text-[24px]">notifications</span>
                </button>

                {onLogout && (
                    <button
                        onClick={onLogout}
                        className="w-10 h-10 rounded-full flex items-center justify-center hover:bg-gray-50 text-red-600 hover:text-red-800 transition-colors"
                        title="Đăng xuất"
                    >
                        <span className="material-symbols-outlined text-[24px]">logout</span>
                    </button>
                )}

                <div className="flex items-center gap-2 bg-gray-50 pl-2 pr-4 py-1.5 rounded-full border border-gray-200 cursor-pointer">
                    <div className="w-8 h-8 bg-primary text-white rounded-full flex items-center justify-center font-bold text-sm">
                        {initial}
                    </div>
                    <span className="text-gray-700 font-medium text-sm hidden sm:block">{fullName}</span>
                </div>
            </div>
        </header>
    );
}
