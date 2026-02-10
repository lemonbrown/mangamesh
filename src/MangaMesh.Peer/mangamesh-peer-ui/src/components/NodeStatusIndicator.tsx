import React, { useEffect, useState } from 'react';
import { getNodeStatus } from '../api/node';
import type { NodeStatus } from '../types/api';

const NodeStatusIndicator: React.FC = () => {
    const [status, setStatus] = useState<NodeStatus | null>(null);
    const [showDetails, setShowDetails] = useState(false);

    useEffect(() => {
        const fetchStatus = async () => {
            try {
                const data = await getNodeStatus();
                setStatus(data);
            } catch (error) {
                console.error('Failed to fetch node status', error);
                // Reset status on error if desired, or keep last known
            }
        };

        const interval = setInterval(fetchStatus, 5000);
        fetchStatus();

        return () => clearInterval(interval);
    }, []);

    if (!status) return null;

    const getTimeAgo = (dateString: string | null) => {
        if (!dateString) return 'Never';
        const date = new Date(dateString);
        const seconds = Math.floor((new Date().getTime() - date.getTime()) / 1000);

        if (seconds < 60) return `${seconds} seconds ago`;
        const minutes = Math.floor(seconds / 60);
        if (minutes < 60) return `${minutes} minutes ago`;
        const hours = Math.floor(minutes / 60);
        return `${hours} hours ago`;
    };

    return (
        <div className="relative">
            <button
                onClick={() => setShowDetails(!showDetails)}
                className="flex items-center gap-2 px-3 py-1 text-xs rounded bg-slate-800 border border-slate-700 hover:bg-slate-700 transition-colors cursor-pointer"
            >
                <div
                    className={`w-2 h-2 rounded-full ${status.isConnected ? 'bg-green-500 shadow-[0_0_5px_rgba(34,197,94,0.5)]' : 'bg-red-500'}`}
                />
                <span className="font-mono text-slate-400 select-all">
                    {status.nodeId.substring(0, 8)}...
                </span>
            </button>

            {showDetails && (
                <div className="absolute right-0 top-full mt-2 w-64 p-3 bg-white rounded-lg shadow-xl border border-gray-200 z-50 text-xs">
                    <div className="space-y-2">
                        <div>
                            <span className="text-gray-500 block mb-1">Index API</span>
                            <code className="bg-gray-100 px-1 py-0.5 rounded text-gray-800 break-all select-all">
                                {status.trackerUrl}
                            </code>
                        </div>
                        <div>
                            <span className="text-gray-500 block mb-1">Node ID</span>
                            <code className="bg-gray-100 px-1 py-0.5 rounded text-gray-800 break-all select-all text-[10px]">
                                {status.nodeId}
                            </code>
                        </div>
                        <div>
                            <span className="text-gray-500 block mb-1">Last Ping</span>
                            <span className="text-gray-800 font-medium">
                                {getTimeAgo(status.lastPingUtc)}
                            </span>
                        </div>
                        <div>
                            <span className="text-gray-500 block mb-1">Status</span>
                            <span className={status.isConnected ? 'text-green-600 font-medium' : 'text-red-600 font-medium'}>
                                {status.isConnected ? 'Connected' : 'Disconnected'}
                            </span>
                        </div>
                    </div>

                    {/* Click outside to close could be added here or via a wrapper */}
                    <div
                        className="fixed inset-0 z-[-1]"
                        onClick={() => setShowDetails(false)}
                    />
                </div>
            )}
        </div>
    );
};

export default NodeStatusIndicator;
