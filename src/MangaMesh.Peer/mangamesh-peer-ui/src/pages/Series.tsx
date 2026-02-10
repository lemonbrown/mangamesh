import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { searchSeries } from '../api/series';
import type { SeriesSearchResult } from '../types/api';

export default function Series() {
    const [results, setResults] = useState<SeriesSearchResult[]>([]);
    const [popular, setPopular] = useState<SeriesSearchResult[]>([]);
    const [recent, setRecent] = useState<SeriesSearchResult[]>([]);
    const [loading, setLoading] = useState(false);
    const [initialLoading, setInitialLoading] = useState(true);
    const [query, setQuery] = useState('');

    useEffect(() => {
        loadInitialData();
    }, []);

    async function loadInitialData() {
        setInitialLoading(true);
        try {
            const [popData, recData] = await Promise.all([
                searchSeries('', 5, 0, 'popular'),
                searchSeries('', 5, 0, 'recent')
            ]);
            setPopular(popData);
            setRecent(recData);
        } catch (e) {
            console.error("Failed to load initial data", e);
        } finally {
            setInitialLoading(false);
        }
    }

    async function handleSearch(q: string) {
        setQuery(q);
        if (!q) {
            setResults([]);
            return;
        }

        setLoading(true);
        try {
            const data = await searchSeries(q);
            setResults(data);
        } catch (e) {
            console.error(e);
        } finally {
            setLoading(false);
        }
    }

    const SeriesCard = ({ series }: { series: SeriesSearchResult }) => (
        <div key={series.seriesId} className="flex items-center justify-between bg-white p-4 rounded-lg shadow-sm border border-gray-200">
            <div className="flex-1">
                <div className="font-medium text-lg">
                    <Link
                        to={`/series/${series.seriesId}`}
                        className="text-blue-600 hover:underline"
                    >
                        {series.title}
                    </Link>
                </div>
                <div className="text-xs text-gray-500 font-mono mt-0.5">ID: {series.seriesId}</div>
                <div className="text-sm text-gray-500 mt-1">
                    {series.latestChapterNumber && (
                        <span className="text-gray-700 font-medium">
                            Latest Chapter: Ch. {series.latestChapterNumber}
                            {series.latestChapterTitle && ` - ${series.latestChapterTitle}`}
                        </span>
                    )}
                </div>
            </div>
            <div className="text-right text-sm text-gray-500">
                <div><span className="font-semibold text-green-600">{series.seedCount ?? 0}</span> seeds</div>
                <div>{series.chapterCount ?? 0} chapters</div>
                <div className="mt-1 text-xs text-gray-400" title={series.lastUploadedAt}>
                    Updated: {series.lastUploadedAt ? new Date(series.lastUploadedAt).toLocaleString(undefined, {
                        dateStyle: 'medium',
                        timeStyle: 'short'
                    }) : 'Never'}
                </div>
            </div>
        </div>
    );

    return (
        <div className="space-y-8">
            <div>
                <h1 className="text-2xl font-bold text-gray-900 mb-4">Search Series</h1>

                <div className="bg-white p-6 rounded-lg shadow-sm border border-gray-200">
                    <div className="relative flex-1">
                        <input
                            type="text"
                            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500"
                            placeholder="Search by series name or ID..."
                            value={query}
                            onChange={(e) => handleSearch(e.target.value)}
                        />
                        {loading && (
                            <div className="absolute right-3 top-2.5 text-gray-400">
                                <svg className="animate-spin h-5 w-5" viewBox="0 0 24 24">
                                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                                </svg>
                            </div>
                        )}
                    </div>
                </div>
            </div>

            {!query && (
                <>
                    <div>
                        <h2 className="text-lg font-medium text-gray-900 mb-4">Popular Series</h2>
                        {initialLoading ? (
                            <div className="text-gray-500">Loading popular series...</div>
                        ) : popular.length === 0 ? (
                            <div className="text-gray-500 italic">No popular series found.</div>
                        ) : (
                            <div className="grid gap-4">
                                {popular.map(s => <SeriesCard key={s.seriesId} series={s} />)}
                            </div>
                        )}
                    </div>

                    <div>
                        <h2 className="text-lg font-medium text-gray-900 mb-4">Recently Updated</h2>
                        {initialLoading ? (
                            <div className="text-gray-500">Loading recent series...</div>
                        ) : recent.length === 0 ? (
                            <div className="text-gray-500 italic">No recently updated series found.</div>
                        ) : (
                            <div className="grid gap-4">
                                {recent.map(s => <SeriesCard key={s.seriesId} series={s} />)}
                            </div>
                        )}
                    </div>
                </>
            )}

            {query && (
                <div>
                    <h2 className="text-lg font-medium text-gray-900 mb-4">Search Results</h2>
                    {loading && <div className="text-gray-500">Loading...</div>}

                    {!loading && results.length === 0 && (
                        <div className="text-gray-500 italic">No series found.</div>
                    )}

                    <div className="grid gap-4">
                        {results.map((series) => <SeriesCard key={series.seriesId} series={series} />)}
                    </div>
                </div>
            )}
        </div>
    );
}
