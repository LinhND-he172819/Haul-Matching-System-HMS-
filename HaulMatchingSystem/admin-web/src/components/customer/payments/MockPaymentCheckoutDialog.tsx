import { useState } from 'react';
import type { PaymentResponseDto } from '../../../api/customer/customerQuotationApi';
import { openMockCheckout, simulatePayment } from '../../../api/customer/customerQuotationApi';
import PaymentStatusBadge from './PaymentStatusBadge';

/* ─── Props ─────────────────────────────────────────────────────── */

interface MockPaymentCheckoutDialogProps {
  /** The payment response from createDepositPayment or createFinalPayment */
  payment: PaymentResponseDto;
  /** Called when payment result is received (Paid/Failed/Cancelled) */
  onComplete: (result: 'Paid' | 'Failed' | 'Cancelled') => void;
  /** Called when dialog is closed without completing */
  onClose: () => void;
  /** Show toast messages */
  onToast: (message: string, type: 'success' | 'error') => void;
}

const PAYMENT_METHODS = [
  { value: 'MockBanking', label: 'Ngân hàng (Mô phỏng)', icon: 'account_balance', description: 'Thanh toán qua cổng ngân hàng giả lập' },
  { value: 'MockWallet', label: 'Ví điện tử (Mô phỏng)', icon: 'account_balance_wallet', description: 'Thanh toán qua ví điện tử giả lập' },
  { value: 'MockTransfer', label: 'Chuyển khoản (Mô phỏng)', icon: 'swap_horiz', description: 'Chuyển khoản ngân hàng giả lập' },
];

const TYPE_LABELS: Record<string, string> = {
  Deposit: 'Đặt cọc',
  FinalPayment: 'Thanh toán cuối',
};

/* ─── Component ─────────────────────────────────────────────────── */

export default function MockPaymentCheckoutDialog({
  payment,
  onComplete,
  onClose,
  onToast,
}: MockPaymentCheckoutDialogProps) {
  const [selectedMethod, setSelectedMethod] = useState('MockBanking');
  const [step, setStep] = useState<'select' | 'processing' | 'result'>('select');
  const [resultStatus, setResultStatus] = useState<'Paid' | 'Failed' | 'Cancelled' | null>(null);
  const [processing, setProcessing] = useState(false);

  const formatCurrency = (n: number) =>
    n.toLocaleString('vi-VN', { style: 'currency', currency: 'VND' });

  const formatExpiry = (s?: string) => {
    if (!s) return 'N/A';
    const exp = new Date(s);
    const now = new Date();
    const diffMs = exp.getTime() - now.getTime();
    if (diffMs <= 0) return 'Đã hết hạn';
    const mins = Math.floor(diffMs / 60000);
    const secs = Math.floor((diffMs % 60000) / 1000);
    return `${mins} phút ${secs} giây`;
  };

  const handleOpenCheckout = async () => {
    setProcessing(true);
    try {
      await openMockCheckout(payment.id, selectedMethod);
      setStep('processing');
    } catch (err: any) {
      onToast(err.message || 'Lỗi mở phiên thanh toán', 'error');
    } finally {
      setProcessing(false);
    }
  };

  const handleSimulate = async (result: 'Paid' | 'Failed' | 'Cancelled') => {
    setProcessing(true);
    try {
      await simulatePayment(payment.id, result);
      setResultStatus(result);
      setStep('result');
      onComplete(result);
    } catch (err: any) {
      onToast(err.message || 'Lỗi mô phỏng thanh toán', 'error');
    } finally {
      setProcessing(false);
    }
  };

  const resultConfig = {
    Paid: {
      icon: 'check_circle',
      iconColor: 'text-emerald-500',
      bgColor: 'bg-emerald-50',
      borderColor: 'border-emerald-200',
      title: 'Thanh toán thành công!',
      description: 'Giao dịch đã được xử lý thành công.',
    },
    Failed: {
      icon: 'error',
      iconColor: 'text-rose-500',
      bgColor: 'bg-rose-50',
      borderColor: 'border-rose-200',
      title: 'Thanh toán thất bại',
      description: 'Giao dịch không thể xử lý. Vui lòng thử lại.',
    },
    Cancelled: {
      icon: 'cancel',
      iconColor: 'text-gray-500',
      bgColor: 'bg-gray-50',
      borderColor: 'border-gray-200',
      title: 'Đã hủy thanh toán',
      description: 'Giao dịch đã được hủy.',
    },
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="bg-surface-container-lowest rounded-2xl border border-outline-variant w-full max-w-md card-shadow"
        onClick={(e) => e.stopPropagation()}
      >
        {/* ─── Header ─── */}
        <div className="px-6 py-4 border-b border-outline-variant flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-full bg-amber-50 border border-amber-200 flex items-center justify-center">
              <span className="material-symbols-outlined text-[20px] text-amber-600">science</span>
            </div>
            <div>
              <h3 className="text-headline-sm font-bold text-on-surface">
                Thanh toán mô phỏng (Dev)
              </h3>
              <p className="text-label-sm text-on-surface-variant">
                Chế độ phát triển — không xử lý tiền thật
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-surface-container-low transition-colors"
          >
            <span className="material-symbols-outlined text-[20px]">close</span>
          </button>
        </div>

        <div className="p-6">
          {/* ─── Step: Select Method ─── */}
          {step === 'select' && (
            <div className="space-y-5">
              {/* Payment Info Card */}
              <div className="bg-surface-container-low rounded-xl p-4 space-y-2">
                <div className="flex justify-between text-body-md">
                  <span className="text-on-surface-variant">Loại thanh toán:</span>
                  <span className="font-semibold text-on-surface">
                    {TYPE_LABELS[payment.paymentType] || payment.paymentType}
                  </span>
                </div>
                <div className="flex justify-between text-body-md">
                  <span className="text-on-surface-variant">Mã thanh toán:</span>
                  <span className="font-mono text-on-surface">{payment.paymentCode}</span>
                </div>
                <div className="flex justify-between text-body-md">
                  <span className="text-on-surface-variant">Số tiền:</span>
                  <span className="font-bold text-primary text-lg">
                    {formatCurrency(payment.amount)}
                  </span>
                </div>
                <div className="flex justify-between text-body-md">
                  <span className="text-on-surface-variant">Trạng thái:</span>
                  <PaymentStatusBadge status={payment.status} />
                </div>
                {payment.expiresAt && (
                  <div className="flex justify-between text-body-md">
                    <span className="text-on-surface-variant">Thời hạn:</span>
                    <span className="font-semibold text-on-surface">
                      {formatExpiry(payment.expiresAt)}
                    </span>
                  </div>
                )}
              </div>

              {/* Method Selection */}
              <div>
                <p className="text-label-lg font-bold text-on-surface mb-3">
                  Chọn phương thức thanh toán mô phỏng:
                </p>
                <div className="space-y-2">
                  {PAYMENT_METHODS.map((method) => (
                    <button
                      key={method.value}
                      onClick={() => setSelectedMethod(method.value)}
                      className={`w-full flex items-center gap-3 p-3 rounded-xl border transition-colors ${
                        selectedMethod === method.value
                          ? 'border-primary bg-primary/5 ring-1 ring-primary'
                          : 'border-outline-variant hover:bg-surface-container-low'
                      }`}
                    >
                      <div
                        className={`w-10 h-10 rounded-full flex items-center justify-center ${
                          selectedMethod === method.value
                            ? 'bg-primary text-on-primary'
                            : 'bg-surface-container-low text-on-surface-variant'
                        }`}
                      >
                        <span className="material-symbols-outlined text-[18px]">{method.icon}</span>
                      </div>
                      <div className="text-left">
                        <p className="text-body-md font-semibold text-on-surface">{method.label}</p>
                        <p className="text-label-sm text-on-surface-variant">{method.description}</p>
                      </div>
                      {selectedMethod === method.value && (
                        <span className="material-symbols-outlined text-primary ml-auto text-[18px]">
                          radio_button_checked
                        </span>
                      )}
                      {selectedMethod !== method.value && (
                        <span className="material-symbols-outlined text-on-surface-variant/40 ml-auto text-[18px]">
                          radio_button_unchecked
                        </span>
                      )}
                    </button>
                  ))}
                </div>
              </div>

              {/* Action Buttons */}
              <div className="flex gap-3 pt-2">
                <button
                  onClick={onClose}
                  className="flex-1 px-4 py-3 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low transition-colors text-label-md font-bold"
                >
                  Đóng
                </button>
                <button
                  onClick={handleOpenCheckout}
                  disabled={processing}
                  className="flex-1 px-4 py-3 rounded-xl bg-primary text-on-primary hover:bg-primary-700 disabled:opacity-50 transition-colors text-label-md font-bold flex items-center justify-center gap-2"
                >
                  {processing ? (
                    <>
                      <span className="material-symbols-outlined animate-spin text-[16px]">sync</span>
                      Đang mở...
                    </>
                  ) : (
                    <>
                      <span className="material-symbols-outlined text-[16px]">credit_card</span>
                      Mở phiên thanh toán
                    </>
                  )}
                </button>
              </div>
            </div>
          )}

          {/* ─── Step: Processing / Simulation ─── */}
          {step === 'processing' && (
            <div className="space-y-5">
              {/* Payment summary */}
              <div className="bg-surface-container-low rounded-xl p-4 text-center space-y-2">
                <span className="material-symbols-outlined text-[40px] text-primary">credit_card</span>
                <p className="text-headline-sm font-bold text-on-surface">
                  {formatCurrency(payment.amount)}
                </p>
                <p className="text-label-sm text-on-surface-variant">
                  {TYPE_LABELS[payment.paymentType] || payment.paymentType} • {payment.paymentCode}
                </p>
                <p className="text-label-sm text-on-surface-variant">
                  Phương thức: {selectedMethod}
                </p>
              </div>

              {/* Dev Warning */}
              <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 text-center">
                <span className="material-symbols-outlined text-[24px] text-amber-600 mb-1">science</span>
                <p className="text-body-sm font-semibold text-amber-700">
                  Môi trường mô phỏng — Chọn kết quả thanh toán
                </p>
              </div>

              {/* Simulation Buttons */}
              <div className="space-y-2">
                <button
                  onClick={() => handleSimulate('Paid')}
                  disabled={processing}
                  className="w-full flex items-center justify-center gap-2 px-4 py-3.5 rounded-xl bg-emerald-500 text-white hover:bg-emerald-600 disabled:opacity-50 transition-colors text-label-md font-bold"
                >
                  <span className="material-symbols-outlined text-[18px]">check_circle</span>
                  Mô phỏng thành công
                </button>
                <button
                  onClick={() => handleSimulate('Failed')}
                  disabled={processing}
                  className="w-full flex items-center justify-center gap-2 px-4 py-3.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 hover:bg-rose-100 disabled:opacity-50 transition-colors text-label-md font-bold"
                >
                  <span className="material-symbols-outlined text-[18px]">error</span>
                  Mô phỏng thất bại
                </button>
                <button
                  onClick={() => handleSimulate('Cancelled')}
                  disabled={processing}
                  className="w-full flex items-center justify-center gap-2 px-4 py-3.5 rounded-xl bg-gray-50 border border-gray-200 text-gray-700 hover:bg-gray-100 disabled:opacity-50 transition-colors text-label-md font-bold"
                >
                  <span className="material-symbols-outlined text-[18px]">cancel</span>
                  Hủy thanh toán
                </button>
              </div>

              <button
                onClick={onClose}
                className="w-full px-4 py-3 rounded-xl border border-outline-variant text-on-surface hover:bg-surface-container-low transition-colors text-label-md font-bold"
              >
                Đóng
              </button>
            </div>
          )}

          {/* ─── Step: Result ─── */}
          {step === 'result' && resultStatus && (
            <div className="space-y-5 text-center">
              <div className={`mx-auto w-16 h-16 rounded-full ${resultConfig[resultStatus].bgColor} border ${resultConfig[resultStatus].borderColor} flex items-center justify-center`}>
                <span className={`material-symbols-outlined text-[32px] ${resultConfig[resultStatus].iconColor}`}>
                  {resultConfig[resultStatus].icon}
                </span>
              </div>
              <div>
                <p className="text-headline-sm font-bold text-on-surface">
                  {resultConfig[resultStatus].title}
                </p>
                <p className="text-body-md text-on-surface-variant mt-1">
                  {resultConfig[resultStatus].description}
                </p>
              </div>
              <button
                onClick={onClose}
                className="w-full px-4 py-3 rounded-xl bg-primary text-on-primary hover:bg-primary-700 transition-colors text-label-md font-bold"
              >
                Đóng
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
