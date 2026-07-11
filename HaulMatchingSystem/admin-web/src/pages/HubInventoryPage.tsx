import { useCallback, useEffect, useState } from 'react';
import {
    fetchInventory,
    fetchDashboardSummary,
    fetchShipmentDetail,
    updateShipment,
    fetchHubsForSelector,
    type HubInventoryShipment,
    type HubInventoryDetail,
    type HubInventoryDashboard,
    type UpdateShipmentPayload,
    type InventoryQueryParams,
} from '../api/hubInventoryApi';

/* ================================================================ */
/*  Constants                                                        */
/* ================================================================ */

const STATUS_OPTIONS = [
    { value: '', label: 'Tất cả trạng thái' },
    { value: 'In_Warehouse', label: 'Đang lưu kho' },
    { value: 'Returned', label: 'Trả hàng' },
];

// const CARGO_TYPES = [
//     'Điện tử', 'Quần áo', 'Thực phẩm', 'Giày dép', 'Mỹ phẩm',
//     'Thuốc', 'Sách', 'Phụ kiện', 'Khác'
// ];

const SORT_OPTIONS = [
    { value: '', label: 'Mới nhất' },
    { value: 'oldest', label: 'Cũ nhất' },
    { value: 'weight_asc', label: 'Khối lượng tăng' },
    { value: 'weight_desc', label: 'Khối lượng giảm' },
    { value: 'longest_storage', label: 'Lưu kho lâu nhất' },
];

const DATE_RANGES = [
    { value: 'all', label: 'Tất cả' },
    { value: 'today', label: 'Hôm nay' },
    { value: '7', label: '7 ngày' },
    { value: '30', label: '30 ngày' },
    { value: 'custom', label: 'Tùy chọn' },
];

const PAGE_SIZES = [10, 20, 50, 100];

const STATUS_BADGE: Record<string, string> = {
    Draft: 'bg-gray-100 text-gray-600 border border-gray-200',
    In_Warehouse: 'bg-blue-50 text-blue-700 border border-blue-200',
    Matched: 'bg-purple-50 text-purple-700 border border-purple-200',
    Assigned: 'bg-orange-50 text-orange-700 border border-orange-200',
    Out_For_Delivery: 'bg-yellow-50 text-yellow-700 border border-yellow-200',
    Delivered: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
    Returned: 'bg-red-50 text-red-700 border border-red-200',
};

const STATUS_LABELS: Record<string, string> = {
    Draft: 'Draft',
    In_Warehouse: 'Đang lưu kho',
    Matched: 'Đã ghép',
    Assigned: 'Đã giao TC',
    Out_For_Delivery: 'Đang giao',
    Delivered: 'Đã giao',
    Returned: 'Trả hàng',
};

// const TIMELINE_ICONS: Record<string, string> = {
//     'Draft': 'edit_note',
//     'Intake Confirmed': 'inventory_2',
//     'Matching': 'alt_route',
//     'Assigned': 'person_pin',
//     'Out For Delivery': 'local_shipping',
//     'Delivered': 'check_circle',
// };

/* ================================================================ */
/*  Helper functions                                                 */
/* ================================================================ */

function formatNumber(value: number, decimals = 2) {
    return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: decimals }).format(value);
}

function formatDate(dateStr: string | null): string {
    if (!dateStr) return '—';
    const d = new Date(dateStr);
    return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

function formatDateTime(dateStr: string | null): string {
    if (!dateStr) return '—';
    const d = new Date(dateStr);
    return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

function getDateRangeParams(range: string): { fromDate?: string; toDate?: string } {
    const now = new Date();
    if (range === 'today') {
        const start = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        return { fromDate: start.toISOString() };
    }
    if (range === '7' || range === '30') {
        const days = parseInt(range);
        const start = new Date(now.getTime() - days * 86400000);
        return { fromDate: start.toISOString() };
    }
    return {};
}

/* ================================================================ */
/*  Sub-Components                                                   */
/* ================================================================ */

interface KpiCardProps {
    icon: string;
    label: string;
    value: string;
    color?: string;
    bgColor?: string;
}

function KpiCard({ icon, label, value, color = 'text-primary', bgColor = 'bg-primary/10' }: KpiCardProps) {
    return (
        <div className="bg-surface-container-lowest rounded-xl p-5 card-shadow border border-outline-variant/20 flex flex-col justify-between">
            <div className="flex justify-between items-start mb-3">
                <span className="text-xs font-bold text-on-surface-variant uppercase tracking-wider leading-tight">{label}</span>
                <div className={`w-9 h-9 rounded-lg ${bgColor} flex items-center justify-center`}>
                    <span className={`material-symbols-outlined text-[20px] ${color}`}>{icon}</span>
                </div>
            </div>
            <span className="text-on-surface" style={{ fontFamily: 'Space Grotesk, sans-serif', fontSize: '1.75rem', fontWeight: 700 }}>{value}</span>
        </div>
    );
}

function StatusBadge({ status }: { status: string }) {
    const cls = STATUS_BADGE[status] ?? 'bg-gray-100 text-gray-600';
    const label = STATUS_LABELS[status] ?? status;
    return (
        <span className={`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-bold whitespace-nowrap ${cls}`}>
            {label}
        </span>
    );
}

/* ================================================================ */
/*  Loading Skeleton                                                 */
/* ================================================================ */

function TableSkeleton({ rows = 8 }: { rows?: number }) {
    return (
        <>
            {Array.from({ length: rows }).map((_, i) => (
                <tr key={i} className="border-t border-outline-variant/10">
                    {Array.from({ length: 8 }).map((_, j) => (
                        <td key={j} className="px-4 py-3">
                            <div className="h-4 bg-surface-container-low rounded animate-pulse" />
                        </td>
                    ))}
                </tr>
            ))}
        </>
    );
}

function KpiSkeleton() {
    return (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            {Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="bg-surface-container-lowest rounded-xl p-5 card-shadow border border-outline-variant/20">
                    <div className="flex justify-between items-start mb-3">
                        <div className="h-4 w-24 bg-surface-container-low rounded animate-pulse" />
                        <div className="w-9 h-9 rounded-lg bg-surface-container-low animate-pulse" />
                    </div>
                    <div className="h-8 w-16 bg-surface-container-low rounded animate-pulse" />
                </div>
            ))}
        </div>
    );
}

/* ================================================================ */
/*  Toast Component                                                  */
/* ================================================================ */

function Toast({ message, type = 'success', onClose }: { message: string; type?: 'success' | 'error' | 'info'; onClose: () => void }) {
    useEffect(() => {
        const t = setTimeout(onClose, 3000);
        return () => clearTimeout(t);
    }, [onClose]);

    const tone = type === 'success' ? 'bg-emerald-600' : type === 'error' ? 'bg-red-600' : 'bg-slate-900';
    return (
        <div className={`text-white px-5 py-3 rounded-xl shadow-lg flex items-center gap-3 ${tone} fixed bottom-6 right-6 z-50`} role="status">
            <span className="material-symbols-outlined text-base">{type === 'success' ? 'check_circle' : type === 'error' ? 'error' : 'info'}</span>
            <span className="text-sm font-medium">{message}</span>
        </div>
    );
}

/* ================================================================ */
/*  Empty State                                                      */
/* ================================================================ */

function EmptyState({ keyword }: { keyword?: string }) {
    return (
        <tr>
            <td colSpan={8} className="px-4 py-16 text-center">
                <div className="flex flex-col items-center gap-3">
                    <div className="w-16 h-16 rounded-full bg-surface-container-low flex items-center justify-center">
                        <span className="material-symbols-outlined text-[32px] text-on-surface-variant/50">inventory_2</span>
                    </div>
                    <p className="text-on-surface-variant font-medium">
                        {keyword ? 'Không tìm thấy kiện hàng phù hợp' : 'Chưa có kiện hàng trong kho'}
                    </p>
                    <p className="text-sm text-on-surface-variant/60">
                        {keyword ? 'Thử thay đổi từ khóa hoặc bộ lọc' : 'Kiện hàng sẽ xuất hiện khi được nhập kho'}
                    </p>
                </div>
            </td>
        </tr>
    );
}

/* ================================================================ */
/*  Detail Drawer                                                    */
/* ================================================================ */

interface DrawerProps {
    shipmentId: string | null;
    onClose: () => void;
}

function DetailDrawer({ shipmentId, onClose }: DrawerProps) {
    const [detail, setDetail] = useState<HubInventoryDetail | null>(null);
    const [loading, setLoading] = useState(false);

    useEffect(() => {
        if (!shipmentId) { setDetail(null); return; }
        setLoading(true);
        fetchShipmentDetail(shipmentId)
            .then(setDetail)
            .catch(() => setDetail(null))
            .finally(() => setLoading(false));
    }, [shipmentId]);

    if (!shipmentId) return null;

    return (
        <>
            {/* Backdrop */}
            <div className="fixed inset-0 bg-black/30 z-40 transition-opacity" onClick={onClose} />

            {/* Drawer */}
            <div className="fixed top-0 right-0 h-full w-full max-w-lg bg-surface-container-lowest shadow-2xl z-50 flex flex-col border-l border-outline-variant/30">
                {/* Header */}
                <div className="flex items-center justify-between px-6 py-4 border-b border-outline-variant/20">
                    <div className="flex items-center gap-3">
                        <div className="w-9 h-9 rounded-lg bg-primary/10 flex items-center justify-center">
                            <span className="material-symbols-outlined text-[20px] text-primary">receipt_long</span>
                        </div>
                        <div>
                            <h2 className="text-headline-md font-headline-md text-on-surface">Chi tiết kiện hàng</h2>
                            {detail && <p className="text-xs text-on-surface-variant mt-0.5">{detail.qrCode}</p>}
                        </div>
                    </div>
                    <button onClick={onClose} className="w-9 h-9 rounded-lg hover:bg-surface-container-low flex items-center justify-center transition-colors">
                        <span className="material-symbols-outlined text-on-surface-variant">close</span>
                    </button>
                </div>

                {/* Content */}
                <div className="flex-1 overflow-y-auto px-6 py-5 space-y-6 custom-scrollbar">
                    {loading ? (
                        <div className="flex items-center justify-center py-16">
                            <span className="material-symbols-outlined animate-spin text-[32px] text-primary">sync</span>
                        </div>
                    ) : !detail ? (
                        <div className="text-center py-16 text-on-surface-variant">Không tìm thấy dữ liệu</div>
                    ) : (
                        <>
                            {/* Shipment Info */}
                            <Section title="Thông tin kiện hàng" icon="inventory_2">
                                <InfoRow label="QR Code" value={detail.qrCode} bold />
                                <InfoRow label="Trạng thái" value={<StatusBadge status={detail.status} />} />
                                <InfoRow label="Hub hiện tại" value={detail.currentHubName ?? '—'} />
                                <InfoRow label="Ngày nhập kho" value={formatDateTime(detail.intakeConfirmedAt)} />
                                <InfoRow label="Ngày tạo" value={formatDateTime(detail.createdAt)} />
                            </Section>

                            {/* Sender Info */}
                            <Section title="Thông tin khách gửi" icon="person">
                                <InfoRow label="Tên" value={detail.customerName ?? '—'} />
                                <InfoRow label="SĐT" value={detail.customerPhone ?? '—'} />
                            </Section>

                            {/* Receiver Info */}
                            <Section title="Thông tin người nhận" icon="location_on">
                                <InfoRow label="Tên" value={detail.receiverName} />
                                <InfoRow label="SĐT" value={detail.receiverPhone} />
                                <InfoRow label="Địa chỉ" value={detail.destAddress} />
                            </Section>

                            {/* Cargo Info */}
                            <Section title="Thông tin hàng hóa" icon="category">
                                <InfoRow label="Loại hàng" value={detail.cargoType || '—'} />
                                <InfoRow label="Khối lượng" value={`${formatNumber(detail.weightKg)} kg`} />
                                <InfoRow label="Thể tích" value={`${formatNumber(detail.volumeCbm)} CBM`} />
                                <InfoRow label="COD" value={detail.cod != null ? `${formatNumber(detail.cod, 0)} ₫` : '—'} />
                                <InfoRow label="Phí vận chuyển" value={detail.shippingFee != null ? `${formatNumber(detail.shippingFee, 0)} ₫` : '—'} />
                                <InfoRow label="Ghi chú" value={detail.specialHandlingNote || 'Không có'} />
                            </Section>

                            {/* Hub Intake Info */}
                            <Section title="Thông tin Hub" icon="warehouse">
                                <InfoRow label="Staff nhập kho" value={detail.intakeStaffName ?? '—'} />
                                <InfoRow label="Thời gian nhập" value={formatDateTime(detail.intakeConfirmedAt)} />
                                <InfoRow label="Số ngày lưu kho" value={`${detail.daysInWarehouse} ngày`} highlight={detail.daysInWarehouse > 7} />
                            </Section>
                        </>
                    )}
                </div>
            </div>
        </>
    );
}

function Section({ title, icon, children }: { title: string; icon: string; children: React.ReactNode }) {
    return (
        <div className="bg-surface-container-lowest rounded-xl border border-outline-variant/20 p-4">
            <div className="flex items-center gap-2 mb-3">
                <span className="material-symbols-outlined text-[18px] text-primary">{icon}</span>
                <h3 className="text-sm font-bold text-on-surface">{title}</h3>
            </div>
            <div className="space-y-2.5">{children}</div>
        </div>
    );
}

function InfoRow({ label, value, bold, highlight }: {
    label: string; value: React.ReactNode; bold?: boolean; highlight?: boolean;
}) {
    return (
        <div className="flex justify-between items-start gap-4">
            <span className="text-xs text-on-surface-variant shrink-0">{label}</span>
            <span className={`text-sm text-right ${bold ? 'font-bold text-on-surface' : highlight ? 'font-bold text-error' : 'text-on-surface'}`}>
                {value}
            </span>
        </div>
    );
}

/* ================================================================ */
/*  Update Modal                                                     */
/* ================================================================ */

interface UpdateModalProps {
    shipment: HubInventoryShipment | null;
    onClose: () => void;
    onUpdated: () => void;
}

function UpdateModal({ shipment, onClose, onUpdated }: UpdateModalProps) {
    const [form, setForm] = useState<UpdateShipmentPayload>({});
    const [saving, setSaving] = useState(false);

    useEffect(() => {
        if (shipment) {
            setForm({
                receiverName: shipment.receiverName,
                receiverPhone: shipment.receiverPhone,
                destAddress: shipment.destAddress,
                cargoType: shipment.cargoType,
                weightKg: shipment.weightKg,
                volumeCbm: shipment.volumeCbm,
                specialHandlingNote: shipment.specialHandlingNote ?? '',
            });
        }
    }, [shipment]);

    if (!shipment) return null;

    const handleSubmit = async () => {
        try {
            setSaving(true);
            await updateShipment(shipment.id, form);
            onUpdated();
        } catch {
            // Toast will be triggered by parent
        } finally {
            setSaving(false);
        }
    };

    return (
        <>
            <div className="fixed inset-0 bg-black/30 z-50" onClick={onClose} />
            <div className="fixed inset-0 flex items-center justify-center z-50 p-4">
                <div className="bg-surface-container-lowest rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] flex flex-col border border-outline-variant/30">
                    {/* Header */}
                    <div className="flex items-center justify-between px-6 py-4 border-b border-outline-variant/20">
                        <div className="flex items-center gap-3">
                            <div className="w-9 h-9 rounded-lg bg-primary/10 flex items-center justify-center">
                                <span className="material-symbols-outlined text-[20px] text-primary">edit</span>
                            </div>
                            <div>
                                <h2 className="text-headline-md font-headline-md text-on-surface">Cập nhật kiện hàng</h2>
                                <p className="text-xs text-on-surface-variant mt-0.5">{shipment.qrCode}</p>
                            </div>
                        </div>
                        <button onClick={onClose} className="w-9 h-9 rounded-lg hover:bg-surface-container-low flex items-center justify-center">
                            <span className="material-symbols-outlined text-on-surface-variant">close</span>
                        </button>
                    </div>

                    {/* Form */}
                    <div className="flex-1 overflow-y-auto px-6 py-5 space-y-4 custom-scrollbar">
                        <FormField label="Tên người nhận">
                            <input
                                className="input-field"
                                value={form.receiverName ?? ''}
                                onChange={e => setForm(f => ({ ...f, receiverName: e.target.value }))}
                            />
                        </FormField>
                        <FormField label="SĐT người nhận">
                            <input
                                className="input-field"
                                value={form.receiverPhone ?? ''}
                                onChange={e => setForm(f => ({ ...f, receiverPhone: e.target.value }))}
                            />
                        </FormField>
                        <FormField label="Địa chỉ giao hàng">
                            <input
                                className="input-field"
                                value={form.destAddress ?? ''}
                                onChange={e => setForm(f => ({ ...f, destAddress: e.target.value }))}
                            />
                        </FormField>
                        <div className="grid grid-cols-2 gap-4">
                            <FormField label="Khối lượng (kg)">
                                <input
                                    className="input-field"
                                    type="number"
                                    step="0.1"
                                    min="0"
                                    value={form.weightKg ?? ''}
                                    onChange={e => setForm(f => ({ ...f, weightKg: parseFloat(e.target.value) || undefined }))}
                                />
                            </FormField>
                            <FormField label="Thể tích (CBM)">
                                <input
                                    className="input-field"
                                    type="number"
                                    step="0.01"
                                    min="0"
                                    value={form.volumeCbm ?? ''}
                                    onChange={e => setForm(f => ({ ...f, volumeCbm: parseFloat(e.target.value) || undefined }))}
                                />
                            </FormField>
                        </div>
                        <FormField label="Ghi chú đặc biệt">
                            <textarea
                                className="input-field min-h-[80px] resize-y"
                                value={form.specialHandlingNote ?? ''}
                                onChange={e => setForm(f => ({ ...f, specialHandlingNote: e.target.value }))}
                                placeholder="Không bắt buộc"
                            />
                        </FormField>
                    </div>

                    {/* Footer */}
                    <div className="flex justify-end gap-3 px-6 py-4 border-t border-outline-variant/20">
                        <button onClick={onClose} className="btn-ghost">Hủy</button>
                        <button onClick={handleSubmit} disabled={saving} className="btn-primary flex items-center gap-2 disabled:opacity-60">
                            {saving && <span className="material-symbols-outlined animate-spin text-[18px]">sync</span>}
                            {saving ? 'Đang lưu...' : 'Lưu thay đổi'}
                        </button>
                    </div>
                </div>
            </div>
        </>
    );
}

function FormField({ label, children }: { label: string; children: React.ReactNode }) {
    return (
        <label className="flex flex-col gap-1.5">
            <span className="text-xs font-bold text-on-surface-variant">{label}</span>
            {children}
        </label>
    );
}

/* ================================================================ */
/*  Mobile Card for responsive                                       */
/* ================================================================ */

function ShipmentCard({
    item,
    onView,
    onEdit,
}: {
    item: HubInventoryShipment;
    onView: () => void;
    onEdit: () => void;
}) {
    return (
        <div className="bg-surface-container-lowest rounded-xl border border-outline-variant/20 p-4 card-shadow space-y-3">
            <div className="flex items-center justify-between">
                <span className="font-mono text-sm font-bold text-on-surface">{item.qrCode}</span>
                <StatusBadge status={item.status} />
            </div>
            <div className="grid grid-cols-2 gap-2 text-xs">
                <div>
                    <span className="text-on-surface-variant">Người nhận</span>
                    <p className="font-medium text-on-surface truncate">{item.receiverName}</p>
                </div>
                <div>
                    <span className="text-on-surface-variant">Loại hàng</span>
                    <p className="font-medium text-on-surface">{item.cargoType || '—'}</p>
                </div>
                <div>
                    <span className="text-on-surface-variant">Khối lượng</span>
                    <p className="font-medium text-on-surface">{formatNumber(item.weightKg)} kg</p>
                </div>
                <div>
                    <span className="text-on-surface-variant">Lưu kho</span>
                    <p className={`font-medium ${item.daysInWarehouse > 7 ? 'text-error' : 'text-on-surface'}`}>{item.daysInWarehouse} ngày</p>
                </div>
            </div>
            {item.currentHubName && (
                <div className="text-xs">
                    <span className="text-on-surface-variant">Hub: </span>
                    <span className="font-medium text-on-surface">{item.currentHubName}</span>
                </div>
            )}
            <div className="flex gap-2 pt-1">
                <button onClick={onView} className="flex-1 flex items-center justify-center gap-1.5 px-3 py-2 rounded-lg border border-outline-variant/30 text-xs font-bold text-on-surface-variant hover:bg-surface-container-low transition-colors">
                    <span className="material-symbols-outlined text-[16px]">visibility</span> Chi tiết
                </button>
                <button onClick={onEdit} className="flex-1 flex items-center justify-center gap-1.5 px-3 py-2 rounded-lg border border-primary/30 text-xs font-bold text-primary hover:bg-primary/5 transition-colors">
                    <span className="material-symbols-outlined text-[16px]">edit</span> Cập nhật
                </button>
            </div>
        </div>
    );
}

/* ================================================================ */
/*  MAIN PAGE                                                        */
/* ================================================================ */

interface HubInventoryPageProps {
    sidebar?: React.ReactNode;
}

export default function HubInventoryPage({ sidebar }: HubInventoryPageProps) {
    const role = localStorage.getItem('role');
    const isAdmin = role === 'Admin';

    // Data
    const [items, setItems] = useState<HubInventoryShipment[]>([]);
    const [dashboard, setDashboard] = useState<HubInventoryDashboard | null>(null);
    const [hubs, setHubs] = useState<{ id: string; name: string }[]>([]);
    const [totalCount, setTotalCount] = useState(0);
    const [totalPages, setTotalPages] = useState(1);

    // Query state
    const [keyword, setKeyword] = useState('');
    const [status, setStatus] = useState('');
    const [cargoType, setCargoType] = useState('');
    const [sort, setSort] = useState('');
    const [dateRange, setDateRange] = useState('all');
    const [customFromDate, setCustomFromDate] = useState('');
    const [customToDate, setCustomToDate] = useState('');
    const [selectedHubId, setSelectedHubId] = useState('');
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(20);

    // UI state
    const [loading, setLoading] = useState(false);
    const [loadingDashboard, setLoadingDashboard] = useState(false);
    const [detailId, setDetailId] = useState<string | null>(null);
    const [editItem, setEditItem] = useState<HubInventoryShipment | null>(null);
    const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

    // Load hubs for Admin
    useEffect(() => {
        if (!isAdmin) return;
        fetchHubsForSelector().then(setHubs).catch(() => {});
    }, [isAdmin]);

    // Build query params
    const buildQuery = useCallback((): InventoryQueryParams => {
        const dateParams = getDateRangeParams(dateRange);
        const params: InventoryQueryParams = {
            page,
            pageSize,
            sort: sort || undefined,
            status: status || undefined,
            cargoType: cargoType || undefined,
            keyword: keyword || undefined,
        };
        if (isAdmin && selectedHubId) params.hubId = selectedHubId;
        if (dateParams.fromDate) params.fromDate = dateParams.fromDate;
        if (dateParams.toDate) params.toDate = dateParams.toDate;

        // Custom date overrides
        if (dateRange === 'custom') {
            if (customFromDate) params.fromDate = customFromDate;
            if (customToDate) params.toDate = customToDate;
        }

        return params;
    }, [page, pageSize, keyword, status, cargoType, sort, dateRange, customFromDate, customToDate, selectedHubId, isAdmin]);

    // Load inventory
    const loadInventory = useCallback(async () => {
        setLoading(true);
        try {
            const params = buildQuery();
            const result = await fetchInventory(params);
            setItems(result.items);
            setTotalCount(result.totalCount);
            setTotalPages(result.totalPages);
        } catch (err) {
            setItems([]);
            setToast({ message: err instanceof Error ? err.message : 'Lỗi tải dữ liệu', type: 'error' });
        } finally {
            setLoading(false);
        }
    }, [buildQuery]);

    // Load dashboard
    const loadDashboard = useCallback(async () => {
        setLoadingDashboard(true);
        try {
            const hubId = isAdmin ? selectedHubId || undefined : undefined;
            const result = await fetchDashboardSummary(hubId);
            setDashboard(result);
        } catch {
            // silent
        } finally {
            setLoadingDashboard(false);
        }
    }, [isAdmin, selectedHubId]);

    // Debounced search
    useEffect(() => {
        const t = setTimeout(() => { setPage(1); loadInventory(); }, 300);
        return () => clearTimeout(t);
    }, [keyword, status, cargoType, sort, dateRange, customFromDate, customToDate, selectedHubId, pageSize]);

    // Reload on page change
    useEffect(() => { loadInventory(); }, [page]);

    // Reload dashboard on hub change
    useEffect(() => { loadDashboard(); }, [selectedHubId]);

    // Reset page when filters change
    useEffect(() => { setPage(1); }, [status, cargoType, sort, dateRange, selectedHubId]);

    const handleUpdated = () => {
        setEditItem(null);
        setToast({ message: 'Cập nhật thành công!', type: 'success' });
        loadInventory();
        loadDashboard();
    };

    const isOverdue = (item: HubInventoryShipment) =>
        item.status === 'In_Warehouse' && item.daysInWarehouse > 7;

    return (
        <div className="bg-surface text-on-surface font-body-md min-h-screen flex text-body-md overflow-x-hidden">
            {sidebar}

            <div className="flex-1 flex flex-col xl:ml-64 w-full min-w-0">
                {/* Header */}
                <header className="bg-surface-container-lowest border-b border-outline-variant h-16 w-full flex justify-between items-center px-5 md:px-8 sticky top-0 z-10">
                    <div className="flex items-center gap-2 text-headline-md font-headline-md text-primary">
                        <span className="material-symbols-outlined">warehouse</span>
                        <span className="font-bold text-on-surface">Hub Inventory</span>
                    </div>
                    <div className="text-xs text-on-surface-variant bg-surface-container-low px-3 py-1.5 rounded-full border border-outline-variant/30">
                        {totalCount} kiện hàng
                    </div>
                </header>

                <main className="flex-1 p-4 md:p-6 lg:p-8 space-y-5 max-w-[1600px] mx-auto w-full">
                    {/* KPI Cards */}
                    {loadingDashboard ? <KpiSkeleton /> : dashboard && (
                        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                            <KpiCard
                                icon="inventory_2"
                                label="Tổng kiện hàng"
                                value={String(dashboard.totalShipment)}
                                color="text-primary"
                                bgColor="bg-primary/10"
                            />
                            <KpiCard
                                icon="store"
                                label="Đang lưu kho"
                                value={String(dashboard.inWarehouse)}
                                color="text-blue-600"
                                bgColor="bg-blue-50"
                            />
                            <KpiCard
                                icon="alt_route"
                                label="Đã ghép chuyến"
                                value={String(dashboard.matched)}
                                color="text-purple-600"
                                bgColor="bg-purple-50"
                            />
                            <KpiCard
                                icon="local_shipping"
                                label="Sẵn sàng xuất"
                                value={String(dashboard.readyForDispatch)}
                                color="text-orange-600"
                                bgColor="bg-orange-50"
                            />
                            <KpiCard
                                icon="warning"
                                label="Quá hạn lưu kho"
                                value={String(dashboard.expired)}
                                color="text-red-600"
                                bgColor="bg-red-50"
                            />
                            <KpiCard
                                icon="scale"
                                label="Khối lượng lưu kho"
                                value={`${formatNumber(dashboard.totalWeight)} kg`}
                                color="text-secondary"
                                bgColor="bg-secondary/10"
                            />
                            <KpiCard
                                icon="square_foot"
                                label="Tổng CBM"
                                value={`${formatNumber(dashboard.totalVolume)} m³`}
                                color="text-tertiary"
                                bgColor="bg-tertiary/10"
                            />
                        </div>
                    )}

                    {/* Toolbar: Search + Filters */}
                    <div className="bg-surface-container-lowest rounded-xl border border-outline-variant/20 p-4 card-shadow">
                        <div className="flex flex-col lg:flex-row gap-3">
                            {/* Search */}
                            <div className="flex flex-col gap-1 flex-1 min-w-0 lg:max-w-sm">
                                <span className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Tìm kiếm</span>
                                <label className="flex h-10 items-center rounded-lg border border-outline-variant/50 bg-surface-container-low px-3">
                                    <span className="material-symbols-outlined mr-2 text-[19px] text-on-surface-variant">search</span>
                                    <input
                                        className="w-full bg-transparent text-sm outline-none text-on-surface placeholder-on-surface-variant/50"
                                        placeholder="Tìm theo QR Code, tên người nhận, SĐT..."
                                        value={keyword}
                                        onChange={e => setKeyword(e.target.value)}
                                    />
                                    {keyword && (
                                        <button onClick={() => setKeyword('')} className="ml-1">
                                            <span className="material-symbols-outlined text-[16px] text-on-surface-variant/50 hover:text-on-surface">close</span>
                                        </button>
                                    )}
                                </label>
                            </div>

                            {/* Status filter */}
                            <div className="flex flex-col gap-1">
                                <span className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Trạng thái</span>
                                <select
                                    className="h-10 rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 text-sm outline-none text-on-surface min-w-[160px]"
                                    value={status}
                                    onChange={e => setStatus(e.target.value)}
                                >
                                    {STATUS_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                                </select>
                            </div>

                            {/* Sort */}
                            <div className="flex flex-col gap-1">
                                <span className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Sắp xếp</span>
                                <select
                                    className="h-10 rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 text-sm outline-none text-on-surface min-w-[160px]"
                                    value={sort}
                                    onChange={e => setSort(e.target.value)}
                                >
                                    {SORT_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                                </select>
                            </div>
                        </div>

                        {/* Second row: Date range + Hub selector */}
                        <div className="flex flex-col lg:flex-row gap-3 mt-3">
                            {/* Date range */}
                            <div className="flex flex-col gap-1">
                                <span className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Thời gian</span>
                                <select
                                    className="h-10 rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 text-sm outline-none text-on-surface min-w-[140px]"
                                    value={dateRange}
                                    onChange={e => setDateRange(e.target.value)}
                                >
                                    {DATE_RANGES.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                                </select>
                            </div>

                            {/* Custom date inputs */}
                            {dateRange === 'custom' && (
                                <>
                                    <input
                                        type="date"
                                        className="h-10 rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 text-sm outline-none text-on-surface"
                                        value={customFromDate}
                                        onChange={e => setCustomFromDate(e.target.value)}
                                    />
                                    <span className="self-center text-on-surface-variant text-sm">đến</span>
                                    <input
                                        type="date"
                                        className="h-10 rounded-lg border border-outline-variant/50 bg-surface-container-low px-3 text-sm outline-none text-on-surface"
                                        value={customToDate}
                                        onChange={e => setCustomToDate(e.target.value)}
                                    />
                                </>
                            )}

                            {/* Hub selector (Admin only) */}
                            {isAdmin && (
                                <div className="flex flex-col gap-1">
                                    <span className="text-[10px] font-bold text-primary uppercase tracking-wider">Hub</span>
                                    <select
                                        className="h-10 rounded-lg border border-primary/30 bg-primary/5 px-3 text-sm font-medium outline-none text-primary min-w-[200px]"
                                        value={selectedHubId}
                                        onChange={e => setSelectedHubId(e.target.value)}
                                    >
                                        <option value="">Tất cả Hub</option>
                                        {hubs.map(h => <option key={h.id} value={h.id}>{h.name}</option>)}
                                    </select>
                                </div>
                            )}

                            {/* Spacer */}
                            <div className="flex-1" />

                            {/* Reset filters */}
                            <div className="flex flex-col gap-1">
                                <span className="text-[10px] font-bold text-transparent uppercase tracking-wider">_</span>
                                <button
                                    onClick={() => {
                                        setKeyword(''); setStatus(''); setCargoType('');
                                        setSort(''); setDateRange('all'); setCustomFromDate('');
                                        setCustomToDate(''); setSelectedHubId(''); setPage(1);
                                    }}
                                    className="h-10 px-4 rounded-lg border border-outline-variant/50 bg-surface-container-low text-sm text-on-surface-variant hover:bg-surface-container transition-colors flex items-center gap-1.5"
                                >
                                    <span className="material-symbols-outlined text-[16px]">restart_alt</span>
                                    Đặt lại
                                </button>
                            </div>
                        </div>
                    </div>

                    {/* Shipment Table - Desktop */}
                    <div className="hidden md:block bg-surface-container-lowest rounded-xl border border-outline-variant/20 card-shadow overflow-hidden">
                        <div className="overflow-x-auto">
                            <table className="w-full min-w-[1000px] text-left text-sm">
                                <thead className="bg-surface-container-low text-xs text-on-surface-variant uppercase tracking-wider">
                                    <tr>
                                        <th className="px-4 py-3 font-semibold">QR Code</th>
                                        <th className="px-4 py-3 font-semibold">Loại hàng</th>
                                        <th className="px-4 py-3 font-semibold">Người nhận</th>
                                        {isAdmin && <th className="px-4 py-3 font-semibold">Hub</th>}
                                        <th className="px-4 py-3 font-semibold text-right">Khối lượng</th>
                                        <th className="px-4 py-3 font-semibold">Trạng thái</th>
                                        <th className="px-4 py-3 font-semibold">Nhập kho</th>
                                        <th className="px-4 py-3 font-semibold text-center">Lưu kho</th>
                                        <th className="px-4 py-3 font-semibold text-center">Thao tác</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {loading ? (
                                        <TableSkeleton />
                                    ) : items.length === 0 ? (
                                        <EmptyState keyword={keyword} />
                                    ) : (
                                        items.map(item => (
                                            <tr
                                                key={item.id}
                                                className={`border-t border-outline-variant/10 hover:bg-surface-container-low/50 transition-colors ${
                                                    isOverdue(item) ? 'bg-red-50/30' : ''
                                                }`}
                                            >
                                                <td className="px-4 py-3">
                                                    <span className="font-mono text-xs font-bold text-primary">{item.qrCode}</span>
                                                </td>
                                                <td className="px-4 py-3 text-on-surface-variant">{item.cargoType || '—'}</td>
                                                <td className="px-4 py-3">
                                                    <p className="text-on-surface truncate max-w-[150px]">{item.receiverName}</p>
                                                </td>
                                                {isAdmin && (
                                                    <td className="px-4 py-3 text-on-surface-variant text-xs max-w-[140px] truncate">
                                                        {item.currentHubName ?? '—'}
                                                    </td>
                                                )}
                                                <td className="px-4 py-3 text-right font-medium tabular-nums">
                                                    {formatNumber(item.weightKg)} kg
                                                </td>
                                                <td className="px-4 py-3">
                                                    <StatusBadge status={item.status} />
                                                </td>
                                                <td className="px-4 py-3 text-xs text-on-surface-variant">
                                                    {formatDate(item.intakeConfirmedAt)}
                                                </td>
                                                <td className="px-4 py-3 text-center">
                                                    <span className={`text-sm font-bold tabular-nums ${
                                                        item.daysInWarehouse > 7 ? 'text-error' : 'text-on-surface'
                                                    }`}>
                                                        {item.daysInWarehouse}d
                                                    </span>
                                                </td>
                                                <td className="px-4 py-3">
                                                    <div className="flex items-center justify-center gap-1">
                                                        <button
                                                            onClick={() => setDetailId(item.id)}
                                                            className="w-8 h-8 rounded-lg hover:bg-surface-container-low flex items-center justify-center transition-colors group"
                                                            title="Xem chi tiết"
                                                        >
                                                            <span className="material-symbols-outlined text-[18px] text-on-surface-variant group-hover:text-primary">visibility</span>
                                                        </button>
                                                        <button
                                                            onClick={() => setEditItem(item)}
                                                            className="w-8 h-8 rounded-lg hover:bg-primary/5 flex items-center justify-center transition-colors group"
                                                            title="Cập nhật"
                                                        >
                                                            <span className="material-symbols-outlined text-[18px] text-on-surface-variant group-hover:text-primary">edit</span>
                                                        </button>
                                                    </div>
                                                </td>
                                            </tr>
                                        ))
                                    )}
                                </tbody>
                            </table>
                        </div>
                    </div>

                    {/* Mobile Cards */}
                    <div className="md:hidden space-y-3">
                        {loading ? (
                            <div className="flex items-center justify-center py-12">
                                <span className="material-symbols-outlined animate-spin text-[32px] text-primary">sync</span>
                            </div>
                        ) : items.length === 0 ? (
                            <div className="text-center py-16">
                                <span className="material-symbols-outlined text-[48px] text-on-surface-variant/30">inventory_2</span>
                                <p className="text-on-surface-variant mt-2">Chưa có kiện hàng</p>
                            </div>
                        ) : (
                            items.map(item => (
                                <ShipmentCard
                                    key={item.id}
                                    item={item}
                                    onView={() => setDetailId(item.id)}
                                    onEdit={() => setEditItem(item)}
                                />
                            ))
                        )}
                    </div>

                    {/* Pagination */}
                    {totalCount > 0 && (
                        <div className="flex flex-col sm:flex-row items-center justify-between gap-3 bg-surface-container-lowest rounded-xl border border-outline-variant/20 px-4 py-3 card-shadow">
                            <div className="flex items-center gap-2 text-sm text-on-surface-variant">
                                <span>Hiển thị</span>
                                <select
                                    className="h-8 rounded-lg border border-outline-variant/50 bg-surface-container-low px-2 text-xs outline-none"
                                    value={pageSize}
                                    onChange={e => { setPageSize(Number(e.target.value)); setPage(1); }}
                                >
                                    {PAGE_SIZES.map(s => <option key={s} value={s}>{s}</option>)}
                                </select>
                                <span>trong {totalCount} kết quả</span>
                            </div>

                            <div className="flex items-center gap-1">
                                <button
                                    onClick={() => setPage(p => Math.max(1, p - 1))}
                                    disabled={page <= 1}
                                    className="h-8 px-3 rounded-lg border border-outline-variant/50 bg-surface-container-low text-xs font-bold text-on-surface-variant hover:bg-surface-container disabled:opacity-40 transition-colors flex items-center gap-1"
                                >
                                    <span className="material-symbols-outlined text-[16px]">chevron_left</span>
                                    Trước
                                </button>

                                {/* Page numbers */}
                                {Array.from({ length: Math.min(5, totalPages) }).map((_, i) => {
                                    let pageNum: number;
                                    if (totalPages <= 5) {
                                        pageNum = i + 1;
                                    } else if (page <= 3) {
                                        pageNum = i + 1;
                                    } else if (page >= totalPages - 2) {
                                        pageNum = totalPages - 4 + i;
                                    } else {
                                        pageNum = page - 2 + i;
                                    }
                                    return (
                                        <button
                                            key={pageNum}
                                            onClick={() => setPage(pageNum)}
                                            className={`w-8 h-8 rounded-lg text-xs font-bold transition-colors ${
                                                page === pageNum
                                                    ? 'bg-primary text-on-primary'
                                                    : 'border border-outline-variant/50 bg-surface-container-low text-on-surface-variant hover:bg-surface-container'
                                            }`}
                                        >
                                            {pageNum}
                                        </button>
                                    );
                                })}

                                <button
                                    onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                                    disabled={page >= totalPages}
                                    className="h-8 px-3 rounded-lg border border-outline-variant/50 bg-surface-container-low text-xs font-bold text-on-surface-variant hover:bg-surface-container disabled:opacity-40 transition-colors flex items-center gap-1"
                                >
                                    Sau
                                    <span className="material-symbols-outlined text-[16px]">chevron_right</span>
                                </button>
                            </div>
                        </div>
                    )}
                </main>
            </div>

            {/* Modals & Drawers */}
            <DetailDrawer shipmentId={detailId} onClose={() => setDetailId(null)} />
            <UpdateModal shipment={editItem} onClose={() => setEditItem(null)} onUpdated={handleUpdated} />
            {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}

            {/* Inline styles for form inputs in modals */}
            <style>{`
                .input-field {
                    width: 100%;
                    padding: 0.5rem 0.75rem;
                    border-radius: 0.5rem;
                    border: 1px solid rgba(196, 197, 213, 0.5);
                    background-color: #eff4ff;
                    font-size: 0.875rem;
                    outline: none;
                    color: #0b1c30;
                    transition: border-color 0.2s, box-shadow 0.2s;
                }
                .input-field:focus {
                    border-color: #00288e;
                    box-shadow: 0 0 0 2px rgba(0, 40, 142, 0.12);
                }
                .input-field::placeholder { color: rgba(68, 70, 83, 0.5); }
            `}</style>
        </div>
    );
}
