import axios from 'axios';

const API_BASE_URL = 'http://localhost:7078'; // Default to localhost:7078 (Index.AdminApi)

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

export interface AnnouncingNode {
    nodeId: string;
    announcedAt: string;
    nodeType?: string;  // present if node is still live in the registry
    lastSeen?: string;  // present if node is still live in the registry
}

export interface ManifestDetail {
    hash: string;
    title: string;
    seriesId: string;
    chapterId: string;
    chapterNumber: number;
    volume?: string;
    language: string;
    scanGroup?: string;
    quality?: string;
    externalMetadataSource: string;
    externalMangaId: string;
    announcedAt: string;
    lastSeenAt: string;
    announcingNodes: AnnouncingNode[];
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
    getManifest: async (hash: string): Promise<ManifestDetail> => {
        const response = await httpClient.get(`/admin/manifests/${encodeURIComponent(hash)}`);
        return response.data;
    },
    deleteManifest: async (hash: string): Promise<void> => {
        await httpClient.delete(`/admin/manifests/${encodeURIComponent(hash)}`);
    },

    // Series
    getSeries: async (search?: string): Promise<Series[]> => {
        const response = await httpClient.get('/admin/series', { params: { q: search } });
        return response.data;
    },
    deleteSeries: async (id: string): Promise<void> => {
        await httpClient.delete(`/admin/series/${encodeURIComponent(id)}`);
    },

    // Logs
    getLogs: async (level?: string): Promise<LogEntry[]> => {
        const response = await httpClient.get('/admin/logs', { params: { level } });
        return response.data;
    }
};
