import { useEffect, useState } from 'react';
import { getStorageStats, getStoredManifests, deleteManifest } from '../api/storage';
import type { StorageStats, StoredManifest } from '../types/api';
import StorageBar from '../components/StorageBar';

const PAGE_SIZE = 20;

export default function Storage() {
    const [stats, setStats] = useState<StorageStats | null>(null);
    const [manifests, setManifests] = useState<StoredManifest[]>([]);
    const [total, setTotal] = useState(0);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [deleting, setDeleting] = useState<string | null>(null);
    const [search, setSearch] = useState('');
    const [debouncedSearch, setDebouncedSearch] = useState('');
    const [page, setPage] = useState(0);

    // Debounce search input
    useEffect(() => {
        const t = setTimeout(() => setDebouncedSearch(search), 300);
        return () => clearTimeout(t);
    }, [search]);

    // Reset to first page when search changes
    useEffect(() => {
        setPage(0);
    }, [debouncedSearch]);

    async function load(q: string, p: number) {
        setLoading(true);
        try {
            const [statsData, paged] = await Promise.all([
                getStorageStats(),
                getStoredManifests(q || undefined, p * PAGE_SIZE, PAGE_SIZE)
            ]);
            setStats(statsData);
            setManifests(paged.items);
            setTotal(paged.total);
        } catch (e) {
            setError('Failed to load storage data');
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => {
        load(debouncedSearch, page);
    }, [debouncedSearch, page]);

    async function handleDelete(hash: string) {
        if (!confirm('Are you sure you want to delete this manifest? This action cannot be undone.')) return;

        setDeleting(hash);
        try {
            await deleteManifest(hash);
            await load(debouncedSearch, page);
        } catch (e) {
            alert('Failed to delete manifest');
        } finally {
            setDeleting(null);
        }
    }

    const pageCount = Math.ceil(total / PAGE_SIZE);
    const rangeStart = total === 0 ? 0 : page * PAGE_SIZE + 1;
    const rangeEnd = Math.min(page * PAGE_SIZE + PAGE_SIZE, total);

    if (error) return <div className="text-red-500">{error}</div>;
    if (!stats && loading) return <div className="text-gray-500">Loading...</div>;
    if (!stats) return null;

    return (
        <div className="space-y-6">
            <h1 className="text-2xl font-bold text-gray-900">Storage</h1>

            <div className="bg-white p-8 rounded-lg shadow-sm border border-gray-200">
                <div className="mb-8">
                    <StorageBar usedMb={stats.usedMb} totalMb={stats.totalMb} />
                </div>

                <div className="grid grid-cols-1 md:grid-cols-3 gap-6 pt-6 border-t border-gray-100">
                    <div>
                        <div className="text-sm font-medium text-gray-500">Total Capacity</div>
                        <div className="mt-1 text-2xl font-mono text-gray-900">{(stats.totalMb / 1024).toFixed(2)} GB</div>
                    </div>
                    <div>
                        <div className="text-sm font-medium text-gray-500">Used Space</div>
                        <div className="mt-1 text-2xl font-mono text-gray-900">{(stats.usedMb / 1024).toFixed(2)} GB</div>
                    </div>
                    <div>
                        <div className="text-sm font-medium text-gray-500">Manifests</div>
                        <div className="mt-1 text-2xl font-mono text-gray-900">{stats.manifestCount}</div>
                    </div>
                </div>
            </div>

            <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
                <div className="px-6 py-4 border-b border-gray-200 bg-gray-50 flex items-center gap-4">
                    <h2 className="text-lg font-medium text-gray-900 shrink-0">Stored Manifests</h2>
                    <input
                        type="text"
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                        placeholder="Search by title, series, language…"
                        className="ml-auto w-full max-w-xs px-3 py-1.5 text-sm border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                    />
                </div>

                {loading ? (
                    <div className="p-6 text-center text-gray-400 text-sm">Loading…</div>
                ) : (
                    <div className="divide-y divide-gray-200">
                        {manifests.length === 0 ? (
                            <div className="p-6 text-center text-gray-500">
                                {debouncedSearch ? `No manifests match "${debouncedSearch}".` : 'No manifests stored.'}
                            </div>
                        ) : (
                            manifests.map(m => (
                                <div key={m.hash} className="p-4 hover:bg-gray-50 flex justify-between items-center group">
                                    <div className="flex-1 min-w-0 pr-4">
                                        <div className="flex items-center gap-2 mb-1">
                                            <h3 className="text-sm font-medium text-gray-900 truncate" title={m.title}>
                                                {m.title || 'Untitled'}
                                            </h3>
                                            {m.volume ? (
                                                <span className="px-1.5 py-0.5 rounded text-[10px] font-medium bg-gray-100 text-gray-600">Vol {m.volume}</span>
                                            ) : null}
                                            <span className="px-1.5 py-0.5 rounded text-[10px] font-medium bg-blue-50 text-blue-700">Ch {m.chapterNumber}</span>
                                            <span className="px-1.5 py-0.5 rounded text-[10px] font-medium bg-purple-50 text-purple-700">{m.language.toUpperCase()}</span>
                                        </div>
                                        <div className="flex items-center text-xs text-gray-500 space-x-2">
                                            <span className="font-mono text-gray-400" title={`Hash: ${m.hash}`}>#{m.hash.substring(0, 8)}</span>
                                            <span>•</span>
                                            <span title="Series ID">{m.seriesId}</span>
                                            <span>•</span>
                                            <span title="Scan Group">{m.scanGroup || 'Unknown Group'}</span>
                                            <span>•</span>
                                            <span title="Size">{(m.sizeBytes / 1024 / 1024).toFixed(2)} MB</span>
                                            <span>•</span>
                                            <span title="File Count">{m.fileCount} Pages</span>
                                            <span>•</span>
                                            <span title="Created At">{new Date(m.createdUtc).toLocaleDateString()}</span>
                                        </div>
                                    </div>
                                    <button
                                        onClick={() => handleDelete(m.hash)}
                                        disabled={deleting === m.hash}
                                        className="p-2 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-full transition-colors opacity-0 group-hover:opacity-100 focus:opacity-100"
                                        title="Delete Manifest"
                                    >
                                        <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                        </svg>
                                    </button>
                                </div>
                            ))
                        )}
                    </div>
                )}

                {total > 0 && (
                    <div className="px-6 py-3 border-t border-gray-200 bg-gray-50 flex items-center justify-between text-sm text-gray-600">
                        <span>Showing {rangeStart}–{rangeEnd} of {total}</span>
                        {pageCount > 1 && (
                            <div className="flex gap-2">
                                <button
                                    onClick={() => setPage(p => p - 1)}
                                    disabled={page === 0}
                                    className="px-3 py-1 rounded border border-gray-300 bg-white hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed"
                                >
                                    ← Prev
                                </button>
                                <span className="px-2 py-1 text-gray-500">{page + 1} / {pageCount}</span>
                                <button
                                    onClick={() => setPage(p => p + 1)}
                                    disabled={page >= pageCount - 1}
                                    className="px-3 py-1 rounded border border-gray-300 bg-white hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed"
                                >
                                    Next →
                                </button>
                            </div>
                        )}
                    </div>
                )}
            </div>
        </div>
    );
}
