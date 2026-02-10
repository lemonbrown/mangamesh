import React, { useEffect, useState } from 'react';
import { getNodeStatus } from '../api/node';
import type { NodeStatus } from '../types/api';

const NodeStatusIndicator: React.FC = () => {
    const [status, setStatus] = useState<NodeStatus | null>(null);

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

    return (
        <div className="flex items-center gap-2 px-3 py-1 text-xs rounded bg-slate-800 border border-slate-700">
            <div
                className={`w-2 h-2 rounded-full ${status.isConnected ? 'bg-green-500 shadow-[0_0_5px_rgba(34,197,94,0.5)]' : 'bg-red-500'}`}
                title={status.isConnected ? 'Connected to Backend' : 'Disconnected'}
            />
            <span className="font-mono text-slate-400 select-all" title="Node ID">
                {status.nodeId.substring(0, 8)}...
            </span>
        </div>
    );
};

export default NodeStatusIndicator;
