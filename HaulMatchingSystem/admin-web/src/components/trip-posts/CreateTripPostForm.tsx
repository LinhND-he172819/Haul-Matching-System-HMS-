import { useState } from 'react';
import { createTripPost, type EligibleTrip, type CreateTripPostPayload } from '../../api/tripPostApi';
import TripSummaryCard from './TripSummaryCard';

interface Props {
    selectedTrip: EligibleTrip | null;
    onSuccess: (message: string) => void;
    onError: (message: string) => void;
}

export default function CreateTripPostForm({ selectedTrip, onSuccess, onError }: Props) {
    const [description, setDescription] = useState('');
    const [acceptUntil, setAcceptUntil] = useState('');
    const [submitting, setSubmitting] = useState(false);

    const canSubmit = selectedTrip && acceptUntil && !submitting;

    const handleSubmit = async () => {
        if (!selectedTrip || !acceptUntil) return;

        // Validate acceptUntil > now
        if (new Date(acceptUntil) <= new Date()) {
            onError('Hạn nhận đề xuất phải lớn hơn thời điểm hiện tại.');
            return;
        }

        setSubmitting(true);
        try {
            const payload: CreateTripPostPayload = {
                tripId: selectedTrip.tripId,
                description: description.trim() || undefined,
                acceptUntil: new Date(acceptUntil).toISOString(),
            };
            const result = await createTripPost(payload);
            onSuccess(result.message);
            setDescription('');
            setAcceptUntil('');
        } catch (err) {
            onError(err instanceof Error ? err.message : 'Đăng bài thất bại.');
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div className="space-y-4">
            {selectedTrip && <TripSummaryCard trip={selectedTrip} />}

            <div className="space-y-3">
                <div>
                    <label className="block text-label-lg font-bold text-on-surface mb-1.5">
                        Mô tả bổ sung
                    </label>
                    <textarea
                        value={description}
                        onChange={e => setDescription(e.target.value)}
                        placeholder="VD: Nhận hàng tiêu dùng, điện tử đóng gói an toàn..."
                        maxLength={2000}
                        rows={3}
                        className="w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-4 py-2.5 text-body-md text-on-surface placeholder:text-on-surface-variant/40 focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary resize-none"
                    />
                    <p className="text-label-sm text-on-surface-variant mt-1">{description.length}/2000</p>
                </div>

                <div>
                    <label className="block text-label-lg font-bold text-on-surface mb-1.5">
                        Hạn nhận đề xuất <span className="text-error">*</span>
                    </label>
                    <input
                        type="datetime-local"
                        value={acceptUntil}
                        onChange={e => setAcceptUntil(e.target.value)}
                        className="w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-4 py-2.5 text-body-md text-on-surface focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
                    />
                </div>
            </div>

            <button
                onClick={handleSubmit}
                disabled={!canSubmit}
                className="w-full flex items-center justify-center gap-2 bg-primary hover:bg-primary/90 disabled:bg-outline-variant disabled:text-on-surface-variant text-on-primary text-label-lg font-bold py-3 rounded-xl transition-all"
            >
                {submitting ? (
                    <>
                        <span className="material-symbols-outlined animate-spin text-[20px]">sync</span>
                        Đang đăng bài...
                    </>
                ) : (
                    <>
                        <span className="material-symbols-outlined text-[20px]">publish</span>
                        Đăng bài
                    </>
                )}
            </button>
        </div>
    );
}
