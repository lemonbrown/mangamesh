
import { useEffect, useState } from 'react';

import type { NodeStatus, SeriesSummaryResponse } from '../types/api';
import StorageBar from '../components/StorageBar';
import SubscriptionUpdatesList from '../components/SubscriptionUpdatesList';
import * as api from '../api/node';
import * as subscriptionsApi from '../api/subscriptions';

export default function Dashboard() {
    const [status, setStatus] = useState<NodeStatus | null>(null);
    const [storageTotal, setStorageTotal] = useState<number>(0);
    const [usedStorage, setUsedStorage] = useState<number>(0);
    const [manifestCount, setManifestCount] = useState<number>(0);
    const [subscriptionUpdates, setSubscriptionUpdates] = useState<SeriesSummaryResponse[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        async function loadData() {
            try {
                const [nodeStatus, storageStats, updates] = await Promise.all([
                    api.getNodeStatus(),
                    api.getStorageStats(),
                    subscriptionsApi.getSubscriptionUpdates()
                ]);
                setStatus(nodeStatus);
                setStorageTotal(storageStats.totalMb);
                setUsedStorage(storageStats.usedMb);
                setManifestCount(storageStats.manifestCount);
                setSubscriptionUpdates(updates);
            } catch (err) {
                console.error(err);
                setError('Failed to load dashboard data');
            } finally {
                setLoading(false);
            }
        }
        loadData();
    }, []);

    if (loading) return <div className="text-gray-500">Loading...</div>;
    if (error) return <div className="text-red-500">{error}</div>;
    if (!status) return null;

    return (
        <div className="space-y-6">
            <div>
                <h1 className="text-2xl font-bold text-gray-900 mb-6">Dashboard</h1>

                <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
                    <div className="bg-white p-6 rounded-lg shadow-sm border border-gray-200">
                        <div className="text-sm font-medium text-gray-500">Node ID</div>
                        <div className="mt-2 text-xl font-mono text-gray-900">{status.nodeId}</div>
                    </div>

                    <div className="bg-white p-6 rounded-lg shadow-sm border border-gray-200">
                        <div className="text-sm font-medium text-gray-500"> Peers</div>
                        <div className="mt-2 text-3xl font-semibold text-gray-900">{status.peerCount}</div>
                    </div>

                    <div className="bg-white p-6 rounded-lg shadow-sm border border-gray-200">
                        <div className="text-sm font-medium text-gray-500">Seeded Manifests</div>
                        <div className="mt-2 text-3xl font-semibold text-gray-900">{manifestCount}</div>
                    </div>
                </div>

                <div className="bg-white p-6 rounded-lg shadow-sm border border-gray-200">
                    <StorageBar usedMb={usedStorage} totalMb={storageTotal} />
                </div>
            </div>

            <SubscriptionUpdatesList updates={subscriptionUpdates} loading={loading} />
        </div>
    );
}
