import { useState, useEffect, useRef } from 'react';

interface QuotationCountdownProps {
  expiresAt: string;
  onExpired?: () => void;
}

export default function QuotationCountdown({ expiresAt, onExpired }: QuotationCountdownProps) {
  const [timeLeft, setTimeLeft] = useState({ hours: 0, minutes: 0, seconds: 0, expired: false });
  const onExpiredRef = useRef(onExpired);
  onExpiredRef.current = onExpired;

  useEffect(() => {
    const target = new Date(expiresAt).getTime();
    let firedExpired = false;

    const tick = () => {
      const now = Date.now();
      const diff = target - now;
      if (diff <= 0) {
        if (!firedExpired) {
          firedExpired = true;
          setTimeLeft({ hours: 0, minutes: 0, seconds: 0, expired: true });
          onExpiredRef.current?.();
        }
        return;
      }
      const hours = Math.floor(diff / (1000 * 60 * 60));
      const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
      const seconds = Math.floor((diff % (1000 * 60)) / 1000);
      setTimeLeft({ hours, minutes, seconds, expired: false });
    };

    tick();
    const interval = setInterval(tick, 1000);
    return () => clearInterval(interval);
  }, [expiresAt]);

  if (timeLeft.expired) {
    return (
      <div className="flex items-center gap-2 text-rose-600 bg-rose-50 rounded-xl px-4 py-3">
        <span className="material-symbols-outlined text-[20px]">timer_off</span>
        <span className="text-body-md font-semibold">Báo giá đã hết hiệu lực</span>
      </div>
    );
  }

  const pad = (n: number) => n.toString().padStart(2, '0');
  const urgency =
    timeLeft.hours === 0 && timeLeft.minutes < 30
      ? 'text-rose-600 bg-rose-50 border-rose-200'
      : timeLeft.hours < 1
        ? 'text-amber-600 bg-amber-50 border-amber-200'
        : 'text-emerald-600 bg-emerald-50 border-emerald-200';

  return (
    <div className={`flex items-center gap-2 rounded-xl px-4 py-3 border ${urgency}`}>
      <span className="material-symbols-outlined text-[20px]">timer</span>
      <span className="text-body-md font-semibold">
        Còn {pad(timeLeft.hours)} giờ {pad(timeLeft.minutes)} phút {pad(timeLeft.seconds)} giây
      </span>
    </div>
  );
}
