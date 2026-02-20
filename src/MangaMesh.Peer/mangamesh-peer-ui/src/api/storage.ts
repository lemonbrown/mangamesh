import type { StorageStats, StoredManifest, StoredBlob, PagedResult } from '../types/api';

export async function getStorageStats(): Promise<StorageStats> {
    const response = await fetch('/api/node/storage');
    if (!response.ok) throw new Error('Failed to fetch storage stats');
    return await response.json();
}

export async function getStoredManifests(q?: string, offset = 0, limit = 20): Promise<PagedResult<StoredManifest>> {
    const params = new URLSearchParams();
    if (q) params.set('q', q);
    params.set('offset', String(offset));
    params.set('limit', String(limit));
    const response = await fetch(`/api/node/storage/manifests?${params}`);
    if (!response.ok) throw new Error('Failed to fetch manifests');
    return await response.json();
}

export async function deleteManifest(hash: string): Promise<void> {
    const response = await fetch(`/api/node/storage/manifests/${hash}`, {
        method: 'DELETE'
    });
    if (!response.ok) throw new Error('Failed to delete manifest');
}

export async function bulkDeleteManifests(hashes: string[]): Promise<void> {
    const response = await fetch('/api/node/storage/manifests', {
        method: 'DELETE',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(hashes)
    });
    if (!response.ok) throw new Error('Failed to delete manifests');
}

export async function getStoredBlobs(offset = 0, limit = 50): Promise<PagedResult<StoredBlob>> {
    const params = new URLSearchParams();
    params.set('offset', String(offset));
    params.set('limit', String(limit));
    const response = await fetch(`/api/node/storage/blobs?${params}`);
    if (!response.ok) throw new Error('Failed to fetch blobs');
    return await response.json();
}

export async function bulkDeleteBlobs(hashes: string[]): Promise<void> {
    const response = await fetch('/api/node/storage/blobs', {
        method: 'DELETE',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(hashes)
    });
    if (!response.ok) throw new Error('Failed to delete blobs');
}
