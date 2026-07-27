import { useState, useEffect } from 'react';
import DashboardPage from './pages/DashboardPage';
import DriverProposalPage from './pages/DriverProposalPage';
import CreateCustomerPage from './pages/CreateCustomerPage';
import CreateDriverPage from './pages/CreateDriverPage';
import CreateShipmentPage from './pages/CreateShipmentPage';
import DriverTripsPage from './pages/DriverTripsPage';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import HomePage from './pages/HomePage';
import AdminLiveMapPage from './pages/AdminLiveMapPage';
import AdminHubsPage from './pages/AdminHubsPage';
import AdminTripsPage from './pages/AdminTripsPage';
import AdminVehiclesPage from './pages/AdminVehiclesPage';
import HubInventoryPage from './pages/HubInventoryPage';
import TripPostManagementPage from './pages/TripPostManagementPage';
import CreateProposalPage from './pages/CreateProposalPage';
import MyShipmentsPage from './pages/MyShipmentsPage';
import ShipmentDetailPage from './pages/ShipmentDetailPage';
import DriverTripsPageV2 from './pages/DriverTripsPageV2';
import DriverTripDetailPage from './pages/DriverTripDetailPage';
import StaffProposalManagementPage from './pages/staff/StaffProposalManagementPage';
import StaffProposalDetailPage from './pages/staff/StaffProposalDetailPage';
import StaffCreateQuotationPage from './pages/staff/StaffCreateQuotationPage';
import StaffQuotationManagementPage from './pages/staff/StaffQuotationManagementPage';
import StaffQuotationDetailPage from './pages/staff/StaffQuotationDetailPage';
import StaffPaymentMonitoringPage from './pages/staff/StaffPaymentMonitoringPage';
import type { PublicTripPost } from './api/tripPostApi';

type Page =
  | 'login'
  | 'register'
  | 'home'
  | 'create-shipment'
  | 'create-proposal'
  | 'driver-portal'
  | 'driver-trips'
  | 'my-shipments'
  | 'shipment-detail'
  | 'driver-trips-v2'
  | 'driver-trip-detail'
  | 'admin'
  | 'admin-proposals'
  | 'admin-proposal-detail'
  | 'admin-create-quotation'
  | 'admin-quotations'
  | 'admin-quotation-detail'
  | 'admin-payments'
  | 'staff'
  | 'staff-proposals'
  | 'staff-proposal-detail'
  | 'staff-create-quotation'
  | 'staff-quotations'
  | 'staff-quotation-detail'
  | 'staff-payments';
type StaffTab = 'staff-proposals' | 'staff-quotations' | 'staff-payments';
type AdminTab = 'dashboard' | 'live-map' | 'create-customer' | 'create-driver' | 'vehicles' | 'create-shipment' | 'driver-portal' | 'driver-trips' | 'admin-trips' | 'hub-intake' | 'hub-inventory' | 'hubs' | 'trip-posts' | 'admin-proposals' | 'admin-quotations' | 'admin-payments';

function App() {
  const [currentPage, setCurrentPage] = useState<Page>(() => {
    const token = localStorage.getItem('accessToken');
    if (!token) return 'login';
    const role = localStorage.getItem('role');
    if (role === 'Admin') return 'admin';
    if (role === 'Driver') return 'driver-trips-v2';
    if (role === 'Customer') return 'home';
    if (role === 'Warehouse_Staff') return 'staff';
    return 'home';
  });

  const [adminTab, setAdminTab] = useState<AdminTab>('dashboard');

  const [staffTab, setStaffTab] = useState<StaffTab>('staff-proposals');

  // Staff detail state
  const [selectedProposalId, setSelectedProposalId] = useState<string | null>(null);
  const [selectedQuotationId, setSelectedQuotationId] = useState<string | null>(null);
  const [createQuotationProposalId, setCreateQuotationProposalId] = useState<string | null>(null);

  // Proposal mode state (when creating proposal from Trip Marketplace)
  const [proposalTripPostId, setProposalTripPostId] = useState<string | null>(null);
  const [proposalTripId, setProposalTripId] = useState<string | null>(null);
  const [proposalPickupMode, setProposalPickupMode] = useState<string | null>(null);
  const [proposalTrip, setProposalTrip] = useState<PublicTripPost | null>(null);

  // Customer/Driver detail state
  const [selectedShipmentId, setSelectedShipmentId] = useState<string | null>(null);
  const [selectedTripId, setSelectedTripId] = useState<string | null>(null);

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
          setCurrentPage('driver-trips-v2');
        } else if (role === 'Customer') {
          setCurrentPage('home');
        } else if (role === 'Warehouse_Staff') {
          setCurrentPage('staff');
          setStaffTab('staff-proposals');
        } else {
          setCurrentPage('home');
        }
      }
    }
  }, [currentPage]);

  const handleNavigate = (targetPage: string) => {
    if (targetPage === 'login') {
      setCurrentPage('login');
    } else if (targetPage === 'register') {
      setCurrentPage('register');
    } else if (targetPage === 'create-shipment') {
      const token = localStorage.getItem('accessToken');
      if (!token) {
        setCurrentPage('login');
      } else {
        // Clear proposal mode when navigating to normal create-shipment
        setProposalTripPostId(null);
        setProposalTripId(null);
        setCurrentPage('create-shipment');
      }
    } else if (targetPage === 'my-shipments') {
      setCurrentPage('my-shipments');
    } else if (targetPage === 'driver-trips-v2') {
      setCurrentPage('driver-trips-v2');
    } else if (targetPage === 'home') {
      const token = localStorage.getItem('accessToken');
      const role = localStorage.getItem('role');
      if (!token) {
        setCurrentPage('login');
      } else if (role === 'Admin') {
        setCurrentPage('admin');
        setAdminTab('dashboard');
      } else if (role === 'Driver') {
        // Driver doesn't have a home page — redirect to trips
        setCurrentPage('driver-trips-v2');
      } else {
        setCurrentPage('home');
      }
    } else {
      setCurrentPage(targetPage as Page);
    }
  };

  const handleLogout = () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('fullName');
    localStorage.removeItem('role');
    setProposalTripPostId(null);
    setProposalTripId(null);
    setCurrentPage('login');
  };

  // Role-aware navigation: routes to admin-* or staff-* based on current role
  const getPortalPrefix = () => {
    const r = localStorage.getItem('role');
    return r === 'Admin' ? 'admin' : 'staff';
  };

  const handleStaffSelectProposal = (proposalId: string) => {
    setSelectedProposalId(proposalId);
    setCurrentPage(`${getPortalPrefix()}-proposal-detail` as Page);
  };

  const handleStaffCreateQuotation = (proposalId: string) => {
    setCreateQuotationProposalId(proposalId);
    setCurrentPage(`${getPortalPrefix()}-create-quotation` as Page);
  };

  const handleStaffViewQuotation = (quotationId: string) => {
    setSelectedQuotationId(quotationId);
    setCurrentPage(`${getPortalPrefix()}-quotation-detail` as Page);
  };

  const handleStaffSelectQuotation = (quotationId: string) => {
    setSelectedQuotationId(quotationId);
    setCurrentPage(`${getPortalPrefix()}-quotation-detail` as Page);
  };

  const handleStaffQuotationCreated = (quotationId: string) => {
    setSelectedQuotationId(quotationId);
    setCurrentPage(`${getPortalPrefix()}-quotation-detail` as Page);
  };

  // Navigate to My Shipments (Customer)
  const handleSelectShipment = (shipmentId: string) => {
    setSelectedShipmentId(shipmentId);
    setCurrentPage('shipment-detail');
  };

  // Navigate to Driver Trip Detail
  const handleSelectTrip = (tripId: string) => {
    setSelectedTripId(tripId);
    setCurrentPage('driver-trip-detail');
  };

  // Handle proposal creation from Trip Marketplace
  const handleNewProposal = (tripPostId: string, tripId: string, pickupMode?: string, trip?: PublicTripPost) => {
    const token = localStorage.getItem('accessToken');
    if (!token) {
      setCurrentPage('login');
      return;
    }
    setProposalTripPostId(tripPostId);
    setProposalTripId(tripId);
    setProposalPickupMode(pickupMode ?? 'Hub');
    setProposalTrip(trip ?? null);
    setCurrentPage('create-proposal');
  };

  const renderSidebar = () => (
    <>
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
          onClick={() => setAdminTab('live-map')} 
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            adminTab === 'live-map' 
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low' 
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">map</span>
          <span className="text-label-lg font-bold">Bản Đồ Live</span>
        </button>

        <button 
          onClick={() => setAdminTab('create-customer')} 
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            adminTab === 'create-customer' 
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low' 
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">group</span>
          <span className="text-label-lg font-bold">Quản lý Khách Hàng</span>
        </button>

        <button 
          onClick={() => setAdminTab('create-driver')} 
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            adminTab === 'create-driver' 
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low' 
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">person_add</span>
          <span className="text-label-lg font-bold">Quản lý Tài Xế</span>
        </button>

        <button
          onClick={() => setAdminTab('hubs')}
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            adminTab === 'hubs'
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low'
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">hub</span>
          <span className="text-label-lg font-bold">Quản lý Hub</span>
        </button>

        <button
          onClick={() => setAdminTab('hub-inventory')}
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            adminTab === 'hub-inventory'
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low'
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">warehouse</span>
          <span className="text-label-lg font-bold">Kho Hub</span>
        </button>

        <button
          onClick={() => setAdminTab('vehicles')}
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            adminTab === 'vehicles'
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low'
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">garage</span>
          <span className="text-label-lg font-bold">Quản lý Xe</span>
        </button>

        <button 
          onClick={() => setAdminTab('create-shipment')} 
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            adminTab === 'create-shipment' 
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low' 
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">add_box</span>
          <span className="text-label-lg font-bold">Tạo Vận Đơn</span>
        </button>

        <button
          onClick={() => setAdminTab('admin-trips')}
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${adminTab === 'admin-trips'
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low'
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">route</span>
          <span className="text-label-lg font-bold">Quản lý chuyến đi</span>
        </button>

        <button
          onClick={() => setAdminTab('trip-posts')}
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${adminTab === 'trip-posts'
            ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low'
            : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">article</span>
          <span className="text-label-lg font-bold">Đăng bài chuyến xe</span>
        </button>

        {/* ── Commercial Flow (Admin portal) ── */}
        <div className="pt-4 border-t border-outline-variant/30 mt-4">
          <p className="text-[11px] font-bold text-on-surface-variant/50 px-4 uppercase tracking-wider mb-2">Quy trình thương mại</p>

          <button
            onClick={() => setAdminTab('admin-proposals')}
            className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
              adminTab === 'admin-proposals'
                ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low'
                : 'text-on-surface-variant hover:bg-surface-container-low/60'
            }`}
          >
            <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">gavel</span>
            <span className="text-label-lg font-bold">Đề xuất</span>
          </button>

          <button
            onClick={() => setAdminTab('admin-quotations')}
            className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
              adminTab === 'admin-quotations'
                ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low'
                : 'text-on-surface-variant hover:bg-surface-container-low/60'
            }`}
          >
            <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">receipt_long</span>
            <span className="text-label-lg font-bold">Báo giá</span>
          </button>

          <button
            onClick={() => setAdminTab('admin-payments')}
            className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
              adminTab === 'admin-payments'
                ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low'
                : 'text-on-surface-variant hover:bg-surface-container-low/60'
            }`}
          >
            <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">payments</span>
            <span className="text-label-lg font-bold">Thanh toán</span>
          </button>
        </div>

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
      <div className="mt-auto shrink-0 px-4 py-2 border-t border-outline-variant/20 pt-4 text-center">
        <button
          onClick={handleLogout}
          className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl border border-error text-error hover:bg-error/5 transition-all text-label-lg font-bold mb-3"
        >
          <span className="material-symbols-outlined text-[20px]">logout</span>
          Đăng xuất
        </button>
      </div>
    </nav>
    <button
      aria-label="Đăng xuất"
      className="fixed right-4 top-3 z-50 flex h-10 w-10 items-center justify-center rounded-lg border border-error bg-surface-container-lowest text-error shadow-sm hover:bg-error/5 xl:hidden"
      onClick={handleLogout}
      title="Đăng xuất"
      type="button"
    >
      <span className="material-symbols-outlined text-[20px]">logout</span>
    </button>
    </>
  );

  // Staff Sidebar for Warehouse_Staff role
  const renderStaffSidebar = () => (
    <nav className="bg-surface-container-lowest border-r border-outline-variant fixed left-0 h-full w-64 flex flex-col py-6 px-4 z-20 hidden xl:flex">
      {/* Brand Logo */}
      <div className="mb-8 flex items-center gap-3 px-2">
        <div className="w-8 h-8 rounded bg-primary flex items-center justify-center text-on-primary shadow-sm">
          <span className="material-symbols-outlined text-[20px]">local_shipping</span>
        </div>
        <div>
          <h1 className="text-headline-lg font-headline-lg text-primary text-[20px] leading-tight">Ghép Chuyến</h1>
          <p className="text-label-md font-label-md text-on-surface-variant text-[12px]">Staff Portal</p>
        </div>
      </div>

      {/* Nav List */}
      <div className="flex-1 space-y-2">
        <button
          onClick={() => { setStaffTab('staff-proposals'); setCurrentPage('staff'); }}
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            staffTab === 'staff-proposals'
              ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low'
              : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">gavel</span>
          <span className="text-label-lg font-bold">Đề xuất</span>
        </button>

        <button
          onClick={() => { setStaffTab('staff-quotations'); setCurrentPage('staff'); }}
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            staffTab === 'staff-quotations'
              ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low'
              : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">receipt_long</span>
          <span className="text-label-lg font-bold">Báo giá</span>
        </button>

        <button
          onClick={() => { setStaffTab('staff-payments'); setCurrentPage('staff'); }}
          className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group text-left ${
            staffTab === 'staff-payments'
              ? 'text-primary font-bold border-r-4 border-primary bg-surface-container-low'
              : 'text-on-surface-variant hover:bg-surface-container-low/60'
          }`}
        >
          <span className="material-symbols-outlined text-[20px] group-hover:scale-105 transition-transform">payments</span>
          <span className="text-label-lg font-bold">Thanh toán</span>
        </button>
      </div>

      {/* Logout */}
      <div className="mt-auto shrink-0 px-4 py-2 border-t border-outline-variant/20 pt-4 text-center">
        <button
          onClick={handleLogout}
          className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl border border-error text-error hover:bg-error/5 transition-all text-label-lg font-bold mb-3"
        >
          <span className="material-symbols-outlined text-[20px]">logout</span>
          Đăng xuất
        </button>
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
      return <HomePage onNavigate={handleNavigate} onNewProposal={handleNewProposal} onLogout={handleLogout} />;

    case 'driver-portal':
      return <DriverProposalPage onLogout={handleLogout} />;

    case 'driver-trips':
      return <DriverTripsPage onLogout={handleLogout} />;

    case 'my-shipments':
      if (role !== 'Customer') {
        setCurrentPage('login');
        return null;
      }
      return (
        <MyShipmentsPage
          onSelectShipment={handleSelectShipment}
          onLogout={handleLogout}
          onNavigate={(p) => {
            if (p === 'home') setCurrentPage('home');
            else if (p === 'create-shipment') setCurrentPage('create-shipment');
            else setCurrentPage(p as Page);
          }}
        />
      );

    case 'shipment-detail':
      if (!selectedShipmentId || role !== 'Customer') {
        setCurrentPage('my-shipments');
        return null;
      }
      return (
        <ShipmentDetailPage
          shipmentId={selectedShipmentId}
          onBack={() => setCurrentPage('my-shipments')}
          onLogout={handleLogout}
        />
      );

    case 'driver-trips-v2':
      if (role !== 'Driver') {
        setCurrentPage('login');
        return null;
      }
      return (
        <DriverTripsPageV2
          onSelectTrip={handleSelectTrip}
          onLogout={handleLogout}
          onNavigate={(p) => {
            if (p === 'home') setCurrentPage('driver-trips-v2');
            else setCurrentPage(p as Page);
          }}
        />
      );

    case 'driver-trip-detail':
      if (!selectedTripId || role !== 'Driver') {
        setCurrentPage('driver-trips-v2');
        return null;
      }
      return (
        <DriverTripDetailPage
          tripId={selectedTripId}
          onBack={() => setCurrentPage('driver-trips-v2')}
          onLogout={handleLogout}
        />
      );

    case 'create-proposal':
      if (proposalTrip && proposalTripPostId) {
        return (
          <CreateProposalPage
            trip={proposalTrip}
            tripPostId={proposalTripPostId}
            onBack={() => setCurrentPage('home')}
            onLogout={handleLogout}
          />
        );
      }
      // Fallback: no trip data, redirect home
      return <HomePage onNavigate={handleNavigate} onNewProposal={handleNewProposal} onLogout={handleLogout} />;

    case 'create-shipment':
      return (
        <CreateShipmentPage
          onNavigate={handleNavigate}
          proposalTripPostId={proposalTripPostId}
          proposalTripId={proposalTripId}
          pickupMode={proposalPickupMode}
        />
      );

    case 'staff':
      if (role !== 'Warehouse_Staff') {
        return (
          <div className="min-h-screen flex items-center justify-center bg-surface text-on-surface p-4">
            <div className="bg-surface-container-lowest border border-outline-variant rounded-xl p-8 card-shadow w-full max-w-md text-center">
              <span className="material-symbols-outlined text-error text-[48px] mb-4">gpp_maybe</span>
              <h1 className="text-headline-md font-headline-md text-on-surface mb-2">Quyền truy cập bị từ chối</h1>
              <p className="text-body-md text-on-surface-variant mb-6">Bạn không có quyền truy cập trang nhân viên kho.</p>
              <div className="flex gap-3">
                <button
                  onClick={() => setCurrentPage('home')}
                  className="w-1/2 bg-outline hover:bg-surface-variant text-on-surface text-label-lg font-bold py-3 rounded-lg transition-colors border border-outline-variant"
                >
                  Trang chủ
                </button>
                <button
                  onClick={handleLogout}
                  className="w-1/2 bg-primary hover:bg-primary/95 text-on-primary text-label-lg font-bold py-3 rounded-lg transition-colors"
                >
                  Đăng xuất
                </button>
              </div>
            </div>
          </div>
        );
      }
      // Render Staff workspace sub-tabs
      switch (staffTab) {
        case 'staff-proposals':
          return (
            <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
              {renderStaffSidebar()}
              <div className="flex-1 flex flex-col xl:ml-64 w-full overflow-y-auto">
                <StaffProposalManagementPage
                  onLogout={handleLogout}
                  onSelectProposal={handleStaffSelectProposal}
                  onCreateQuotation={handleStaffCreateQuotation}
                  onViewQuotation={handleStaffViewQuotation}
                />
              </div>
            </div>
          );
        case 'staff-quotations':
          return (
            <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
              {renderStaffSidebar()}
              <div className="flex-1 flex flex-col xl:ml-64 w-full overflow-y-auto">
                <StaffQuotationManagementPage
                  onLogout={handleLogout}
                  onSelectQuotation={handleStaffSelectQuotation}
                />
              </div>
            </div>
          );
        case 'staff-payments':
          return (
            <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
              {renderStaffSidebar()}
              <div className="flex-1 flex flex-col xl:ml-64 w-full overflow-y-auto">
                <StaffPaymentMonitoringPage onLogout={handleLogout} />
              </div>
            </div>
          );
        default:
          return (
            <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
              {renderStaffSidebar()}
              <div className="flex-1 flex flex-col xl:ml-64 w-full overflow-y-auto">
                <StaffProposalManagementPage
                  onLogout={handleLogout}
                  onSelectProposal={handleStaffSelectProposal}
                  onCreateQuotation={handleStaffCreateQuotation}
                  onViewQuotation={handleStaffViewQuotation}
                />
              </div>
            </div>
          );
      }

    case 'staff-proposals':
      return (
        <StaffProposalManagementPage
          onLogout={handleLogout}
          onSelectProposal={handleStaffSelectProposal}
          onCreateQuotation={handleStaffCreateQuotation}
          onViewQuotation={handleStaffViewQuotation}
        />
      );

    case 'staff-proposal-detail':
      if (!selectedProposalId) {
        setCurrentPage('staff');
        return null;
      }
      return (
        <StaffProposalDetailPage
          proposalId={selectedProposalId}
          onBack={() => setCurrentPage('staff')}
          onLogout={handleLogout}
          onCreateQuotation={handleStaffCreateQuotation}
          onViewQuotation={handleStaffViewQuotation}
        />
      );

    case 'staff-create-quotation':
      if (!createQuotationProposalId) {
        setCurrentPage('staff');
        return null;
      }
      return (
        <StaffCreateQuotationPage
          proposalId={createQuotationProposalId}
          onBack={() => setCurrentPage('staff-proposal-detail')}
          onLogout={handleLogout}
          onCreated={handleStaffQuotationCreated}
        />
      );

    case 'staff-quotations':
      return (
        <StaffQuotationManagementPage
          onLogout={handleLogout}
          onSelectQuotation={handleStaffSelectQuotation}
        />
      );

    case 'staff-quotation-detail':
      if (!selectedQuotationId) {
        setCurrentPage('staff');
        return null;
      }
      return (
        <StaffQuotationDetailPage
          quotationId={selectedQuotationId}
          onBack={() => setCurrentPage('staff')}
          onLogout={handleLogout}
        />
      );

    case 'staff-payments':
      return <StaffPaymentMonitoringPage onLogout={handleLogout} />;

    case 'admin':
      if (role !== 'Admin') {
        // Enforce admin permission restriction
        return (
          <div className="min-h-screen flex items-center justify-center bg-surface text-on-surface p-4">
            <div className="bg-surface-container-lowest border border-outline-variant rounded-xl p-8 card-shadow w-full max-w-md text-center">
              <span className="material-symbols-outlined text-error text-[48px] mb-4">gpp_maybe</span>
              <h1 className="text-headline-md font-headline-md text-on-surface mb-2">Quyền truy cập bị từ chối</h1>
              <p className="text-body-md text-on-surface-variant mb-6">Bạn không có quyền truy cập trang quản trị này.</p>
              <div className="flex gap-3">
                <button 
                  onClick={() => setCurrentPage('home')}
                  className="w-1/2 bg-outline hover:bg-surface-variant text-on-surface text-label-lg font-bold py-3 rounded-lg transition-colors border border-outline-variant"
                >
                  Trang chủ
                </button>
                <button 
                  onClick={handleLogout}
                  className="w-1/2 bg-primary hover:bg-primary/95 text-on-primary text-label-lg font-bold py-3 rounded-lg transition-colors"
                >
                  Đăng xuất
                </button>
              </div>
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
        case 'hubs':
          return (
            <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
              {renderSidebar()}
              <div className="flex-1 flex flex-col xl:ml-64 w-full overflow-y-auto">
                <AdminHubsPage />
              </div>
            </div>
          );
        case 'vehicles':
          return (
            <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
              {renderSidebar()}
              <div className="flex-1 flex flex-col xl:ml-64 w-full overflow-y-auto">
                <AdminVehiclesPage />
              </div>
            </div>
          );
        case 'create-shipment':
          return (
            <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
              {renderSidebar()}
              <div className="flex-1 flex flex-col xl:ml-64 w-full p-8 overflow-y-auto">
                <CreateShipmentPage onNavigate={handleNavigate} />
              </div>
            </div>
          );
        case 'driver-trips':
          return <DriverTripsPage onBackToAdmin={() => setAdminTab('dashboard')} onLogout={handleLogout} />;
        case 'admin-trips':
          return <AdminTripsPage sidebar={renderSidebar()} />;
        case 'trip-posts':
          return <TripPostManagementPage sidebar={renderSidebar()} />;
        case 'hub-inventory':
          return <HubInventoryPage sidebar={renderSidebar()} />;
        case 'driver-portal':
          return <DriverProposalPage onBackToAdmin={() => setAdminTab('dashboard')} onLogout={handleLogout} />;
        case 'live-map':
          return <AdminLiveMapPage sidebar={renderSidebar()} />;

        // ── Admin commercial flow pages (shared components with admin sidebar) ──
        case 'admin-proposals':
          return (
            <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
              {renderSidebar()}
              <div className="flex-1 flex flex-col xl:ml-64 w-full overflow-y-auto">
                <StaffProposalManagementPage
                  onLogout={handleLogout}
                  onSelectProposal={handleStaffSelectProposal}
                  onCreateQuotation={handleStaffCreateQuotation}
                  onViewQuotation={handleStaffViewQuotation}
                />
              </div>
            </div>
          );
        case 'admin-proposal-detail':
          if (!selectedProposalId) {
            setAdminTab('admin-proposals');
            return null;
          }
          return (
            <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
              {renderSidebar()}
              <div className="flex-1 flex flex-col xl:ml-64 w-full overflow-y-auto">
                <StaffProposalDetailPage
                  proposalId={selectedProposalId}
                  onBack={() => setAdminTab('admin-proposals')}
                  onLogout={handleLogout}
                  onCreateQuotation={handleStaffCreateQuotation}
                  onViewQuotation={handleStaffViewQuotation}
                />
              </div>
            </div>
          );
        case 'admin-create-quotation':
          if (!createQuotationProposalId) {
            setAdminTab('admin-proposals');
            return null;
          }
          return (
            <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
              {renderSidebar()}
              <div className="flex-1 flex flex-col xl:ml-64 w-full overflow-y-auto">
                <StaffCreateQuotationPage
                  proposalId={createQuotationProposalId}
                  onBack={() => setAdminTab('admin-proposals')}
                  onLogout={handleLogout}
                  onCreated={handleStaffQuotationCreated}
                />
              </div>
            </div>
          );
        case 'admin-quotations':
          return (
            <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
              {renderSidebar()}
              <div className="flex-1 flex flex-col xl:ml-64 w-full overflow-y-auto">
                <StaffQuotationManagementPage
                  onLogout={handleLogout}
                  onSelectQuotation={handleStaffSelectQuotation}
                />
              </div>
            </div>
          );
        case 'admin-quotation-detail':
          if (!selectedQuotationId) {
            setAdminTab('admin-quotations');
            return null;
          }
          return (
            <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
              {renderSidebar()}
              <div className="flex-1 flex flex-col xl:ml-64 w-full overflow-y-auto">
                <StaffQuotationDetailPage
                  quotationId={selectedQuotationId}
                  onBack={() => setAdminTab('admin-quotations')}
                  onLogout={handleLogout}
                />
              </div>
            </div>
          );
        case 'admin-payments':
          return (
            <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden relative">
              {renderSidebar()}
              <div className="flex-1 flex flex-col xl:ml-64 w-full overflow-y-auto">
                <StaffPaymentMonitoringPage onLogout={handleLogout} />
              </div>
            </div>
          );

        default:
          return <DashboardPage sidebar={renderSidebar()} />;
      }

    default:
      return <LoginPage onNavigate={handleNavigate} />;
  }
}

export default App;
