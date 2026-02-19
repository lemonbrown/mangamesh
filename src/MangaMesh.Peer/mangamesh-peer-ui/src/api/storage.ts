import type { StorageStats, StoredManifest, PagedResult } from '../types/api';

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
