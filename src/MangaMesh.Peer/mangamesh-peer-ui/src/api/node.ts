import type { NodeStatus } from '../types/api';

const API_BASE_URL = ''; // Relative path for Docker/Production

export async function getNodeStatus(): Promise<NodeStatus> {
    const response = await fetch(`${API_BASE_URL}/api/node/status`);
    if (!response.ok) {
        throw new Error('Failed to fetch node status');
    }
    return await response.json();
}

export async function getStorageStats(): Promise<import('../types/api').StorageStats> {
    const response = await fetch(`${API_BASE_URL}/api/node/storage`);
    if (!response.ok) {
        throw new Error('Failed to fetch storage stats');
    }
    return await response.json();
}

export async function getKnownNodes(): Promise<import('../types/api').KnownNode[]> {
    const response = await fetch(`${API_BASE_URL}/api/node/peers`);
    if (!response.ok) {
        throw new Error('Failed to fetch known nodes');
    }
    return await response.json();
}
