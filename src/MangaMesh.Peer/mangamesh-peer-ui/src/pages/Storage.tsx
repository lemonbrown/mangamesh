import { useEffect, useState } from 'react';
import { getStorageStats, getStoredManifests, getStoredBlobs, deleteManifest, bulkDeleteManifests, bulkDeleteBlobs } from '../api/storage';
import type { StorageStats, StoredManifest, StoredBlob } from '../types/api';
import StorageBar from '../components/StorageBar';

const MANIFEST_PAGE_SIZE = 20;
const BLOB_PAGE_SIZE = 50;

type Tab = 'manifests' | 'blobs';

// ── Shared: selection toolbar ─────────────────────────────────────────────────

function SelectionToolbar({
    selectedCount,
    totalCount,
    onSelectAll,
    onClearAll,
    onDeleteSelected,
    deleting,
}: {
    selectedCount: number;
    totalCount: number;
    onSelectAll: () => void;
    onClearAll: () => void;
    onDeleteSelected: () => void;
    deleting: boolean;
}) {
    if (selectedCount === 0) return null;
    return (
        <div className="px-6 py-2.5 bg-blue-50 border-b border-blue-200 flex items-center justify-between">
            <div className="flex items-center gap-3 text-sm text-blue-800">
                <span className="font-medium">{selectedCount} selected</span>
                {selectedCount < totalCount && (
                    <button onClick={onSelectAll} className="underline hover:no-underline text-blue-700">
                        Select all {totalCount}
                    </button>
                )}
                <button onClick={onClearAll} className="underline hover:no-underline text-blue-700">
                    Clear
                </button>
            </div>
            <button
                onClick={onDeleteSelected}
                disabled={deleting}
                className="flex items-center gap-1.5 px-3 py-1.5 bg-red-600 hover:bg-red-700 disabled:opacity-50 text-white text-sm font-medium rounded-md transition-colors"
            >
                <svg xmlns="http://www.w3.org/2000/svg" className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                </svg>
                {deleting ? 'Deleting…' : `Delete ${selectedCount}`}
            </button>
        </div>
    );
}

// ── Manifests tab ────────────────────────────────────────────────────────────

function ManifestsTab({ stats, onStatsChanged }: { stats: StorageStats; onStatsChanged: () => void }) {
    const [manifests, setManifests] = useState<StoredManifest[]>([]);
    const [total, setTotal] = useState(0);
    const [loading, setLoading] = useState(true);
    const [bulkDeleting, setBulkDeleting] = useState(false);
    const [deletingSingle, setDeletingSingle] = useState<string | null>(null);
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [search, setSearch] = useState('');
    const [debouncedSearch, setDebouncedSearch] = useState('');
    const [page, setPage] = useState(0);

    useEffect(() => {
        const t = setTimeout(() => setDebouncedSearch(search), 300);
        return () => clearTimeout(t);
    }, [search]);

    useEffect(() => { setPage(0); setSelected(new Set()); }, [debouncedSearch]);

    async function load(q: string, p: number) {
        setLoading(true);
        try {
            const paged = await getStoredManifests(q || undefined, p * MANIFEST_PAGE_SIZE, MANIFEST_PAGE_SIZE);
            setManifests(paged.items);
            setTotal(paged.total);
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => { load(debouncedSearch, page); }, [debouncedSearch, page]);

    function toggleOne(hash: string) {
        setSelected(prev => {
            const next = new Set(prev);
            next.has(hash) ? next.delete(hash) : next.add(hash);
            return next;
        });
    }

    function togglePage() {
        const pageHashes = manifests.map(m => m.hash);
        const allSelected = pageHashes.every(h => selected.has(h));
        setSelected(prev => {
            const next = new Set(prev);
            if (allSelected) pageHashes.forEach(h => next.delete(h));
            else pageHashes.forEach(h => next.add(h));
            return next;
        });
    }

    async function handleDeleteSingle(hash: string) {
        if (!confirm('Delete this manifest? This cannot be undone.')) return;
        setDeletingSingle(hash);
        try {
            await deleteManifest(hash);
            setSelected(prev => { const n = new Set(prev); n.delete(hash); return n; });
            await load(debouncedSearch, page);
            onStatsChanged();
        } catch {
            alert('Failed to delete manifest');
        } finally {
            setDeletingSingle(null);
        }
    }

    async function handleSelectAllManifests() {
        const all = await getStoredManifests(debouncedSearch || undefined, 0, total);
        setSelected(new Set(all.items.map(m => m.hash)));
    }

    async function handleBulkDelete() {
        const hashes = Array.from(selected);
        if (!confirm(`Delete ${hashes.length} manifest${hashes.length !== 1 ? 's' : ''}? This cannot be undone.`)) return;
        setBulkDeleting(true);
        try {
            await bulkDeleteManifests(hashes);
            setSelected(new Set());
            await load(debouncedSearch, page);
            onStatsChanged();
        } catch {
            alert('Failed to delete manifests');
        } finally {
            setBulkDeleting(false);
        }
    }

    const pageHashes = manifests.map(m => m.hash);
    const allPageSelected = pageHashes.length > 0 && pageHashes.every(h => selected.has(h));
    const somePageSelected = pageHashes.some(h => selected.has(h));
    const pageCount = Math.ceil(total / MANIFEST_PAGE_SIZE);
    const rangeStart = total === 0 ? 0 : page * MANIFEST_PAGE_SIZE + 1;
    const rangeEnd = Math.min(page * MANIFEST_PAGE_SIZE + MANIFEST_PAGE_SIZE, total);

    return (
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
            {/* Header */}
            <div className="px-6 py-4 border-b border-gray-200 bg-gray-50 flex items-center gap-4">
                <h2 className="text-sm font-medium text-gray-700 shrink-0">
                    {stats.manifestCount} manifest{stats.manifestCount !== 1 ? 's' : ''} stored
                </h2>
                <input
                    type="text"
                    value={search}
                    onChange={e => setSearch(e.target.value)}
                    placeholder="Search by title, series, language…"
                    className="ml-auto w-full max-w-xs px-3 py-1.5 text-sm border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
            </div>

            {/* Selection toolbar */}
            <SelectionToolbar
                selectedCount={selected.size}
                totalCount={total}
                onSelectAll={handleSelectAllManifests}
                onClearAll={() => setSelected(new Set())}
                onDeleteSelected={handleBulkDelete}
                deleting={bulkDeleting}
            />

            {loading ? (
                <div className="p-6 text-center text-gray-400 text-sm">Loading…</div>
            ) : (
                <>
                    {/* Column header with select-all for page */}
                    {manifests.length > 0 && (
                        <div className="px-4 py-2 border-b border-gray-100 bg-gray-50 flex items-center gap-3">
                            <input
                                type="checkbox"
                                checked={allPageSelected}
                                ref={el => { if (el) el.indeterminate = somePageSelected && !allPageSelected; }}
                                onChange={togglePage}
                                className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500 cursor-pointer"
                            />
                            <span className="text-xs text-gray-500">Select page</span>
                        </div>
                    )}
                    <div className="divide-y divide-gray-200">
                        {manifests.length === 0 ? (
                            <div className="p-6 text-center text-gray-500">
                                {debouncedSearch ? `No manifests match "${debouncedSearch}".` : 'No manifests stored.'}
                            </div>
                        ) : (
                            manifests.map(m => {
                                const isSelected = selected.has(m.hash);
                                return (
                                    <div
                                        key={m.hash}
                                        onClick={() => toggleOne(m.hash)}
                                        className={`p-4 flex items-center gap-3 group cursor-pointer ${isSelected ? 'bg-blue-50' : 'hover:bg-gray-50'}`}
                                    >
                                        <input
                                            type="checkbox"
                                            checked={isSelected}
                                            onChange={() => toggleOne(m.hash)}
                                            onClick={e => e.stopPropagation()}
                                            className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500 cursor-pointer shrink-0"
                                        />
                                        <div className="flex-1 min-w-0">
                                            <div className="flex items-center gap-2 mb-1">
                                                <h3 className="text-sm font-medium text-gray-900 truncate" title={m.title}>
                                                    {m.title || 'Untitled'}
                                                </h3>
                                                {m.volume && (
                                                    <span className="px-1.5 py-0.5 rounded text-[10px] font-medium bg-gray-100 text-gray-600">Vol {m.volume}</span>
                                                )}
                                                <span className="px-1.5 py-0.5 rounded text-[10px] font-medium bg-blue-50 text-blue-700">Ch {m.chapterNumber}</span>
                                                <span className="px-1.5 py-0.5 rounded text-[10px] font-medium bg-purple-50 text-purple-700">{m.language.toUpperCase()}</span>
                                            </div>
                                            <div className="flex items-center text-xs text-gray-500 space-x-2">
                                                <span className="font-mono text-gray-400" title={`Hash: ${m.hash}`}>#{m.hash.substring(0, 8)}</span>
                                                <span>•</span>
                                                <span>{m.seriesId}</span>
                                                <span>•</span>
                                                <span>{m.scanGroup || 'Unknown Group'}</span>
                                                <span>•</span>
                                                <span>{(m.sizeBytes / 1024 / 1024).toFixed(2)} MB</span>
                                                <span>•</span>
                                                <span>{m.fileCount} pages</span>
                                                <span>•</span>
                                                <span>{new Date(m.createdUtc).toLocaleDateString()}</span>
                                            </div>
                                        </div>
                                        <button
                                            onClick={e => { e.stopPropagation(); handleDeleteSingle(m.hash); }}
                                            disabled={deletingSingle === m.hash}
                                            className="p-2 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-full transition-colors opacity-0 group-hover:opacity-100 focus:opacity-100 shrink-0"
                                            title="Delete"
                                        >
                                            <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                            </svg>
                                        </button>
                                    </div>
                                );
                            })
                        )}
                    </div>
                </>
            )}

            {total > 0 && (
                <div className="px-6 py-3 border-t border-gray-200 bg-gray-50 flex items-center justify-between text-sm text-gray-600">
                    <span>Showing {rangeStart}–{rangeEnd} of {total}</span>
                    {pageCount > 1 && (
                        <div className="flex gap-2">
                            <button onClick={() => setPage(p => p - 1)} disabled={page === 0}
                                className="px-3 py-1 rounded border border-gray-300 bg-white hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed">
                                ← Prev
                            </button>
                            <span className="px-2 py-1 text-gray-500">{page + 1} / {pageCount}</span>
                            <button onClick={() => setPage(p => p + 1)} disabled={page >= pageCount - 1}
                                className="px-3 py-1 rounded border border-gray-300 bg-white hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed">
                                Next →
                            </button>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}

// ── Blobs tab ────────────────────────────────────────────────────────────────

function formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
}

function BlobsTab({ stats, onStatsChanged }: { stats: StorageStats; onStatsChanged: () => void }) {
    const [blobs, setBlobs] = useState<StoredBlob[]>([]);
    const [total, setTotal] = useState(0);
    const [loading, setLoading] = useState(true);
    const [bulkDeleting, setBulkDeleting] = useState(false);
    const [selected, setSelected] = useState<Set<string>>(new Set());
    const [page, setPage] = useState(0);

    async function load(p: number) {
        setLoading(true);
        try {
            const paged = await getStoredBlobs(p * BLOB_PAGE_SIZE, BLOB_PAGE_SIZE);
            setBlobs(paged.items);
            setTotal(paged.total);
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => { load(page); }, [page]);

    function toggleOne(hash: string) {
        setSelected(prev => {
            const next = new Set(prev);
            next.has(hash) ? next.delete(hash) : next.add(hash);
            return next;
        });
    }

    function togglePage() {
        const pageHashes = blobs.map(b => b.hash);
        const allSelected = pageHashes.every(h => selected.has(h));
        setSelected(prev => {
            const next = new Set(prev);
            if (allSelected) pageHashes.forEach(h => next.delete(h));
            else pageHashes.forEach(h => next.add(h));
            return next;
        });
    }

    async function handleSelectAll() {
        const all = await getStoredBlobs(0, total);
        setSelected(new Set(all.items.map(b => b.hash)));
    }

    async function handleBulkDelete() {
        const hashes = Array.from(selected);
        if (!confirm(`Delete ${hashes.length} blob${hashes.length !== 1 ? 's' : ''}? This cannot be undone.`)) return;
        setBulkDeleting(true);
        try {
            await bulkDeleteBlobs(hashes);
            setSelected(new Set());
            await load(page);
            onStatsChanged();
        } catch {
            alert('Failed to delete blobs');
        } finally {
            setBulkDeleting(false);
        }
    }

    const pageHashes = blobs.map(b => b.hash);
    const allPageSelected = pageHashes.length > 0 && pageHashes.every(h => selected.has(h));
    const somePageSelected = pageHashes.some(h => selected.has(h));
    const pageCount = Math.ceil(total / BLOB_PAGE_SIZE);
    const rangeStart = total === 0 ? 0 : page * BLOB_PAGE_SIZE + 1;
    const rangeEnd = Math.min(page * BLOB_PAGE_SIZE + BLOB_PAGE_SIZE, total);

    return (
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
            {/* Header */}
            <div className="px-6 py-4 border-b border-gray-200 bg-gray-50">
                <h2 className="text-sm font-medium text-gray-700">
                    {stats.blobCount} blob{stats.blobCount !== 1 ? 's' : ''} on disk
                </h2>
                <p className="text-xs text-gray-400 mt-0.5">Raw content-addressed data files (image chunks)</p>
            </div>

            {/* Selection toolbar */}
            <SelectionToolbar
                selectedCount={selected.size}
                totalCount={total}
                onSelectAll={handleSelectAll}
                onClearAll={() => setSelected(new Set())}
                onDeleteSelected={handleBulkDelete}
                deleting={bulkDeleting}
            />

            {loading ? (
                <div className="p-6 text-center text-gray-400 text-sm">Loading…</div>
            ) : blobs.length === 0 ? (
                <div className="p-6 text-center text-gray-500">No blobs stored.</div>
            ) : (
                <>
                    {/* Column header with select-all for page */}
                    <div className="px-4 py-2 border-b border-gray-100 bg-gray-50 flex items-center gap-3">
                        <input
                            type="checkbox"
                            checked={allPageSelected}
                            ref={el => { if (el) el.indeterminate = somePageSelected && !allPageSelected; }}
                            onChange={togglePage}
                            className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500 cursor-pointer"
                        />
                        <span className="text-xs text-gray-500">Select page</span>
                    </div>
                    <div className="divide-y divide-gray-200">
                        {blobs.map(b => {
                            const isSelected = selected.has(b.hash);
                            return (
                                <div
                                    key={b.hash}
                                    onClick={() => toggleOne(b.hash)}
                                    className={`px-4 py-3 flex items-center gap-3 cursor-pointer ${isSelected ? 'bg-blue-50' : 'hover:bg-gray-50'}`}
                                >
                                    <input
                                        type="checkbox"
                                        checked={isSelected}
                                        onChange={() => toggleOne(b.hash)}
                                        onClick={e => e.stopPropagation()}
                                        className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500 cursor-pointer shrink-0"
                                    />
                                    <span className="text-xs font-mono text-gray-700 flex-1 break-all">{b.hash}</span>
                                    <span className="text-xs text-gray-500 shrink-0">{formatBytes(b.sizeBytes)}</span>
                                </div>
                            );
                        })}
                    </div>
                </>
            )}

            {total > 0 && (
                <div className="px-6 py-3 border-t border-gray-200 bg-gray-50 flex items-center justify-between text-sm text-gray-600">
                    <span>Showing {rangeStart}–{rangeEnd} of {total}</span>
                    {pageCount > 1 && (
                        <div className="flex gap-2">
                            <button onClick={() => setPage(p => p - 1)} disabled={page === 0}
                                className="px-3 py-1 rounded border border-gray-300 bg-white hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed">
                                ← Prev
                            </button>
                            <span className="px-2 py-1 text-gray-500">{page + 1} / {pageCount}</span>
                            <button onClick={() => setPage(p => p + 1)} disabled={page >= pageCount - 1}
                                className="px-3 py-1 rounded border border-gray-300 bg-white hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed">
                                Next →
                            </button>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}

// ── Page ─────────────────────────────────────────────────────────────────────

export default function Storage() {
    const [stats, setStats] = useState<StorageStats | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [tab, setTab] = useState<Tab>('manifests');

    async function fetchStats() {
        const s = await getStorageStats();
        setStats(s);
    }

    useEffect(() => {
        fetchStats()
            .catch(() => setError('Failed to load storage stats'))
            .finally(() => setLoading(false));
    }, []);

    if (error) return <div className="text-red-500">{error}</div>;
    if (loading || !stats) return <div className="text-gray-500">Loading...</div>;

    return (
        <div className="space-y-6">
            <h1 className="text-2xl font-bold text-gray-900">Storage</h1>

            {/* Summary card */}
            <div className="bg-white p-8 rounded-lg shadow-sm border border-gray-200">
                <div className="mb-8">
                    <StorageBar usedMb={stats.usedMb} totalMb={stats.totalMb} />
                </div>
                <div className="grid grid-cols-2 md:grid-cols-4 gap-6 pt-6 border-t border-gray-100">
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
                    <div>
                        <div className="text-sm font-medium text-gray-500">Blobs</div>
                        <div className="mt-1 text-2xl font-mono text-gray-900">{stats.blobCount}</div>
                    </div>
                </div>
            </div>

            {/* Tabs */}
            <div>
                <div className="flex border-b border-gray-200 mb-4">
                    {(['manifests', 'blobs'] as Tab[]).map(t => (
                        <button
                            key={t}
                            onClick={() => setTab(t)}
                            className={`px-5 py-2.5 text-sm font-medium capitalize border-b-2 -mb-px transition-colors ${
                                tab === t
                                    ? 'border-blue-600 text-blue-600'
                                    : 'border-transparent text-gray-500 hover:text-gray-700'
                            }`}
                        >
                            {t}
                        </button>
                    ))}
                </div>

                {tab === 'manifests' && <ManifestsTab stats={stats} onStatsChanged={fetchStats} />}
                {tab === 'blobs' && <BlobsTab stats={stats} onStatsChanged={fetchStats} />}
            </div>
        </div>
    );
}
