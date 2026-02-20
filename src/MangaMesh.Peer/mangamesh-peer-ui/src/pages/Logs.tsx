import { useState, useEffect, useRef } from 'react';
import { getLogs, clearLogs } from '../api/logs';
import type { LogEntry } from '../api/logs';

const LEVEL_NAMES: Record<number, string> = {
    0: 'Trace',
    1: 'Debug',
    2: 'Info',
    3: 'Warn',
    4: 'Error',
    5: 'Critical',
};

const LEVEL_COLORS: Record<number, string> = {
    0: 'text-gray-400',
    1: 'text-blue-500',
    2: 'text-green-600',
    3: 'text-yellow-600',
    4: 'text-red-600',
    5: 'text-red-900 font-bold',
};

const LEVEL_OPTIONS = [
    { label: 'All', value: undefined },
    { label: 'Debug+', value: 1 },
    { label: 'Info+', value: 2 },
    { label: 'Warn+', value: 3 },
    { label: 'Error+', value: 4 },
];

export default function Logs() {
    const [logs, setLogs] = useState<LogEntry[]>([]);
    const [loading, setLoading] = useState(true);
    const [minLevel, setMinLevel] = useState<number | undefined>(2); // default Info+
    const [paused, setPaused] = useState(false);
    const [clearing, setClearing] = useState(false);
    const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

    async function fetchLogs() {
        try {
            const data = await getLogs(minLevel);
            setLogs(data);
        } catch {
            // silently ignore fetch errors during polling
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => {
        setLoading(true);
        fetchLogs();
    }, [minLevel]);

    useEffect(() => {
        if (paused) {
            if (intervalRef.current) clearInterval(intervalRef.current);
        } else {
            intervalRef.current = setInterval(fetchLogs, 3000);
        }
        return () => { if (intervalRef.current) clearInterval(intervalRef.current); };
    }, [paused, minLevel]);

    async function handleClear() {
        setClearing(true);
        try {
            await clearLogs();
            setLogs([]);
        } catch {
            alert('Failed to clear logs');
        } finally {
            setClearing(false);
        }
    }

    function shortCategory(category: string) {
        // Show last 2 segments: e.g. "Core.Node.DhtNode" → "Node.DhtNode"
        const parts = category.split('.');
        return parts.slice(-2).join('.');
    }

    return (
        <div className="space-y-4">
            {/* Header */}
            <div className="flex items-center justify-between">
                <h1 className="text-2xl font-bold text-gray-900">Logs</h1>
                <div className="flex items-center gap-2">
                    {/* Level filter */}
                    <select
                        value={minLevel ?? ''}
                        onChange={e => setMinLevel(e.target.value === '' ? undefined : Number(e.target.value))}
                        className="text-sm border border-gray-300 rounded-md px-2 py-1.5 bg-white focus:outline-none focus:ring-2 focus:ring-blue-500"
                    >
                        {LEVEL_OPTIONS.map(o => (
                            <option key={String(o.value)} value={o.value ?? ''}>
                                {o.label}
                            </option>
                        ))}
                    </select>

                    {/* Pause/Resume */}
                    <button
                        onClick={() => setPaused(p => !p)}
                        className={`px-3 py-1.5 text-sm font-medium rounded-md border transition-colors ${
                            paused
                                ? 'bg-green-600 text-white border-green-600 hover:bg-green-700'
                                : 'bg-white text-gray-700 border-gray-300 hover:bg-gray-50'
                        }`}
                    >
                        {paused ? 'Resume' : 'Pause'}
                    </button>

                    {/* Refresh */}
                    <button
                        onClick={fetchLogs}
                        className="px-3 py-1.5 text-sm font-medium rounded-md border border-gray-300 bg-white text-gray-700 hover:bg-gray-50 transition-colors"
                    >
                        Refresh
                    </button>

                    {/* Clear */}
                    <button
                        onClick={handleClear}
                        disabled={clearing || logs.length === 0}
                        className="px-3 py-1.5 text-sm font-medium rounded-md border border-gray-300 bg-white text-gray-700 hover:bg-red-50 hover:text-red-600 hover:border-red-300 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                    >
                        Clear
                    </button>
                </div>
            </div>

            {/* Status bar */}
            <div className="flex items-center gap-2 text-xs text-gray-500">
                <span className={`inline-block w-2 h-2 rounded-full ${paused ? 'bg-yellow-400' : 'bg-green-400 animate-pulse'}`} />
                {paused ? 'Paused' : 'Live — refreshing every 3s'}
                {logs.length > 0 && <span className="ml-2">{logs.length} entries</span>}
            </div>

            {/* Log table */}
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
                {loading && logs.length === 0 ? (
                    <div className="p-8 text-center text-gray-400 text-sm">Loading logs…</div>
                ) : logs.length === 0 ? (
                    <div className="p-8 text-center text-gray-500 text-sm">No log entries captured yet.</div>
                ) : (
                    <div className="overflow-x-auto">
                        <table className="min-w-full divide-y divide-gray-100 text-xs font-mono">
                            <thead className="bg-gray-50 text-[10px] uppercase tracking-wider text-gray-500">
                                <tr>
                                    <th className="px-3 py-2 text-left w-28">Time</th>
                                    <th className="px-3 py-2 text-left w-16">Level</th>
                                    <th className="px-3 py-2 text-left w-48">Category</th>
                                    <th className="px-3 py-2 text-left">Message</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-gray-100">
                                {logs.map((log, idx) => (
                                    <tr key={idx} className={`hover:bg-gray-50 ${log.level >= 4 ? 'bg-red-50' : log.level === 3 ? 'bg-yellow-50' : ''}`}>
                                        <td className="px-3 py-1.5 whitespace-nowrap text-gray-400">
                                            {new Date(log.timestamp).toLocaleTimeString()}
                                        </td>
                                        <td className={`px-3 py-1.5 whitespace-nowrap font-semibold ${LEVEL_COLORS[log.level] ?? 'text-gray-700'}`}>
                                            {LEVEL_NAMES[log.level] ?? log.level}
                                        </td>
                                        <td className="px-3 py-1.5 whitespace-nowrap text-gray-500 truncate max-w-[12rem]" title={log.category}>
                                            {shortCategory(log.category)}
                                        </td>
                                        <td className="px-3 py-1.5 text-gray-900 break-all">
                                            {log.message}
                                            {log.exception && (
                                                <div className="mt-1 text-red-700 whitespace-pre-wrap border-l-2 border-red-300 pl-2 text-[10px]">
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
