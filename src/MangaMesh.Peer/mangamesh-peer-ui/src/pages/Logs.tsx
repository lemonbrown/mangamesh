import { useState, useEffect } from 'react';

interface LogEntry {
    timestamp: string;
    level: number;
    category: string;
    message: string;
    exception?: string;
}

const LogLevelNames: Record<number, string> = {
    0: 'Trace',
    1: 'Debug',
    2: 'Info',
    3: 'Warn',
    4: 'Error',
    5: 'Critical',
    6: 'None'
};

const LogLevelColors: Record<number, string> = {
    0: 'text-gray-500', // Trace
    1: 'text-blue-500', // Debug
    2: 'text-green-600', // Info
    3: 'text-yellow-600', // Warn
    4: 'text-red-600', // Error
    5: 'text-red-900 font-bold', // Critical
};

export default function Logs() {
    const [logs, setLogs] = useState<LogEntry[]>([]);
    const [loading, setLoading] = useState(true);

    const fetchLogs = async () => {
        try {
            const response = await fetch(`${import.meta.env.VITE_API_URL || 'https://localhost:7030'}/logs`);
            if (response.ok) {
                const data = await response.json();
                setLogs(data);
            }
        } catch (error) {
            console.error('Failed to fetch logs', error);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchLogs();
        const interval = setInterval(fetchLogs, 5000); // Auto refresh every 5s
        return () => clearInterval(interval);
    }, []);

    return (
        <div className="space-y-6">
            <div className="flex justify-between items-center">
                <h1 className="text-2xl font-semibold text-gray-900">Logs</h1>
                <button
                    onClick={() => fetchLogs()}
                    className="px-4 py-2 bg-white border border-gray-300 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-50 hover:text-gray-900 transition-colors"
                >
                    Refresh
                </button>
            </div>

            <div className="bg-white shadow rounded-lg overflow-hidden border border-gray-200">
                {loading && logs.length === 0 ? (
                    <div className="p-8 text-center text-gray-500">Loading logs...</div>
                ) : logs.length === 0 ? (
                    <div className="p-8 text-center text-gray-500">No logs captured yet.</div>
                ) : (
                    <div className="overflow-x-auto">
                        <table className="min-w-full divide-y divide-gray-200 text-sm">
                            <thead className="bg-gray-50">
                                <tr>
                                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider w-32">Time</th>
                                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider w-20">Level</th>
                                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider w-48">Category</th>
                                    <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Message</th>
                                </tr>
                            </thead>
                            <tbody className="bg-white divide-y divide-gray-200">
                                {logs.map((log, idx) => (
                                    <tr key={idx} className="hover:bg-gray-50 font-mono text-xs">
                                        <td className="px-4 py-2 whitespace-nowrap text-gray-500">
                                            {new Date(log.timestamp).toLocaleTimeString()}
                                        </td>
                                        <td className={`px-4 py-2 whitespace-nowrap font-semibold ${LogLevelColors[log.level] || 'text-gray-700'}`}>
                                            {LogLevelNames[log.level] || log.level}
                                        </td>
                                        <td className="px-4 py-2 whitespace-nowrap text-gray-600 truncate max-w-xs" title={log.category}>
                                            {log.category.split('.').pop()} {/* Show simplified category */}
                                        </td>
                                        <td className="px-4 py-2 text-gray-900 break-words">
                                            {log.message}
                                            {log.exception && (
                                                <div className="mt-1 text-red-700 whitespace-pre-wrap pl-2 border-l-2 border-red-300">
                                                    {log.exception}
                                                </div>
                                            )}
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
