/* ─── Shared Data Shape ───────────────────────────────────────────── */

export interface ShipmentFormData {
  // Sender
  senderName: string;
  senderPhone: string;
  pickupAddress: string;

  // Receiver
  receiverName: string;
  receiverPhone: string;
  destAddress: string;

  // Cargo
  cargoType: string;
  weightKg: number | string;
  volumeCbm: number | string;
}

/* ─── Props ────────────────────────────────────────────────────────── */

export interface ShipmentFormFieldsProps {
  /** Current form data */
  data: ShipmentFormData;
  /** Called whenever any field changes */
  onChange: (key: keyof ShipmentFormData, value: string | number) => void;

  /* ── Feature flags (default: false) ── */

  /** Show "Ghi chú nhận hàng" input below pickupAddress */
  showPickupNote?: boolean;
  pickupNote?: string;
  onPickupNoteChange?: (value: string) => void;

  /** Show cargo type as a dropdown (driver) vs free-text (customer) */
  cargoTypeAsDropdown?: boolean;
  /** Options for the dropdown when cargoTypeAsDropdown=true */
  cargoTypeOptions?: { value: string; label: string }[];

  /** Show "Mô tả hàng hóa" textarea (driver) */
  showDescription?: boolean;
  description?: string;
  onDescriptionChange?: (value: string) => void;

  /** Show "Số kiện" input (driver) */
  showQuantity?: boolean;
  quantity?: number;
  onQuantityChange?: (value: number) => void;

  /** Show "Thu hộ (COD)" checkbox (driver) */
  showCod?: boolean;
  codRequired?: boolean;
  onCodChange?: (value: boolean) => void;

  /** Show "Ghi chú" textarea (driver) */
  showNote?: boolean;
  note?: string;
  onNoteChange?: (value: string) => void;

  /** Show "Ghi chú đặc biệt" input (customer) */
  showSpecialHandlingNote?: boolean;
  specialHandlingNote?: string;
  onSpecialHandlingNoteChange?: (value: string) => void;

  /** Show destination geocoding button (customer) */
  showGeocode?: boolean;
  geocoding?: boolean;
  destResolved?: boolean;
  onGeocode?: () => void;
  /** Display name returned by geocode API */
  destResolvedName?: string;

  /** Capacity warnings for trip-post proposals */
  remainingWeight?: number;
  remainingVolume?: number;

  /** Hide specific sections */
  hideSenderSection?: boolean;
}

/* ─── CARGO CATEGORY OPTIONS ─────────────────────────────────────── */

export const CARGO_CATEGORIES = [
  { value: 'Hàng khô', label: 'Hàng khô' },
  { value: 'Hàng đông lạnh', label: 'Hàng đông lạnh' },
  { value: 'Hàng dễ vỡ', label: 'Hàng dễ vỡ' },
  { value: 'Hàng nặng/cồng kềnh', label: 'Hàng nặng/cồng kềnh' },
  { value: 'Hàng hóa chất', label: 'Hàng hóa chất' },
  { value: 'Hàng điện tử', label: 'Hàng điện tử' },
  { value: 'Thực phẩm', label: 'Thực phẩm' },
  { value: 'Khác', label: 'Khác' },
];

/* ─── Styles ──────────────────────────────────────────────────────── */

const inputClass =
  'w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#00288e]/30 focus:border-[#00288e] transition-all';

const labelClass = 'block text-xs font-semibold text-gray-500 mb-1';

const textAreaClass =
  'w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#00288e]/30 focus:border-[#00288e] transition-all resize-none';

/* ─── Component ───────────────────────────────────────────────────── */

export default function ShipmentFormFields({
  data,
  onChange,

  showPickupNote,
  pickupNote,
  onPickupNoteChange,

  cargoTypeAsDropdown,
  cargoTypeOptions = CARGO_CATEGORIES,

  showDescription,
  description,
  onDescriptionChange,

  showQuantity,
  quantity,
  onQuantityChange,

  showCod,
  codRequired,
  onCodChange,

  showNote,
  note,
  onNoteChange,

  showSpecialHandlingNote,
  specialHandlingNote,
  onSpecialHandlingNoteChange,

  showGeocode,
  geocoding,
  destResolved,
  onGeocode,
  destResolvedName,

  remainingWeight,
  remainingVolume,
}: ShipmentFormFieldsProps) {
  /* ── Sender Section ──────────────────────────────────────────────── */

  const renderSenderSection = () => (
    <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
      <div className="flex items-center gap-2.5 mb-4">
        <div className="w-8 h-8 bg-amber-50 rounded-lg flex items-center justify-center">
          <span className="material-symbols-outlined text-amber-600 text-[18px]">person_pin_circle</span>
        </div>
        <div>
          <h3 className="text-sm font-bold text-gray-800">Thông tin người gửi</h3>
          {showPickupNote && (
            <p className="text-[11px] text-amber-600 font-medium">Tài xế sẽ đến nhận hàng tại địa chỉ của bạn</p>
          )}
        </div>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div>
          <label className={labelClass}>Họ tên người gửi *</label>
          <input
            type="text"
            value={data.senderName}
            onChange={(e) => onChange('senderName', e.target.value)}
            placeholder="Nguyễn Văn A"
            className={inputClass}
          />
        </div>
        <div>
          <label className={labelClass}>Số điện thoại *</label>
          <input
            type="tel"
            value={data.senderPhone}
            onChange={(e) => onChange('senderPhone', e.target.value)}
            placeholder="0912 345 678"
            className={inputClass}
          />
        </div>
        <div className="sm:col-span-2">
          <label className={labelClass}>Địa chỉ nhận hàng *</label>
          <input
            type="text"
            value={data.pickupAddress}
            onChange={(e) => onChange('pickupAddress', e.target.value)}
            placeholder="123 Đường ABC, Quận XYZ, TP.HCM"
            className={inputClass}
          />
        </div>
        {showPickupNote && (
          <div className="sm:col-span-2">
            <label className={labelClass}>Ghi chú nhận hàng</label>
            <input
              type="text"
              value={pickupNote ?? ''}
              onChange={(e) => onPickupNoteChange?.(e.target.value)}
              placeholder="Ghi chú thêm cho tài xế (tùy chọn)"
              className={inputClass}
            />
          </div>
        )}
      </div>
    </div>
  );

  /* ── Receiver Section ────────────────────────────────────────────── */

  const renderReceiverSection = () => (
    <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
      <div className="flex items-center gap-2.5 mb-4">
        <div className="w-8 h-8 bg-blue-50 rounded-lg flex items-center justify-center">
          <span className="material-symbols-outlined text-blue-600 text-[18px]">where_to_vote</span>
        </div>
        <h3 className="text-sm font-bold text-gray-800">Thông tin người nhận</h3>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div>
          <label className={labelClass}>Họ và tên *</label>
          <input
            type="text"
            value={data.receiverName}
            onChange={(e) => onChange('receiverName', e.target.value)}
            placeholder="Trần Thị B"
            className={inputClass}
          />
        </div>
        <div>
          <label className={labelClass}>Số điện thoại *</label>
          <input
            type="tel"
            value={data.receiverPhone}
            onChange={(e) => onChange('receiverPhone', e.target.value)}
            placeholder="0987 654 321"
            className={inputClass}
          />
        </div>
        <div className="sm:col-span-2">
          <label className={labelClass}>Địa chỉ giao hàng *</label>
          {showGeocode ? (
            <div className="flex gap-2">
              <input
                type="text"
                value={data.destAddress}
                onChange={(e) => onChange('destAddress', e.target.value)}
                placeholder="456 Đường DEF, Quận UVW, Đà Nẵng"
                className="flex-1 px-3.5 py-2.5 rounded-xl border border-gray-200 text-sm focus:outline-none focus:ring-2 focus:ring-[#00288e]/30 focus:border-[#00288e] transition-all"
              />
              <button
                type="button"
                onClick={onGeocode}
                disabled={geocoding}
                className="px-4 py-2.5 rounded-xl bg-[#00288e] text-white text-sm font-bold hover:bg-[#001f6e] transition-all disabled:opacity-50 whitespace-nowrap"
              >
                {geocoding ? (
                  <span className="material-symbols-outlined animate-spin text-[18px]">sync</span>
                ) : (
                  'Tìm vị trí'
                )}
              </button>
            </div>
          ) : (
            <input
              type="text"
              value={data.destAddress}
              onChange={(e) => onChange('destAddress', e.target.value)}
              placeholder="456 Đường XYZ, Quận 7, TP.HCM"
              className={inputClass}
            />
          )}
          {showGeocode && destResolved && (
            <p className="text-xs text-emerald-600 font-medium mt-1.5 flex items-center gap-1">
              <span className="material-symbols-outlined text-[14px]">check_circle</span>
              {destResolvedName || 'Đã xác định vị trí giao hàng.'}
            </p>
          )}
        </div>
      </div>
    </div>
  );

  /* ── Cargo Section ───────────────────────────────────────────────── */

  const renderCargoSection = () => (
    <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
      <div className="flex items-center gap-2.5 mb-4">
        <div className="w-8 h-8 bg-purple-50 rounded-lg flex items-center justify-center">
          <span className="material-symbols-outlined text-purple-600 text-[18px]">inventory_2</span>
        </div>
        <h3 className="text-sm font-bold text-gray-800">Thông tin hàng hóa</h3>
      </div>
      <div className="space-y-3">
        {/* Cargo Type */}
        {cargoTypeAsDropdown ? (
          <div>
            <label className={labelClass}>Loại hàng *</label>
            <select
              value={data.cargoType}
              onChange={(e) => onChange('cargoType', e.target.value)}
              className={inputClass + ' cursor-pointer'}
            >
              <option value="">Chọn loại hàng</option>
              {cargoTypeOptions.map((cat) => (
                <option key={cat.value} value={cat.value}>
                  {cat.label}
                </option>
              ))}
            </select>
          </div>
        ) : (
          <div>
            <label className={labelClass}>Loại hàng *</label>
            <input
              type="text"
              value={data.cargoType}
              onChange={(e) => onChange('cargoType', e.target.value)}
              placeholder="Ví dụ: Nội thất, Quần áo, Thực phẩm..."
              className={inputClass}
            />
          </div>
        )}

        {/* Description (optional, driver) */}
        {showDescription && (
          <div>
            <label className={labelClass}>Mô tả hàng hóa</label>
            <textarea
              value={description ?? ''}
              onChange={(e) => onDescriptionChange?.(e.target.value)}
              placeholder="Mô tả ngắn gọn về hàng hóa..."
              rows={3}
              className={textAreaClass}
            />
          </div>
        )}

        {/* Weight / Volume / Quantity */}
        <div className={`grid grid-cols-1 sm:grid-cols-${showQuantity ? '3' : '2'} gap-3`}>
          <div>
            <label className={labelClass}>Cân nặng (kg) *</label>
            <input
              type="number"
              min="0.1"
              step="0.1"
              value={data.weightKg || ''}
              onChange={(e) => onChange('weightKg', parseFloat(e.target.value) || 0)}
              placeholder="0"
              className={inputClass}
            />
            {remainingWeight !== undefined && Number(data.weightKg) > remainingWeight && (
              <p className="text-[11px] text-amber-600 font-medium mt-1">
                Vượt quá dung tích còn lại ({remainingWeight} kg)
              </p>
            )}
          </div>
          <div>
            <label className={labelClass}>Thể tích (m³) *</label>
            <input
              type="number"
              min="0.01"
              step="0.01"
              value={data.volumeCbm || ''}
              onChange={(e) => onChange('volumeCbm', parseFloat(e.target.value) || 0)}
              placeholder="0"
              className={inputClass}
            />
            {remainingVolume !== undefined && Number(data.volumeCbm) > remainingVolume && (
              <p className="text-[11px] text-amber-600 font-medium mt-1">
                Vượt quá thể tích còn lại ({remainingVolume} m³)
              </p>
            )}
          </div>
          {showQuantity && (
            <div>
              <label className={labelClass}>Số kiện</label>
              <input
                type="number"
                min="1"
                step="1"
                value={quantity ?? 1}
                onChange={(e) => onQuantityChange?.(parseInt(e.target.value) || 1)}
                className={inputClass}
              />
            </div>
          )}
        </div>

        {/* COD checkbox */}
        {showCod && (
          <div className="flex items-center gap-3">
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={codRequired ?? false}
                onChange={(e) => onCodChange?.(e.target.checked)}
                className="w-5 h-5 rounded accent-[#00288e] cursor-pointer"
              />
              <span className="text-sm text-gray-700">Thu hộ (COD)</span>
            </label>
          </div>
        )}

        {/* Special Handling Note (customer) */}
        {showSpecialHandlingNote && (
          <div>
            <label className={labelClass}>Ghi chú đặc biệt</label>
            <input
              type="text"
              value={specialHandlingNote ?? ''}
              onChange={(e) => onSpecialHandlingNoteChange?.(e.target.value)}
              placeholder="Hàng dễ vỡ, cần cẩn thận..."
              className={inputClass}
            />
          </div>
        )}

        {/* Note textarea (driver) */}
        {showNote && (
          <div>
            <label className={labelClass}>Ghi chú</label>
            <textarea
              value={note ?? ''}
              onChange={(e) => onNoteChange?.(e.target.value)}
              placeholder="Ghi chú đặc biệt (VD: hàng dễ vỡ, cần cẩn thận...)"
              rows={2}
              className={textAreaClass}
            />
          </div>
        )}
      </div>
    </div>
  );

  /* ── Render All Sections ─────────────────────────────────────────── */

  return (
    <div className="space-y-5">
      {renderSenderSection()}
      {renderReceiverSection()}
      {renderCargoSection()}
    </div>
  );
}
