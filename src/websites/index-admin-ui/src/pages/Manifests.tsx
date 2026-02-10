import { useState, useEffect } from 'react';
import { FileText, Search, Filter, CheckCircle, XCircle } from 'lucide-react';
import { api } from '../services/api';
import type { Manifest } from '../services/api';

export default function Manifests() {
    const [searchTerm, setSearchTerm] = useState('');
    const [manifests, setManifests] = useState<Manifest[]>([]);

    useEffect(() => {
        const fetchManifests = async () => {
            try {
                const data = await api.getManifests(searchTerm);
                setManifests(data);
            } catch (error) {
                console.error("Failed to fetch manifests:", error);
            }
        };

        const debounce = setTimeout(fetchManifests, 300);
        return () => clearTimeout(debounce);
    }, [searchTerm]);

    const filteredManifests = manifests; // Filtering is done server-side via search term

    return (
        <div className="space-y-6">
            <div className="flex justify-between items-center">
                <h1 className="text-2xl font-bold text-gray-900">Manifest Entries</h1>
                <div className="flex space-x-2">
                    <div className="relative">
                        <input
                            type="text"
                            placeholder="Search manifests..."
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                            className="pl-10 pr-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none w-64"
                        />
                        <Search className="w-5 h-5 text-gray-400 absolute left-3 top-2.5" />
                    </div>
                    <button className="px-4 py-2 bg-white border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 flex items-center">
                        <Filter className="w-4 h-4 mr-2" />
                        Filter
                    </button>
                </div>
            </div>

            <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
                <table className="min-w-full divide-y divide-gray-200">
                    <thead className="bg-gray-50">
                        <tr>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Manifest Hash</th>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Series</th>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Chapter</th>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Group</th>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Size</th>
                            <th className="px-6 py-3 text-center text-xs font-medium text-gray-500 uppercase tracking-wider">Verified</th>
                            <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Uploaded</th>
                        </tr>
                    </thead>
                    <tbody className="bg-white divide-y divide-gray-200">
                        {filteredManifests.map((manifest) => (
                            <tr key={manifest.hash} className="hover:bg-gray-50">
                                <td className="px-6 py-4 whitespace-nowrap">
                                    <div className="flex items-center">
                                        <FileText className="w-4 h-4 text-gray-400 mr-2" />
                                        <span className="text-sm font-mono text-gray-900">{manifest.hash}</span>
                                    </div>
                                </td>
                                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600 font-medium">
                                    {manifest.seriesId}
                                </td>
                                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                                    Ch. {manifest.chapterNumber} {manifest.volume && `(Vol. ${manifest.volume})`}
                                </td>
                                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                                    {manifest.scanGroup || '-'}
                                </td>
                                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                                    {(manifest.sizeBytes / 1024 / 1024).toFixed(2)} MB
                                </td>
                                <td className="px-6 py-4 whitespace-nowrap text-center">
                                    {manifest.verified ? (
                                        <CheckCircle className="w-5 h-5 text-green-500 mx-auto" />
                                    ) : (
                                        <XCircle className="w-5 h-5 text-gray-300 mx-auto" />
                                    )}
                                </td>
                                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500 text-right">
                                    {new Date(manifest.uploadedAt).toLocaleString()}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {filteredManifests.length === 0 && (
                    <div className="p-8 text-center text-gray-500">
                        No manifests found matching your search.
                    </div>
                )}
            </div>
        </div>
    );
}
