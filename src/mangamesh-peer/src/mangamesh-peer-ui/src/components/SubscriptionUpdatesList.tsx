import { Link } from 'react-router-dom';
import type { SeriesSummaryResponse } from '../types/api';

interface SubscriptionUpdatesListProps {
    updates: SeriesSummaryResponse[];
    loading?: boolean;
}

export default function SubscriptionUpdatesList({ updates, loading }: SubscriptionUpdatesListProps) {
    if (loading) {
        return <div className="animate-pulse h-40 bg-gray-50 rounded-lg"></div>;
    }

    if (updates.length === 0) {
        return (
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6 text-center">
                <p className="text-gray-500 mb-2">No subscription updates.</p>
                <Link to="/series" className="text-blue-600 hover:underline text-sm">
                    Browse series to subscribe
                </Link>
            </div>
        );
    }

    return (
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
            <div className="px-6 py-4 border-b border-gray-100 flex justify-between items-center">
                <h3 className="text-lg font-medium text-gray-900">Subscription Updates</h3>
            </div>
            <div className="divide-y divide-gray-100">
                {updates.map((series) => (
                    <div key={series.seriesId} className="p-4 hover:bg-gray-50 transition-colors flex items-center justify-between">
                        <div className="flex-1 min-w-0 mr-4">
                            <div className="flex items-center space-x-2">
                                <Link
                                    to={`/series/${series.seriesId}`}
                                    className="text-base font-medium text-gray-900 hover:text-blue-600 truncate"
                                >
                                    {series.title}
                                </Link>
                                <span className="text-xs px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 font-medium">
                                    {series.chapterCount} Chapters
                                </span>
                            </div>
                            <div className="mt-1 text-sm text-gray-500">
                                Latest update via {series.source === 0 ? 'MangaDex' : series.source === 1 ? 'AniList' : 'MAL'}
                            </div>
                        </div>
                        <div className="text-right">
                            <div className="text-xs text-gray-400">Updated</div>
                            <div className="text-sm font-medium text-gray-700">
                                {new Date(series.lastUploadedAt || 0).toLocaleDateString()}
                            </div>
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
}
