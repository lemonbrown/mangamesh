import { useState, useEffect } from 'react';
import { Server, Activity, Users, Globe } from 'lucide-react';
import clsx from 'clsx';
import { api } from '../services/api';
import type { Node, DashboardStats } from '../services/api';

export default function Dashboard() {
    const [nodes, setNodes] = useState<Node[]>([]);
    const [stats, setStats] = useState<DashboardStats>({ activeNodes: 0, totalPeers: 0, gateways: 0, bootstraps: 0 });
    const [filter, setFilter] = useState<Node['type'] | 'All'>('All');

    useEffect(() => {
        const fetchData = async () => {
            try {
                const [nodesData, statsData] = await Promise.all([
                    api.getNodes(),
                    api.getDashboardStats()
                ]);
                setNodes(nodesData);
                setStats(statsData);
            } catch (error) {
                console.error("Failed to fetch dashboard data:", error);
            }
        };

        fetchData();
        const interval = setInterval(fetchData, 30000); // Refresh every 30s
        return () => clearInterval(interval);
    }, []);

    const filteredNodes = filter === 'All' ? nodes : nodes.filter(n => n.type === filter);

    return (
        <div className="space-y-6">
            {/* Stats Cards */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
                <StatCard label="Active Nodes" value={stats.activeNodes} icon={<Activity className="text-green-500" />} />
                <StatCard label="Total Peers" value={stats.totalPeers} icon={<Users className="text-blue-500" />} />
                <StatCard label="Gateways" value={stats.gateways} icon={<Globe className="text-purple-500" />} />
                <StatCard label="Bootstraps" value={stats.bootstraps} icon={<Server className="text-amber-500" />} />
            </div>

            {/* Node List */}
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
                <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
                    <h3 className="text-lg font-medium text-gray-900">Network Nodes</h3>
                    <div className="flex space-x-2">
                        {['All', 'Peer', 'Gateway', 'Bootstrap'].map(f => (
                            <button
                                key={f}
                                onClick={() => setFilter(f as any)}
                                className={clsx(
                                    "px-3 py-1 text-xs font-medium rounded-full transition-colors",
                                    filter === f
                                        ? "bg-slate-900 text-white"
                                        : "bg-gray-100 text-gray-600 hover:bg-gray-200"
                                )}
                            >
                                {f}
                            </button>
                        ))}
                    </div>
                </div>
                <div className="overflow-x-auto">
                    <table className="min-w-full divide-y divide-gray-200">
                        <thead className="bg-gray-50">
                            <tr>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Node ID</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Type</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Address</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Version</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                                <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Last Seen</th>
                            </tr>
                        </thead>
                        <tbody className="bg-white divide-y divide-gray-200">
                            {filteredNodes.map((node) => (
                                <tr key={node.id} className="hover:bg-gray-50">
                                    <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900 font-mono">
                                        {node.id}
                                    </td>
                                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                                        <span className={clsx(
                                            "inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium",
                                            node.type === 'Peer' && "bg-blue-100 text-blue-800",
                                            node.type === 'Gateway' && "bg-purple-100 text-purple-800",
                                            node.type === 'Bootstrap' && "bg-amber-100 text-amber-800",
                                            node.type === 'Mixed' && "bg-gray-100 text-gray-800",
                                        )}>
                                            {node.type}
                                        </span>
                                    </td>
                                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500 font-mono">
                                        {node.ip}:{node.port}
                                    </td>
                                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                                        v{node.version}
                                    </td>
                                    <td className="px-6 py-4 whitespace-nowrap text-sm">
                                        <div className="flex items-center">
                                            <div className={clsx(
                                                "h-2.5 w-2.5 rounded-full mr-2",
                                                node.status === 'Online' ? "bg-green-500" : "bg-gray-300"
                                            )} />
                                            <span className={node.status === 'Online' ? "text-green-700 font-medium" : "text-gray-500"}>
                                                {node.status}
                                            </span>
                                        </div>
                                    </td>
                                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500 text-right">
                                        {new Date(node.lastSeen).toLocaleTimeString()}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    );
}

function StatCard({ label, value, icon }: { label: string; value: number; icon: React.ReactNode }) {
    return (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 flex items-center justify-between">
            <div>
                <p className="text-sm font-medium text-gray-500">{label}</p>
                <p className="text-2xl font-semibold text-gray-900 mt-1">{value}</p>
            </div>
            <div className="p-3 bg-gray-50 rounded-lg">
                {icon}
            </div>
        </div>
    );
}
