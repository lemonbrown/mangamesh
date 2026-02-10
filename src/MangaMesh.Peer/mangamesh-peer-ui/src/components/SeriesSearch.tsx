import { useState, useEffect, useRef } from 'react';
import { searchSeries } from '../api/series';
import type { SeriesSearchResult } from '../types/api';

interface SeriesSearchProps {
    onSelect: (seriesId: string) => void;
}

export default function SeriesSearch({ onSelect }: SeriesSearchProps) {
    const [query, setQuery] = useState('');
    const [results, setResults] = useState<SeriesSearchResult[]>([]);
    const [loading, setLoading] = useState(false);
    const [isOpen, setIsOpen] = useState(false);
    const wrapperRef = useRef<HTMLDivElement>(null);

    // Debounce search
    useEffect(() => {
        if (!query) {
            setResults([]);
            setIsOpen(false);
            return;
        }

        const timer = setTimeout(async () => {
            setLoading(true);
            try {
                const data = await searchSeries(query);
                setResults(data);
                setIsOpen(true);
            } catch (e) {
                console.error(e);
            } finally {
                setLoading(false);
            }
        }, 300);

        return () => clearTimeout(timer);
    }, [query]);

    // Click outside to close
    useEffect(() => {
        function handleClickOutside(event: MouseEvent) {
            if (wrapperRef.current && !wrapperRef.current.contains(event.target as Node)) {
                setIsOpen(false);
            }
        }
        document.addEventListener("mousedown", handleClickOutside);
        return () => document.removeEventListener("mousedown", handleClickOutside);
    }, []);

    const handleSelect = (seriesId: string) => {
        onSelect(seriesId);
        setQuery('');
        setIsOpen(false);
    };

    return (
        <div ref={wrapperRef} className="relative flex-1">
            <label className="block text-sm font-medium text-gray-700 mb-1">Search Series</label>
            <div className="relative">
                <input
                    type="text"
                    className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500"
                    placeholder="Search by series name or ID..."
                    value={query}
                    onChange={(e) => setQuery(e.target.value)}
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

            {isOpen && results.length > 0 && (
                <div className="absolute z-10 w-full mt-1 bg-white shadow-lg rounded-md border border-gray-200 max-h-80 overflow-auto">
                    {results.map((series) => (
                        <div
                            key={series.seriesId}
                            className="px-4 py-3 hover:bg-gray-50 cursor-pointer border-b border-gray-100 last:border-0"
                            onClick={() => handleSelect(series.seriesId)}
                        >
                            <div className="flex justify-between items-start">
                                <div>
                                    <div className="font-medium text-gray-900">{series.title}</div>
                                    <div className="text-xs text-gray-500 font-mono mt-0.5">ID: {series.seriesId}</div>
                                </div>
                                <div className="text-right text-xs text-gray-500">
                                    <div><span className="font-semibold text-green-600">{series.seedCount}</span> seeds</div>
                                    <div>{series.chapterCount} chapters</div>
                                </div>
                            </div>
                            <div className="text-xs text-gray-400 mt-1">
                                Last updated: {new Date(series.lastUploadedAt || 0).toLocaleDateString()}
                            </div>
                        </div>
                    ))}
                </div>
            )}

            {isOpen && query && results.length === 0 && !loading && (
                <div className="absolute z-10 w-full mt-1 bg-white shadow-lg rounded-md border border-gray-200 p-4 text-center text-gray-500 text-sm">
                    No series found.
                </div>
            )}
        </div>
    );
}
