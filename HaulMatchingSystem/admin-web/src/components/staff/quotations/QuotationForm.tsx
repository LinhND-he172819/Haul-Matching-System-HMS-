import { useState, useEffect } from 'react';

interface QuotationFormProps {
  initialData?: {
    shippingFee?: number;
    depositAmount?: number;
    currency?: string;
    expiresAt?: string;
  };
  onSubmit: (data: {
    shippingFee: number;
    depositAmount: number;
    currency: string;
    expiresAt: string;
  }) => void;
  onCancel: () => void;
  loading?: boolean;
  readonly?: boolean;
  submitLabel?: string;
}

export default function QuotationForm({
  initialData,
  onSubmit,
  onCancel,
  loading = false,
  readonly = false,
  submitLabel = 'Tạo báo giá',
}: QuotationFormProps) {
  const [shippingFee, setShippingFee] = useState(initialData?.shippingFee?.toString() || '');
  const [depositAmount, setDepositAmount] = useState(initialData?.depositAmount?.toString() || '');
  const [currency, setCurrency] = useState(initialData?.currency || 'VND');
  const [expiresAt, setExpiresAt] = useState(
    initialData?.expiresAt
      ? new Date(initialData.expiresAt).toISOString().slice(0, 16)
      : ''
  );
  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (initialData) {
      setShippingFee(initialData.shippingFee?.toString() || '');
      setDepositAmount(initialData.depositAmount?.toString() || '');
      setCurrency(initialData.currency || 'VND');
      setExpiresAt(
        initialData.expiresAt
          ? new Date(initialData.expiresAt).toISOString().slice(0, 16)
          : ''
      );
    }
  }, [initialData]);

  const validate = (): boolean => {
    const errs: Record<string, string> = {};
    const fee = parseFloat(shippingFee);
    const deposit = parseFloat(depositAmount);

    if (!shippingFee || isNaN(fee) || fee <= 0) {
      errs.shippingFee = 'Phí vận chuyển phải lớn hơn 0';
    }
    if (!depositAmount || isNaN(deposit) || deposit <= 0) {
      errs.depositAmount = 'Tiền đặt cọc phải lớn hơn 0';
    }
    if (!isNaN(fee) && !isNaN(deposit) && deposit > fee) {
      errs.depositAmount = 'Tiền đặt cọc không được vượt quá phí vận chuyển';
    }
    if (!expiresAt) {
      errs.expiresAt = 'Thời hạn báo giá là bắt buộc';
    } else if (new Date(expiresAt) <= new Date()) {
      errs.expiresAt = 'Thời hạn phải lớn hơn thời gian hiện tại';
    }

    setErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate() || readonly) return;
    onSubmit({
      shippingFee: parseFloat(shippingFee),
      depositAmount: parseFloat(depositAmount),
      currency,
      expiresAt: new Date(expiresAt).toISOString(),
    });
  };

  const formatCurrency = (n: number) =>
    n.toLocaleString('vi-VN', { style: 'currency', currency: 'VND' });

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {/* Shipping Fee */}
      <div>
        <label className="block text-label-md font-semibold text-on-surface mb-1.5">
          Phí vận chuyển (VND) <span className="text-rose-500">*</span>
        </label>
        <input
          type="number"
          value={shippingFee}
          onChange={(e) => setShippingFee(e.target.value)}
          placeholder="Nhập phí vận chuyển"
          readOnly={readonly}
          min={0}
          step={1000}
          className="w-full border border-outline-variant rounded-xl px-4 py-2.5 text-body-md
                     placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/30
                     focus:border-primary transition-colors disabled:bg-gray-50"
        />
        {errors.shippingFee && (
          <p className="text-label-sm text-rose-600 mt-1">{errors.shippingFee}</p>
        )}
      </div>

      {/* Deposit Amount */}
      <div>
        <label className="block text-label-md font-semibold text-on-surface mb-1.5">
          Tiền đặt cọc (VND) <span className="text-rose-500">*</span>
        </label>
        <input
          type="number"
          value={depositAmount}
          onChange={(e) => setDepositAmount(e.target.value)}
          placeholder="Nhập tiền đặt cọc"
          readOnly={readonly}
          min={0}
          step={1000}
          className="w-full border border-outline-variant rounded-xl px-4 py-2.5 text-body-md
                     placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/30
                     focus:border-primary transition-colors disabled:bg-gray-50"
        />
        {errors.depositAmount && (
          <p className="text-label-sm text-rose-600 mt-1">{errors.depositAmount}</p>
        )}
      </div>

      {/* Currency */}
      <div>
        <label className="block text-label-md font-semibold text-on-surface mb-1.5">
          Đơn vị tiền tệ
        </label>
        <select
          value={currency}
          onChange={(e) => setCurrency(e.target.value)}
          disabled={readonly}
          className="w-full border border-outline-variant rounded-xl px-4 py-2.5 text-body-md
                     focus:outline-none focus:ring-2 focus:ring-primary/30 focus:border-primary
                     transition-colors disabled:bg-gray-50"
        >
          <option value="VND">VND - Việt Nam Đồng</option>
          <option value="USD">USD - US Dollar</option>
        </select>
      </div>

      {/* Expires At */}
      <div>
        <label className="block text-label-md font-semibold text-on-surface mb-1.5">
          Thời hạn báo giá <span className="text-rose-500">*</span>
        </label>
        <input
          type="datetime-local"
          value={expiresAt}
          onChange={(e) => setExpiresAt(e.target.value)}
          readOnly={readonly}
          className="w-full border border-outline-variant rounded-xl px-4 py-2.5 text-body-md
                     focus:outline-none focus:ring-2 focus:ring-primary/30 focus:border-primary
                     transition-colors disabled:bg-gray-50"
        />
        {errors.expiresAt && (
          <p className="text-label-sm text-rose-600 mt-1">{errors.expiresAt}</p>
        )}
      </div>

      {/* Preview */}
      {shippingFee && depositAmount && !isNaN(parseFloat(shippingFee)) && !isNaN(parseFloat(depositAmount)) && (
        <div className="bg-surface-container-low rounded-xl p-4 border border-outline-variant">
          <p className="text-label-sm text-on-surface-variant mb-2">Tóm tắt báo giá:</p>
          <div className="space-y-1 text-body-md">
            <div className="flex justify-between">
              <span className="text-on-surface-variant">Phí vận chuyển:</span>
              <span className="font-semibold">{formatCurrency(parseFloat(shippingFee))}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-on-surface-variant">Tiền đặt cọc:</span>
              <span className="font-semibold">{formatCurrency(parseFloat(depositAmount))}</span>
            </div>
            <div className="flex justify-between border-t border-outline-variant pt-1 mt-1">
              <span className="text-on-surface-variant">Còn lại sau đặt cọc:</span>
              <span className="font-semibold text-primary">
                {formatCurrency(Math.max(0, parseFloat(shippingFee) - parseFloat(depositAmount)))}
              </span>
            </div>
          </div>
        </div>
      )}

      {/* Actions */}
      {!readonly && (
        <div className="flex gap-3 justify-end pt-2">
          <button type="button" onClick={onCancel} className="btn-ghost">
            Hủy
          </button>
          <button
            type="submit"
            disabled={loading}
            className="btn-primary"
          >
            {loading ? (
              <span className="flex items-center gap-2">
                <span className="material-symbols-outlined animate-spin text-[16px]">sync</span>
                Đang xử lý...
              </span>
            ) : (
              submitLabel
            )}
          </button>
        </div>
      )}
    </form>
  );
}
