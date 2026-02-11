import axios from 'axios';

const API_BASE_URL = 'https://localhost:7043'; // Default to localhost:5176 (Index.Api)

export const httpClient = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        'Content-Type': 'application/json',
    },
});

export interface DashboardStats {
    activeNodes: number;
    totalPeers: number;
    gateways: number;
    bootstraps: number;
}

export interface Node {
    id: string;
    type: 'Peer' | 'Gateway' | 'Bootstrap' | 'Mixed';
    ip: string;
    port: number;
    lastSeen: string;
    status: 'Online' | 'Offline';
    version: string;
}

export interface PublicKey {
    id: string; // PublicKeyBase64
    name: string; // Comment
    keyPreview: string; // Derived from PublicKeyBase64
    addedAt: string;
    status: 'Active' | 'Revoked';
}

export interface Manifest {
    hash: string;
    seriesId: string;
    chapterNumber: number;
    volume?: string;
    scanGroup?: string;
    uploadedAt: string;
    verified: boolean;
    sizeBytes: number;
}

export interface Series {
    id: string;
    title: string;
    chapterCount: number;
    lastUpdated: string;
    source: string;
    manifestCount: number;
}

export interface LogEntry {
    id: number;
    timestamp: string;
    level: 'info' | 'warn' | 'error' | 'debug';
    category: string;
    message: string;
}

export const api = {
    // Dashboard
    getDashboardStats: async (): Promise<DashboardStats> => {
        const response = await httpClient.get('/admin/dashboard/stats');
        return response.data;
    },
    getNodes: async (filter?: string): Promise<Node[]> => {
        const response = await httpClient.get('/admin/dashboard/nodes', { params: { filter } });
        return response.data;
    },

    // Public Keys
    getPublicKeys: async (): Promise<PublicKey[]> => {
        const response = await httpClient.get('/admin/keys');
        return response.data;
    },
    registerPublicKey: async (request: { publicKeyBase64: string; comment: string }): Promise<void> => {
        await httpClient.post('/admin/keys', request);
    },
    revokePublicKey: async (publicKeyBase64: string): Promise<void> => {
        await httpClient.delete(`/admin/keys/${encodeURIComponent(publicKeyBase64)}`);
    },
    reactivatePublicKey: async (publicKeyBase64: string): Promise<void> => {
        await httpClient.post(`/admin/keys/${encodeURIComponent(publicKeyBase64)}/reactivate`);
    },

    // Manifests
    getManifests: async (search?: string): Promise<Manifest[]> => {
        const response = await httpClient.get('/admin/manifests', { params: { q: search } });
        return response.data;
    },

    // Series
    getSeries: async (search?: string): Promise<Series[]> => {
        const response = await httpClient.get('/admin/series', { params: { q: search } });
        return response.data;
    },

    // Logs
    getLogs: async (level?: string): Promise<LogEntry[]> => {
        const response = await httpClient.get('/admin/logs', { params: { level } });
        return response.data;
    }
};
