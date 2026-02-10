import { Link } from 'react-router-dom';
import type { ChapterSummary } from '../types/api';

interface RecentChaptersListProps {
    chapters: ChapterSummary[];
    loading?: boolean;
}

export default function RecentChaptersList({ chapters, loading }: RecentChaptersListProps) {
    if (loading) {
        return <div className="animate-pulse h-40 bg-gray-50 rounded-lg"></div>;
    }

    if (chapters.length === 0) {
        return <div className="text-gray-500 text-sm">No recent uploads.</div>;
    }

    return (
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
            <div className="px-6 py-4 border-b border-gray-100">
                <h3 className="text-lg font-medium text-gray-900">Recent Uploads</h3>
            </div>
            <div className="divide-y divide-gray-100">
                {chapters.map((chapter) => (
                    <div key={chapter.manifestHash} className="p-4 hover:bg-gray-50 transition-colors flex items-center justify-between">
                        <div className="flex-1 min-w-0 mr-4">
                            <div className="flex items-center space-x-2">
                                <Link
                                    to={`/series/${chapter.seriesId}`}
                                    className="text-sm font-medium text-gray-900 hover:text-blue-600 truncate"
                                >
                                    {chapter.seriesId}
                                </Link>
                                <span className="text-gray-300 text-xs">|</span>
                                <span className="text-xs text-gray-500">{chapter.scanlatorId}</span>
                            </div>
                            <div className="mt-1 flex items-center text-sm text-gray-600">
                                <Link to={`/read/${chapter.manifestHash}`} className="hover:underline">
                                    Chapter {chapter.chapterNumber}
                                    {chapter.title && <span className="text-gray-400 ml-1">- {chapter.title}</span>}
                                </Link>
                            </div>
                        </div>
                        <div className="text-xs text-gray-400 whitespace-nowrap">
                            {new Date(chapter.uploadedAt).toLocaleDateString()}
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
}
