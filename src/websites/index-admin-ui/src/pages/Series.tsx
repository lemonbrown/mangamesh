import { useState, useEffect } from 'react';
import { Library, Search, BarChart3 } from 'lucide-react';
import { api } from '../services/api';
import type { Series } from '../services/api';

export default function Series() {
    const [searchTerm, setSearchTerm] = useState('');
    const [seriesList, setSeriesList] = useState<Series[]>([]);

    useEffect(() => {
        const fetchSeries = async () => {
            try {
                const data = await api.getSeries(searchTerm);
                setSeriesList(data);
            } catch (error) {
                console.error("Failed to fetch series:", error);
            }
        };

        const debounce = setTimeout(fetchSeries, 300);
        return () => clearTimeout(debounce);
    }, [searchTerm]);

    const filteredSeries = seriesList; // Filtering is done server-side

    return (
        <div className="space-y-6">
            <div className="flex justify-between items-center">
                <h1 className="text-2xl font-bold text-gray-900">Registered Series</h1>
                <div className="relative">
                    <input
                        type="text"
                        placeholder="Search series..."
                        value={searchTerm}
                        onChange={(e) => setSearchTerm(e.target.value)}
                        className="pl-10 pr-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none w-64"
                    />
                    <Search className="w-5 h-5 text-gray-400 absolute left-3 top-2.5" />
                </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                {filteredSeries.map((series) => (
                    <div key={series.id} className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 hover:shadow-md transition-shadow">
                        <div className="flex justify-between items-start mb-4">
                            <div className="p-3 bg-blue-50 rounded-lg">
                                <Library className="w-6 h-6 text-blue-600" />
                            </div>
                            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                                {series.source}
                            </span>
                        </div>
                        <h3 className="text-lg font-bold text-gray-900 mb-1">{series.title}</h3>
                        <p className="text-sm text-gray-500 font-mono mb-4">ID: {series.id}</p>

                        <div className="grid grid-cols-2 gap-4 border-t border-gray-100 pt-4">
                            <div>
                                <p className="text-xs text-gray-500 mb-1">Chapters</p>
                                <p className="text-lg font-semibold text-gray-900">{series.chapterCount}</p>
                            </div>
                            <div>
                                <p className="text-xs text-gray-500 mb-1">Manifests</p>
                                <p className="text-lg font-semibold text-gray-900">{series.manifestCount}</p>
                            </div>
                        </div>

                        <div className="mt-4 pt-4 border-t border-gray-100 flex items-center text-xs text-gray-400">
                            <BarChart3 className="w-4 h-4 mr-1" />
                            Updated {new Date(series.lastUpdated).toLocaleDateString()}
                        </div>
                    </div>
                ))}
            </div>

            {filteredSeries.length === 0 && (
                <div className="p-12 bg-white rounded-xl shadow-sm border border-gray-200 text-center text-gray-500">
                    No series found matching your search.
                </div>
            )}
        </div>
    );
}
