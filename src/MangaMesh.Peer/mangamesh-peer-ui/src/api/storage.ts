import type { StorageStats, StoredManifest } from '../types/api';

export async function getStorageStats(): Promise<StorageStats> {
    const response = await fetch('/api/node/storage');
    if (!response.ok) throw new Error('Failed to fetch storage stats');
    return await response.json();
}

export async function getStoredManifests(): Promise<StoredManifest[]> {
    const response = await fetch('/api/node/storage/manifests');
    if (!response.ok) throw new Error('Failed to fetch manifests');
    return await response.json();
}

export async function deleteManifest(hash: string): Promise<void> {
    const response = await fetch(`/api/node/storage/manifests/${hash}`, {
        method: 'DELETE'
    });
    if (!response.ok) throw new Error('Failed to delete manifest');
}
