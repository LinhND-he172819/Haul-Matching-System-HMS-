import { useState, useEffect } from 'react';
import DashboardPage from './pages/DashboardPage';
import MatchingSuggestionPage from './pages/MatchingSuggestionPage';
import CreateCustomerPage from './pages/CreateCustomerPage';
import CreateDriverPage from './pages/CreateDriverPage';
import DriverTripsPage from './pages/DriverTripsPage';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import HomePage from './pages/HomePage';

type Page = 'login' | 'register' | 'home' | 'admin' | 'driver-portal' | 'driver-trips';
type AdminTab = 'dashboard' | 'create-customer' | 'create-driver' | 'driver-portal' | 'driver-trips';

function App() {
  const [currentPage, setCurrentPage] = useState<Page>(() => {
    const token = localStorage.getItem('accessToken');
    if (!token) return 'login';
    const role = localStorage.getItem('role');
    if (role === 'Admin') return 'admin';
    if (role === 'Driver') return 'driver-portal';
    return 'home';
  });

  const [adminTab, setAdminTab] = useState<AdminTab>('dashboard');

  // Sync state if user changes localStorage directly or on mount
  useEffect(() => {
    const token = localStorage.getItem('accessToken');
    const role = localStorage.getItem('role');
    
    if (!token) {
      if (currentPage !== 'register' && currentPage !== 'login') {
        setCurrentPage('login');
      }
    } else {
      // Role checking and redirection
      if (currentPage === 'login' || currentPage === 'register') {
        if (role === 'Admin') {
          setCurrentPage('admin');
        } else if (role === 'Driver') {
          setCurrentPage('driver-portal');
        } else {
          setCurrentPage('home');
        }
      }
    }
  }, [currentPage]);

  const handleNavigate = (targetPage: 'login' | 'register' | 'home') => {
    if (targetPage === 'login') {
      setCurrentPage('login');
    } else if (targetPage === 'register') {
      setCurrentPage('register');
    } else if (targetPage === 'home') {
      const token = localStorage.getItem('accessToken');
      const role = localStorage.getItem('role');
      if (!token) {
        setCurrentPage('login');
      } else if (role === 'Admin') {
        setCurrentPage('admin');
        setAdminTab('dashboard');
      } else if (role === 'Driver') {
        setCurrentPage('driver-portal');
      } else {
        setCurrentPage('home');
      }
    }
  };

  const handleLogout = () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('fullName');
    localStorage.removeItem('role');
    setCurrentPage('login');
  };

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
          onClick={() => setAdminTab('dashboard')} 
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            adminTab === 'dashboard' 
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low' 
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">dashboard</span>
          <span className="text-label-lg font-bold">Tổng Quan</span>
        </button>

        <button 
          onClick={() => setAdminTab('create-customer')} 
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            adminTab === 'create-customer' 
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low' 
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">person_add</span>
          <span className="text-label-lg font-bold">Tạo Khách Hàng</span>
        </button>

        <button 
          onClick={() => setAdminTab('create-driver')} 
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            adminTab === 'create-driver' 
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low' 
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">local_shipping</span>
          <span className="text-label-lg font-bold">Tạo Tài Xế & Xe</span>
        </button>

        <button 
          onClick={() => setAdminTab('driver-trips')} 
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            adminTab === 'driver-trips' 
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low' 
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">route</span>
          <span className="text-label-lg font-bold">Quản lý Trips</span>
        </button>

        <div className="pt-4 border-t border-outline-variant/30 mt-4">
          <p className="text-[11px] font-bold text-on-surface-variant/50 px-4 uppercase tracking-wider mb-2">Demo Roles</p>
          <button 
            onClick={() => setAdminTab('driver-portal')} 
            className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
              adminTab === 'driver-portal' 
              ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low' 
              : 'text-on-surface-variant hover:bg-surface-container-low/60'
            }`}
          >
            <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">account_circle</span>
            <span className="text-label-lg font-bold">Driver Portal</span>
          </button>
        </div>
      </div>

      {/* Logout */}
      <div className="mt-auto px-4 py-2 border-t border-outline-variant/20 pt-4 text-center">
        <button 
          onClick={handleLogout}
          className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl border border-error text-error hover:bg-error/5 transition-all text-label-lg font-bold mb-3"
        >
          <span className="material-symbols-outlined text-[20px]">logout</span>
          Đăng xuất
        </button>
        <span className="text-xs text-on-surface-variant/60 font-semibold block">HMS Admin Panel v1.2</span>
      </div>
    </nav>
  );

  // Guard checks
  const token = localStorage.getItem('accessToken');
  const role = localStorage.getItem('role');

  if (!token) {
    if (currentPage === 'register') {
      return <RegisterPage onNavigate={handleNavigate} />;
    }
    return <LoginPage onNavigate={handleNavigate} />;
  }

  // Logged in rendering
  switch (currentPage) {
    case 'login':
    case 'register':
      // Handled by useEffect redirect, but return loading/spinner just in case
      return (
        <div className="min-h-screen flex items-center justify-center bg-surface text-on-surface">
          <div className="flex flex-col items-center gap-3">
            <span className="material-symbols-outlined animate-spin text-[40px] text-primary">sync</span>
            <p className="text-body-md text-on-surface-variant font-medium">Đang chuyển hướng...</p>
          </div>
        </div>
      );

    case 'home':
      return <HomePage onNavigate={handleNavigate} onLogout={handleLogout} />;

    case 'driver-portal':
      return <MatchingSuggestionPage onLogout={handleLogout} />;

    case 'driver-trips':
      return <DriverTripsPage onLogout={handleLogout} />;

    case 'admin':
      if (role !== 'Admin') {
        // Enforce admin permission restriction
        return (
          <div className="min-h-screen flex items-center justify-center bg-surface text-on-surface p-4">
            <div className="bg-surface-container-lowest border border-outline-variant rounded-xl p-8 card-shadow w-full max-w-md text-center">
              <span className="material-symbols-outlined text-error text-[48px] mb-4">gpp_maybe</span>
              <h1 className="text-headline-md font-headline-md text-on-surface mb-2">Quyền truy cập bị từ chối</h1>
              <p className="text-body-md text-on-surface-variant mb-6">Bạn không có quyền truy cập trang quản trị này.</p>
              <button 
                onClick={handleLogout}
                className="w-full bg-primary hover:bg-primary-container text-on-primary text-label-lg font-label-lg py-3 rounded-lg transition-colors"
              >
                Đăng xuất & Đăng nhập lại
              </button>
            </div>
          </div>
        );
      }

      // Render Admin workspace sub-tabs
      switch (adminTab) {
        case 'dashboard':
          return <DashboardPage sidebar={renderSidebar()} />;
        case 'create-customer':
          return <CreateCustomerPage sidebar={renderSidebar()} />;
        case 'create-driver':
          return <CreateDriverPage sidebar={renderSidebar()} />;
        case 'driver-trips':
          return <DriverTripsPage onBackToAdmin={() => setAdminTab('dashboard')} onLogout={handleLogout} />;
        case 'driver-portal':
          return <MatchingSuggestionPage onBackToAdmin={() => setAdminTab('dashboard')} onLogout={handleLogout} />;
        default:
          return <DashboardPage sidebar={renderSidebar()} />;
      }

    default:
      return <LoginPage onNavigate={handleNavigate} />;
  }
}

export default App;
