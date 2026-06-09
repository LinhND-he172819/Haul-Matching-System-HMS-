import { useState } from 'react';
import DashboardPage from './pages/DashboardPage';
import MatchingSuggestionPage from './pages/MatchingSuggestionPage';
import CreateCustomerPage from './pages/CreateCustomerPage';
import CreateDriverPage from './pages/CreateDriverPage';

type Tab = 'dashboard' | 'create-customer' | 'create-driver' | 'driver-portal';

function App() {
  const [currentTab, setCurrentTab] = useState<Tab>('dashboard');

  const renderSidebar = () => (
    <nav className="bg-surface-container-lowest border-r border-outline-variant fixed left-0 h-full w-64 flex flex-col py-6 px-4 z-20 hidden xl:flex">
      {/* Brand Logo */}
      <div className="mb-8 flex items-center gap-3 px-2">
        <div className="w-8 h-8 rounded bg-primary flex items-center justify-center text-on-primary shadow-sm">
          <span className="material-symbols-outlined text-[20px]">local_shipping</span>
        </div>
        <div>
          <h1 className="text-headline-lg font-headline-lg text-primary text-[20px] leading-tight">Ghép Chuyến</h1>
          <p className="text-label-md font-label-md text-on-surface-variant text-[12px]">Logistics Console</p>
        </div>
      </div>

      {/* Nav List */}
      <div className="flex-1 space-y-2">
        <button 
          onClick={() => setCurrentTab('dashboard')} 
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            currentTab === 'dashboard' 
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low' 
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">dashboard</span>
          <span className="text-label-lg font-bold">Tổng Quan</span>
        </button>

        <button 
          onClick={() => setCurrentTab('create-customer')} 
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            currentTab === 'create-customer' 
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low' 
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">person_add</span>
          <span className="text-label-lg font-bold">Tạo Khách Hàng</span>
        </button>

        <button 
          onClick={() => setCurrentTab('create-driver')} 
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            currentTab === 'create-driver' 
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low' 
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">local_shipping</span>
          <span className="text-label-lg font-bold">Tạo Tài Xế & Xe</span>
        </button>

        <div className="pt-4 border-t border-outline-variant/30 mt-4">
          <p className="text-[11px] font-bold text-on-surface-variant/50 px-4 uppercase tracking-wider mb-2">Demo Roles</p>
          <button 
            onClick={() => setCurrentTab('driver-portal')} 
            className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
              currentTab === 'driver-portal' 
              ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low' 
              : 'text-on-surface-variant hover:bg-surface-container-low/60'
            }`}
          >
            <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">account_circle</span>
            <span className="text-label-lg font-bold">Driver Portal</span>
          </button>
        </div>
      </div>

      {/* Footer Info */}
      <div className="mt-auto px-4 py-2 border-t border-outline-variant/20 pt-4 text-center">
        <span className="text-xs text-on-surface-variant/60 font-semibold block">HMS Admin Panel v1.2</span>
      </div>
    </nav>
  );

  switch (currentTab) {
    case 'dashboard':
      return <DashboardPage sidebar={renderSidebar()} />;
    case 'create-customer':
      return <CreateCustomerPage sidebar={renderSidebar()} />;
    case 'create-driver':
      return <CreateDriverPage sidebar={renderSidebar()} />;
    case 'driver-portal':
      return <MatchingSuggestionPage onBackToAdmin={() => setCurrentTab('dashboard')} />;
    default:
      return <DashboardPage sidebar={renderSidebar()} />;
  }
}

export default App;