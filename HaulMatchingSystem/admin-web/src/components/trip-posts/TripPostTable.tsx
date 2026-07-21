import type { TripPostListItem } from '../../api/tripPostApi';
import StatusBadge from './StatusBadge';

interface Props {
    posts: TripPostListItem[];
    loading: boolean;
    onViewDetail: (id: string) => void;
    onEdit: (post: TripPostListItem) => void;
    onClose: (post: TripPostListItem) => void;
    onCancel: (post: TripPostListItem) => void;
}

export default function TripPostTable({ posts, loading, onViewDetail, onEdit, onClose, onCancel }: Props) {
    if (loading) {
        return (
            <div className="rounded-xl border border-outline-variant/40 bg-surface-container-lowest overflow-hidden">
                <div className="p-8 text-center space-y-3">
                    {[1, 2, 3, 4, 5].map(i => (
                        <div key={i} className="h-14 bg-surface-variant/30 rounded-lg animate-pulse" />
                    ))}
                </div>
            </div>
        );
    }

    if (posts.length === 0) {
        return (
            <div className="rounded-xl border border-outline-variant/40 bg-surface-container-lowest p-12 text-center">
                <span className="material-symbols-outlined text-on-surface-variant/30 text-[56px]">article</span>
                <p className="text-title-lg text-on-surface-variant mt-2">Không có bài đăng nào</p>
                <p className="text-body-sm text-on-surface-variant/70 mt-1">Thử thay đổi bộ lọc hoặc tạo bài đăng mới.</p>
            </div>
        );
    }

    return (
        <div className="rounded-xl border border-outline-variant/40 bg-surface-container-lowest overflow-hidden shadow-sm">
            <div className="overflow-x-auto">
                <table className="w-full text-left">
                    <thead>
                        <tr className="border-b border-outline-variant/30 bg-surface-container-low">
                            <th className="px-4 py-3 text-label-sm font-bold text-on-surface">Tiêu đề</th>
                            <th className="px-4 py-3 text-label-sm font-bold text-on-surface">Tuyến</th>
                            <th className="px-4 py-3 text-label-sm font-bold text-on-surface">Tài xế</th>
                            <th className="px-4 py-3 text-label-sm font-bold text-on-surface">Còn lại</th>
                            <th className="px-4 py-3 text-label-sm font-bold text-on-surface">Trạng thái</th>
                            <th className="px-4 py-3 text-label-sm font-bold text-on-surface">Hạn nhận</th>
                            <th className="px-4 py-3 text-label-sm font-bold text-on-surface text-right">Hành động</th>
                        </tr>
                    </thead>
                    <tbody className="divide-y divide-outline-variant/20">
                        {posts.map(post => (
                            <tr
                                key={post.id}
                                className="hover:bg-primary/[0.03] transition-colors cursor-pointer"
                                onClick={() => onViewDetail(post.id)}
                            >
                                <td className="px-4 py-3">
                                    <p className="text-body-sm font-medium text-on-surface truncate max-w-[260px]">{post.title}</p>
                                </td>
                                <td className="px-4 py-3">
                                    <p className="text-body-sm text-on-surface-variant">{post.originHubName} → {post.destinationHubName}</p>
                                </td>
                                <td className="px-4 py-3">
                                    <p className="text-body-sm text-on-surface-variant">{post.driverName}</p>
                                </td>
                                <td className="px-4 py-3">
                                    <p className="text-body-sm font-semibold text-secondary">
                                        {post.remainingWeightKg.toLocaleString()} kg
                                    </p>
                                    <p className="text-label-sm text-on-surface-variant">
                                        {post.remainingVolumeCbm.toLocaleString()} CBM
                                    </p>
                                </td>
                                <td className="px-4 py-3" onClick={e => e.stopPropagation()}>
                                    <StatusBadge status={post.status} />
                                </td>
                                <td className="px-4 py-3">
                                    <p className="text-body-sm text-on-surface-variant">
                                        {new Date(post.acceptUntil).toLocaleString('vi-VN', {
                                            hour: '2-digit',
                                            minute: '2-digit',
                                            day: '2-digit',
                                            month: '2-digit',
                                            year: 'numeric',
                                        })}
                                    </p>
                                </td>
                                <td className="px-4 py-3 text-right" onClick={e => e.stopPropagation()}>
                                    <div className="flex items-center justify-end gap-1">
                                        {post.status === 'Open' && (
                                            <>
                                                <button
                                                    onClick={() => onEdit(post)}
                                                    className="p-1.5 rounded-lg hover:bg-primary/10 text-on-surface-variant hover:text-primary transition-colors"
                                                    title="Chỉnh sửa"
                                                >
                                                    <span className="material-symbols-outlined text-[18px]">edit</span>
                                                </button>
                                                <button
                                                    onClick={() => onClose(post)}
                                                    className="p-1.5 rounded-lg hover:bg-amber-50 text-on-surface-variant hover:text-amber-600 transition-colors"
                                                    title="Đóng bài"
                                                >
                                                    <span className="material-symbols-outlined text-[18px]">lock</span>
                                                </button>
                                                <button
                                                    onClick={() => onCancel(post)}
                                                    className="p-1.5 rounded-lg hover:bg-red-50 text-on-surface-variant hover:text-red-500 transition-colors"
                                                    title="Hủy bài"
                                                >
                                                    <span className="material-symbols-outlined text-[18px]">cancel</span>
                                                </button>
                                            </>
                                        )}
                                    </div>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
