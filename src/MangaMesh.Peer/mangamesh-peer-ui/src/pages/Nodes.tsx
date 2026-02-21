import { useState, useEffect, useRef } from 'react';
import { getKnownNodes } from '../api/node';
import type { KnownNode } from '../types/api';

function timeAgo(dateStr: string): string {
    const now = Date.now();
    const then = new Date(dateStr).getTime();
    const seconds = Math.floor((now - then) / 1000);

    if (seconds < 60) return `${seconds}s ago`;
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    return `${days}d ago`;
}

function truncateNodeId(nodeId: string): string {
    if (nodeId.length <= 16) return nodeId;
    return `${nodeId.slice(0, 8)}...${nodeId.slice(-8)}`;
}

export default function Nodes() {
    const [nodes, setNodes] = useState<KnownNode[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

    async function fetchNodes() {
        try {
            const data = await getKnownNodes();
            setNodes(data);
            setError(null);
        } catch {
            setError('Failed to fetch known nodes');
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => {
        fetchNodes();
        intervalRef.current = setInterval(fetchNodes, 10000);
        return () => { if (intervalRef.current) clearInterval(intervalRef.current); };
    }, []);

    return (
        <div className="space-y-4">
            <div className="flex items-center justify-between">
                <h1 className="text-2xl font-bold text-gray-900">Known Nodes</h1>
                <div className="flex items-center gap-2">
                    <span className="text-sm text-gray-500">{nodes.length} node{nodes.length !== 1 ? 's' : ''}</span>
                    <button
                        onClick={fetchNodes}
                        className="px-3 py-1.5 text-sm font-medium rounded-md border border-gray-300 bg-white text-gray-700 hover:bg-gray-50 transition-colors"
                    >
                        Refresh
                    </button>
                </div>
            </div>

            <div className="flex items-center gap-2 text-xs text-gray-500">
                <span className="inline-block w-2 h-2 rounded-full bg-green-400 animate-pulse" />
                Auto-refreshing every 10s
            </div>

            {error && (
                <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-sm text-red-700">{error}</div>
            )}

            <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
                {loading && nodes.length === 0 ? (
                    <div className="p-8 text-center text-gray-400 text-sm">Loading nodes...</div>
                ) : nodes.length === 0 ? (
                    <div className="p-8 text-center text-gray-500 text-sm">No known nodes yet. Waiting for DHT bootstrap...</div>
                ) : (
                    <div className="overflow-x-auto">
                        <table className="min-w-full divide-y divide-gray-100 text-sm">
                            <thead className="bg-gray-50 text-[10px] uppercase tracking-wider text-gray-500">
                                <tr>
                                    <th className="px-4 py-2 text-left">Node ID</th>
                                    <th className="px-4 py-2 text-left">Host</th>
                                    <th className="px-4 py-2 text-left">DHT Port</th>
                                    <th className="px-4 py-2 text-left">HTTP API Port</th>
                                    <th className="px-4 py-2 text-left">Last Seen</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-gray-100">
                                {nodes.map((node) => (
                                    <tr key={node.nodeId} className="hover:bg-gray-50">
                                        <td className="px-4 py-2.5 font-mono text-xs text-gray-800" title={node.nodeId}>
                                            {truncateNodeId(node.nodeId)}
                                        </td>
                                        <td className="px-4 py-2.5 text-gray-700">{node.host}</td>
                                        <td className="px-4 py-2.5 text-gray-700">{node.port}</td>
                                        <td className="px-4 py-2.5 text-gray-700">
                                            {node.httpApiPort > 0 ? node.httpApiPort : <span className="text-gray-400">-</span>}
                                        </td>
                                        <td className="px-4 py-2.5 text-gray-500" title={new Date(node.lastSeenUtc).toLocaleString()}>
                                            {timeAgo(node.lastSeenUtc)}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>
        </div>
    );
}
