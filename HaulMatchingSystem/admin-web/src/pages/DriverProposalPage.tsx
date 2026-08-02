import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import TripCapacitySummary from '../components/matching/TripCapacitySummary';
import ProposalCard from '../components/matching/ProposalCard';
import RejectProposalDialog from '../components/matching/RejectProposalDialog';
import ConfirmDialog from '../components/matching/ConfirmDialog';
import Toast, { type ToastType } from '../components/matching/Toast';
import {
    fetchDriverPendingProposals,
    acceptProposal,
    rejectProposal,
    acceptAllProposals,
    type DriverProposalsResponse,
    type ProposalDto
} from '../api/proposalApi';

const apiBaseUrl =
    import.meta.env.VITE_API_BASE_URL ??
    import.meta.env.VITE_API_URL ??
    'http://localhost:5104';

interface DriverProposalPageProps {
    onBackToAdmin?: () => void;
    onLogout?: () => void;
}

export default function DriverProposalPage({ onBackToAdmin, onLogout }: DriverProposalPageProps) {
    const [data, setData] = useState<DriverProposalsResponse | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    // Reject dialog state
    const [rejectDialogOpen, setRejectDialogOpen] = useState(false);
    const [rejectingProposalId, setRejectingProposalId] = useState<string | null>(null);

    // Accept-all confirm dialog
    const [acceptAllConfirmOpen, setAcceptAllConfirmOpen] = useState(false);

    // Toast state
    const [toasts, setToasts] = useState<Array<{ id: string; message: string; type: ToastType }>>([]);

    const currentRole = localStorage.getItem('role');
    const proposalCount = data?.proposals?.length ?? 0;

    function pushToast(message: string, type: ToastType = 'info') {
        const id = crypto.randomUUID();
        setToasts(prev => [...prev, { id, message, type }]);
    }

    // Load proposals
    async function load() {
        setLoading(true);
        setError(null);
        try {
            const res = await fetchDriverPendingProposals();
            setData(res);
        } catch (ex: any) {
            const msg = ex?.message ?? 'Lỗi tải dữ liệu';
            setError(msg);
            pushToast(msg, 'error');
        } finally {
            setLoading(false);
        }
    }

    // SignalR connection for real-time updates
    useEffect(() => {
        let cancelled = false;
        let conn: signalR.HubConnection | null = null;

        async function init() {
            // First, load data (this triggers authFetch which refreshes expired token)
            await load();
            if (cancelled) return;

            // Read the (possibly refreshed) token from localStorage AFTER load
            const freshToken = localStorage.getItem('accessToken');
            if (!freshToken || currentRole !== 'Driver') return;

            conn = new signalR.HubConnectionBuilder()
                .withUrl(apiBaseUrl + '/hub/fleet', {
                    // Always read the freshest token from localStorage
                    accessTokenFactory: () => localStorage.getItem('accessToken') || ''
                })
                .withAutomaticReconnect()
                .configureLogging(signalR.LogLevel.Warning)
                .build();

            // When a new proposal arrives for this driver
            conn.on('NewShipmentProposal', () => {
                void load();
                pushToast('📦 Có đề xuất lô hàng mới', 'info');
            });

            // When a proposal is cancelled by customer
            conn.on('ShipmentProposalCancelled', () => {
                void load();
                pushToast('Đề xuất đã bị hủy bởi khách hàng', 'info');
            });

            // When trip capacity changes (another proposal accepted)
            conn.on('TripCapacityUpdated', () => {
                void load();
            });

            try {
                await conn.start();
            } catch (err) {
                console.error('Hub start error', err);
            }
        }

        void init();

        return () => {
            cancelled = true;
            conn?.stop();
        };
    }, [currentRole]);

    // ── Accept a single proposal ──
    async function handleAcceptOne(proposalId: string) {
        try {
            await acceptProposal(proposalId);
            await load();
            pushToast('✅ Đã chấp nhận đề xuất', 'success');
        } catch (e: any) {
            pushToast(e?.message ?? 'Không thể chấp nhận', 'error');
        }
    }

    // ── Open reject dialog ──
    function handleOpenReject(proposalId: string) {
        setRejectingProposalId(proposalId);
        setRejectDialogOpen(true);
    }

    // ── Confirm reject with reason ──
    async function handleConfirmReject(reason: string) {
        if (!rejectingProposalId) return;
        try {
            await rejectProposal(rejectingProposalId, reason);
            setRejectDialogOpen(false);
            setRejectingProposalId(null);
            await load();
            pushToast('Đã từ chối đề xuất', 'success');
        } catch (e: any) {
            pushToast(e?.message ?? 'Không thể từ chối', 'error');
        }
    }

    // ── Accept all proposals ──
    function handleAcceptAllClick() {
        setAcceptAllConfirmOpen(true);
    }

    async function handleConfirmAcceptAll() {
        if (!data?.tripId) return;
        try {
            await acceptAllProposals(data.tripId);
            setAcceptAllConfirmOpen(false);
            await load();
            pushToast(`✅ Đã chấp nhận tất cả ${proposalCount} đề xuất`, 'success');
        } catch (e: any) {
            pushToast(e?.message ?? 'Không thể chấp nhận tất cả', 'error');
        }
    }

    return (
        <div className="min-h-screen bg-surface text-on-surface lg:flex lg:overflow-hidden">
            {/* ── Sidebar ── */}
            <aside className="hidden lg:flex w-64 flex-col border-r border-outline-variant bg-surface p-4 shadow-sm">
                <div className="mb-8 px-2">
                    <h1 className="text-[24px] font-black text-primary">Cổng tài xế</h1>
                    <p className="mt-1 text-sm text-on-surface-variant">Quản lý đề xuất ghép chuyến</p>
                </div>

                <div className="flex-1 space-y-2">
                    <button className="flex w-full items-center gap-3 rounded-xl bg-primary-container px-4 py-3 text-left text-on-primary-container font-semibold">
                        <span className="material-symbols-outlined" style={{ fontVariationSettings: "'FILL' 1" }}>inbox</span>
                        <span>Đề xuất chờ xử lý</span>
                        {proposalCount > 0 && (
                            <span className="ml-auto bg-primary text-on-primary text-xs font-bold px-2 py-0.5 rounded-full">
                                {proposalCount}
                            </span>
                        )}
                    </button>
                </div>

                <div className="mt-auto border-t border-outline-variant pt-4">
                    <button
                        onClick={onLogout}
                        className="flex w-full items-center justify-center gap-2 rounded-xl border border-outline px-4 py-2.5 text-sm font-semibold text-on-surface hover:bg-surface-container"
                    >
                        <span className="material-symbols-outlined text-[18px]">power_settings_new</span>
                        Ngắt kết nối
                    </button>
                </div>
            </aside>

            {/* ── Main Content ── */}
            <div className="flex min-h-screen flex-1 flex-col overflow-hidden">
                {/* Mobile header */}
                <header className="flex items-center justify-between border-b border-outline-variant bg-surface px-4 py-3 lg:hidden">
                    <h1 className="text-[20px] font-bold text-primary">Đề Xuất Ghép Chuyến</h1>
                    <div className="flex gap-3 text-on-surface-variant">
                        <span className="material-symbols-outlined">notifications</span>
                        <span className="material-symbols-outlined">account_circle</span>
                    </div>
                </header>

                <main className="flex-1 overflow-y-auto p-5 lg:p-8">
                    <div className="max-w-3xl mx-auto space-y-6">
                        {/* Page title */}
                        <div className="flex items-start justify-between gap-3">
                            <div>
                                <h2 className="text-[22px] font-bold text-on-surface">Đề Xuất Ghép Chuyến</h2>
                                <p className="mt-1 text-sm text-on-surface-variant">
                                    Xem và xử lý các đề xuất ghép lô hàng từ khách hàng
                                </p>
                            </div>
                            {onBackToAdmin && (
                                <button
                                    onClick={onBackToAdmin}
                                    className="rounded-lg border border-outline-variant px-3 py-2 text-xs font-semibold text-on-surface hover:bg-surface-container"
                                >
                                    Bảng quản trị
                                </button>
                            )}
                        </div>

                        {/* Loading / Error / Empty states */}
                        {loading && (
                            <div className="rounded-xl bg-surface-container-low px-4 py-6 text-center text-on-surface-variant">
                                <span className="material-symbols-outlined animate-spin text-[28px] text-primary">sync</span>
                                <p className="mt-2 text-sm">Đang tải đề xuất...</p>
                            </div>
                        )}

                        {error && (
                            <div className="rounded-xl bg-error-container px-4 py-3 text-sm text-on-error-container">
                                {error}
                            </div>
                        )}

                        {!loading && !error && !data && (
                            <div className="rounded-2xl border border-dashed border-outline-variant bg-surface p-8 text-center">
                                <span className="material-symbols-outlined text-[48px] text-on-surface-variant/30">inbox</span>
                                <p className="mt-3 text-on-surface-variant font-medium">Chưa có đề xuất nào</p>
                                <p className="mt-1 text-sm text-on-surface-variant/70">
                                    Khi khách hàng gửi đề xuất ghép lô hàng, chúng sẽ hiển thị ở đây.
                                </p>
                            </div>
                        )}

                        {/* ── Data Content ── */}
                        {data && !loading && (
                            <>
                                {/* Trip Capacity Summary */}
                                <TripCapacitySummary
                                    currentWeight={data.currentLoadWeight}
                                    currentVolume={data.currentLoadVolume}
                                    remainingWeight={data.remainingWeightCapacity}
                                    remainingVolume={data.remainingVolumeCapacity}
                                />

                                {/* Proposals List */}
                                <div className="rounded-2xl border border-outline-variant/50 bg-surface p-5 shadow-sm">
                                    <div className="flex items-center justify-between mb-4">
                                        <h3 className="text-base font-semibold text-on-surface">Đề Xuất Chờ Xử Lý</h3>
                                        <span className="rounded-full bg-surface-variant px-3 py-1 text-xs font-semibold text-on-surface-variant">
                                            {proposalCount} đề xuất
                                        </span>
                                    </div>

                                    {proposalCount === 0 ? (
                                        <div className="text-center py-8 text-on-surface-variant text-sm">
                                            <span className="material-symbols-outlined text-[40px] text-on-surface-variant/30">check_circle</span>
                                            <p className="mt-2">Không có đề xuất chờ xử lý</p>
                                        </div>
                                    ) : (
                                        <div className="space-y-4">
                                            {data.proposals.map((proposal: ProposalDto) => (
                                                <ProposalCard
                                                    key={proposal.proposalId}
                                                    proposal={proposal}
                                                    onAccept={handleAcceptOne}
                                                    onReject={handleOpenReject}
                                                />
                                            ))}
                                        </div>
                                    )}
                                </div>

                                {/* Accept All button */}
                                {proposalCount > 1 && (
                                    <div className="flex justify-end">
                                        <button
                                            onClick={handleAcceptAllClick}
                                            className="flex items-center gap-2 px-6 py-3 rounded-xl bg-primary text-on-primary font-bold text-label-lg hover:bg-primary/90 transition-all shadow-sm"
                                        >
                                            <span className="material-symbols-outlined text-[20px]">done_all</span>
                                            Chấp nhận tất cả ({proposalCount})
                                        </button>
                                    </div>
                                )}
                            </>
                        )}
                    </div>
                </main>
            </div>

            {/* ── Reject Proposal Dialog ── */}
            <RejectProposalDialog
                open={rejectDialogOpen}
                onConfirm={handleConfirmReject}
                onCancel={() => { setRejectDialogOpen(false); setRejectingProposalId(null); }}
            />

            {/* ── Accept All Confirm Dialog ── */}
            <ConfirmDialog
                title="Chấp nhận tất cả đề xuất"
                open={acceptAllConfirmOpen}
                onConfirm={handleConfirmAcceptAll}
                onCancel={() => setAcceptAllConfirmOpen(false)}
            >
                Bạn có chắc muốn chấp nhận tất cả {proposalCount} đề xuất? 
                Hành động này sẽ thêm tất cả lô hàng vào chuyến đi của bạn.
            </ConfirmDialog>

            {/* ── Toasts ── */}
            <div className="fixed right-6 top-6 z-50 flex flex-col gap-3">
                {toasts.map(t => (
                    <Toast key={t.id} message={t.message} type={t.type} onClose={() => setToasts(prev => prev.filter(x => x.id !== t.id))} />
                ))}
            </div>
        </div>
    );
}
