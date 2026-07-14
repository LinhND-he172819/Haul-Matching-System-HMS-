import { useState } from 'react';
import { updateTripPost, type TripPostListItem } from '../../api/tripPostApi';

interface Props {
    post: TripPostListItem | null;
    onClose: () => void;
    onSuccess: (message: string) => void;
    onError: (message: string) => void;
}

export default function EditTripPostModal({ post, onClose, onSuccess, onError }: Props) {
    const [description, setDescription] = useState(post?.description ?? '');
    const [acceptUntil, setAcceptUntil] = useState(() => {
        if (post?.acceptUntil) {
            const d = new Date(post.acceptUntil);
            return d.toISOString().slice(0, 16);
        }
        return '';
    });
    const [submitting, setSubmitting] = useState(false);

    if (!post) return null;

    const canSubmit = description.trim() && acceptUntil && !submitting;

    const handleSave = async () => {
        if (!acceptUntil) return;
        if (new Date(acceptUntil) <= new Date()) {
            onError('Hạn nhận đề xuất phải lớn hơn thời điểm hiện tại.');
            return;
        }
        setSubmitting(true);
        try {
            const result = await updateTripPost(post.id, {
                description: description.trim(),
                acceptUntil: new Date(acceptUntil).toISOString(),
            });
            onSuccess(result.message);
        } catch (err) {
            onError(err instanceof Error ? err.message : 'Cập nhật thất bại.');
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <>
            <div className="fixed inset-0 z-40 bg-black/30" onClick={onClose} />
            <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
                <div className="bg-surface-container-lowest rounded-2xl shadow-xl w-full max-w-lg">
                    {/* Header */}
                    <div className="flex items-center justify-between px-6 py-4 border-b border-outline-variant/30">
                        <h2 className="text-title-lg font-bold text-on-surface">Chỉnh sửa bài đăng</h2>
                        <button onClick={onClose} className="p-2 rounded-full hover:bg-surface-variant/50 transition-colors">
                            <span className="material-symbols-outlined text-on-surface-variant">close</span>
                        </button>
                    </div>

                    {/* Body */}
                    <div className="px-6 py-5 space-y-4">
                        <div>
                            <label className="block text-label-lg font-bold text-on-surface mb-1.5">Mô tả</label>
                            <textarea
                                value={description}
                                onChange={e => setDescription(e.target.value)}
                                maxLength={2000}
                                rows={4}
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

                    {/* Footer */}
                    <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-outline-variant/30">
                        <button
                            onClick={onClose}
                            className="px-4 py-2 rounded-lg text-label-lg font-bold text-on-surface hover:bg-surface-variant/50 transition-colors"
                        >
                            Hủy
                        </button>
                        <button
                            onClick={handleSave}
                            disabled={!canSubmit}
                            className="flex items-center gap-2 px-5 py-2 rounded-lg bg-primary hover:bg-primary/90 disabled:bg-outline-variant disabled:text-on-surface-variant text-on-primary text-label-lg font-bold transition-all"
                        >
                            {submitting ? (
                                <>
                                    <span className="material-symbols-outlined animate-spin text-[18px]">sync</span>
                                    Đang lưu...
                                </>
                            ) : (
                                <>
                                    <span className="material-symbols-outlined text-[18px]">save</span>
                                    Lưu
                                </>
                            )}
                        </button>
                    </div>
                </div>
            </div>
        </>
    );
}
