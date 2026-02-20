import React, { useState, useEffect, useCallback } from 'react';
import { FileText, Search, Filter, CheckCircle, XCircle, Trash2, X, Server } from 'lucide-react';
import { api } from '../services/api';
import type { Manifest, ManifestDetail } from '../services/api';

function DetailRow({ label, value }: { label: string; value?: string | number | null }) {
    if (value == null || value === '') return null;
    return (
        <div className="py-2 grid grid-cols-5 gap-2">
            <dt className="col-span-2 text-xs font-medium text-gray-500 uppercase tracking-wide">{label}</dt>
            <dd className="col-span-3 text-sm text-gray-900 font-mono break-all">{String(value)}</dd>
        </div>
    );
}

function ManifestDetailPanel({ hash, onClose }: { hash: string; onClose: () => void }) {
    const [detail, setDetail] = useState<ManifestDetail | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        setLoading(true);
        setError(null);
        api.getManifest(hash)
            .then(setDetail)
            .catch(() => setError('Failed to load manifest details.'))
            .finally(() => setLoading(false));
    }, [hash]);

    return (
        <>
            {/* Backdrop */}
            <div
                className="fixed inset-0 bg-black/30 z-30"
                onClick={onClose}
            />
            {/* Panel */}
            <div className="fixed inset-y-0 right-0 w-full max-w-lg bg-white shadow-2xl z-40 flex flex-col">
                <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200">
                    <div className="flex items-center gap-2">
                        <FileText className="w-5 h-5 text-gray-500" />
                        <h2 className="text-base font-semibold text-gray-900">Manifest Detail</h2>
                    </div>
                    <button
                        onClick={onClose}
                        className="text-gray-400 hover:text-gray-600 p-1.5 rounded-lg hover:bg-gray-100 transition-colors"
                    >
                        <X className="w-5 h-5" />
                    </button>
                </div>

                <div className="flex-1 overflow-y-auto px-6 py-4">
                    {loading && (
                        <div className="flex items-center justify-center h-32 text-gray-400">Loading…</div>
                    )}
                    {error && (
                        <div className="text-red-600 text-sm">{error}</div>
                    )}
                    {detail && (
                        <div className="space-y-6">
                            {/* Manifest hash */}
                            <div>
                                <p className="text-xs font-medium text-gray-500 uppercase tracking-wide mb-1">Hash</p>
                                <p className="text-xs font-mono text-gray-800 break-all bg-gray-50 rounded-lg px-3 py-2 border border-gray-200">
                                    {detail.hash}
                                </p>
                            </div>

                            {/* Core metadata */}
                            <div>
                                <h3 className="text-sm font-semibold text-gray-700 mb-1 border-b pb-1">Metadata</h3>
                                <dl className="divide-y divide-gray-100">
                                    <DetailRow label="Title" value={detail.title} />
                                    <DetailRow label="Series ID" value={detail.seriesId} />
                                    <DetailRow label="Chapter ID" value={detail.chapterId} />
                                    <DetailRow label="Chapter" value={detail.chapterNumber} />
                                    <DetailRow label="Volume" value={detail.volume} />
                                    <DetailRow label="Language" value={detail.language} />
                                    <DetailRow label="Scan Group" value={detail.scanGroup} />
                                    <DetailRow label="Quality" value={detail.quality} />
                                </dl>
                            </div>

                            {/* External source */}
                            <div>
                                <h3 className="text-sm font-semibold text-gray-700 mb-1 border-b pb-1">External Source</h3>
                                <dl className="divide-y divide-gray-100">
                                    <DetailRow label="Source" value={detail.externalMetadataSource} />
                                    <DetailRow label="Manga ID" value={detail.externalMangaId} />
                                </dl>
                            </div>

                            {/* Timestamps */}
                            <div>
                                <h3 className="text-sm font-semibold text-gray-700 mb-1 border-b pb-1">Timestamps</h3>
                                <dl className="divide-y divide-gray-100">
                                    <DetailRow label="Announced" value={new Date(detail.announcedAt).toLocaleString()} />
                                    <DetailRow label="Last Seen" value={new Date(detail.lastSeenAt).toLocaleString()} />
                                </dl>
                            </div>

                            {/* Announcing nodes */}
                            <div>
                                <h3 className="text-sm font-semibold text-gray-700 mb-2 border-b pb-1">
                                    Announcing Nodes
                                    <span className="ml-2 text-xs font-normal text-gray-400">
                                        ({detail.announcingNodes.length} {detail.announcingNodes.length === 1 ? 'node' : 'nodes'})
                                    </span>
                                </h3>
                                {detail.announcingNodes.length === 0 ? (
                                    <p className="text-sm text-gray-400 italic">No nodes have announced this manifest.</p>
                                ) : (
                                    <ul className="space-y-2">
                                        {detail.announcingNodes.map((node) => (
                                            <li key={node.nodeId} className="flex items-start gap-3 p-3 bg-gray-50 rounded-lg border border-gray-200">
                                                <Server className="w-4 h-4 text-gray-400 mt-0.5 shrink-0" />
                                                <div className="min-w-0 flex-1">
                                                    <p className="text-xs font-mono text-gray-900 break-all">{node.nodeId}</p>
                                                    <div className="flex flex-wrap items-center gap-2 mt-1">
                                                        {node.nodeType && (
                                                            <span className="inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium bg-blue-100 text-blue-700">
                                                                {node.nodeType}
                                                            </span>
                                                        )}
                                                        <span className="text-xs text-gray-500">
                                                            Announced {new Date(node.announcedAt).toLocaleString()}
                                                        </span>
                                                        {node.lastSeen && (
                                                            <span className="text-xs text-green-600">
                                                                · Live, last seen {new Date(node.lastSeen).toLocaleString()}
                                                            </span>
                                                        )}
                                                        {!node.lastSeen && (
                                                            <span className="text-xs text-gray-400 italic">· Offline</span>
                                                        )}
                                                    </div>
                                                </div>
                                            </li>
                                        ))}
                                    </ul>
                                )}
                            </div>
                        </div>
                    )}
                </div>
            </div>
        </>
    );
}

export default function Manifests() {
    const [searchTerm, setSearchTerm] = useState('');
    const [manifests, setManifests] = useState<Manifest[]>([]);
    const [selectedHash, setSelectedHash] = useState<string | null>(null);

    const fetchManifests = useCallback(async () => {
        try {
            const data = await api.getManifests(searchTerm);
            setManifests(data);
        } catch (error) {
            console.error("Failed to fetch manifests:", error);
        }
    }, [searchTerm]);

    useEffect(() => {
        const debounce = setTimeout(fetchManifests, 300);
        return () => clearTimeout(debounce);
    }, [fetchManifests]);

    const handleDelete = async (e: React.MouseEvent, manifest: Manifest) => {
        e.stopPropagation();
        if (!window.confirm(`Delete manifest ${manifest.hash}?`)) return;
        try {
            await api.deleteManifest(manifest.hash);
            if (selectedHash === manifest.hash) setSelectedHash(null);
            await fetchManifests();
        } catch (error) {
            console.error("Failed to delete manifest:", error);
        }
    };

    const filteredManifests = manifests;

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

            <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-x-auto">
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
                            <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider sticky right-0 bg-gray-50">Actions</th>
                        </tr>
                    </thead>
                    <tbody className="bg-white divide-y divide-gray-200">
                        {filteredManifests.map((manifest) => (
                            <tr
                                key={manifest.hash}
                                className={`hover:bg-gray-50 group cursor-pointer ${selectedHash === manifest.hash ? 'bg-blue-50 hover:bg-blue-50' : ''}`}
                                onClick={() => setSelectedHash(manifest.hash)}
                            >
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
                                <td className={`px-6 py-4 whitespace-nowrap text-right sticky right-0 ${selectedHash === manifest.hash ? 'bg-blue-50' : 'bg-white group-hover:bg-gray-50'}`}>
                                    <button
                                        onClick={(e) => handleDelete(e, manifest)}
                                        className="text-gray-400 hover:text-red-600 hover:bg-red-50 p-1.5 rounded-lg transition-colors"
                                        title="Delete manifest"
                                    >
                                        <Trash2 className="w-4 h-4" />
                                    </button>
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

            {selectedHash && (
                <ManifestDetailPanel
                    hash={selectedHash}
                    onClose={() => setSelectedHash(null)}
                />
            )}
        </div>
    );
}
