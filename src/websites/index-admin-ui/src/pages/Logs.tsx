import { useState, useEffect } from 'react';
import { Download, Pause, Play, Trash2 } from 'lucide-react';
import clsx from 'clsx';
import { api } from '../services/api';
import type { LogEntry } from '../services/api';

export default function Logs() {
    const [logs, setLogs] = useState<LogEntry[]>([]);
    const [paused, setPaused] = useState(false);
    const [filterLevel, setFilterLevel] = useState<'all' | 'error' | 'warn' | 'info'>('all');

    useEffect(() => {
        if (paused) return;

        const fetchLogs = async () => {
            // TODO: Real logs might need pagination or streaming
            // For now just fetching all/filtered
            try {
                const level = filterLevel === 'all' ? undefined : filterLevel;
                const data = await api.getLogs(level);
                setLogs(data);
            } catch (error) {
                console.error("Failed to fetch logs:", error);
            }
        };

        fetchLogs();
        const interval = setInterval(fetchLogs, 3000);
        return () => clearInterval(interval);
    }, [paused, filterLevel]);

    const filteredLogs = logs;

    return (
        <div className="space-y-6">
            <div className="flex justify-between items-center">
                <h1 className="text-2xl font-bold text-gray-900">System Logs</h1>
                <div className="flex space-x-2">
                    <select
                        className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                        value={filterLevel}
                        onChange={(e) => setFilterLevel(e.target.value as any)}
                    >
                        <option value="all">All Levels</option>
                        <option value="info">Info</option>
                        <option value="warn">Warn</option>
                        <option value="error">Error</option>
                    </select>
                    <button
                        onClick={() => setLogs([])}
                        className="p-2 border border-gray-300 rounded-lg text-gray-600 hover:bg-gray-50"
                        title="Clear LOgs"
                    >
                        <Trash2 className="w-5 h-5" />
                    </button>
                    <button
                        onClick={() => setPaused(!paused)}
                        className="p-2 border border-gray-300 rounded-lg text-gray-600 hover:bg-gray-50"
                        title={paused ? "Resume" : "Pause"}
                    >
                        {paused ? <Play className="w-5 h-5" /> : <Pause className="w-5 h-5" />}
                    </button>
                    <button className="p-2 bg-slate-900 border border-slate-900 rounded-lg text-white hover:bg-slate-800">
                        <Download className="w-5 h-5" />
                    </button>
                </div>
            </div>

            <div className="bg-gray-900 rounded-xl shadow-sm border border-gray-800 overflow-hidden text-gray-300 font-mono text-sm max-h-[600px] overflow-y-auto">
                <div className="p-4 space-y-1">
                    {filteredLogs.map((log) => (
                        <div key={log.id} className="flex gap-4 hover:bg-gray-800/50 p-1 rounded">
                            <span className="text-gray-500 whitespace-nowrap w-40 shrink-0">
                                {new Date(log.timestamp).toISOString().split('T')[1].replace('Z', '')}
                            </span>
                            <span className={clsx(
                                "w-16 shrink-0 font-bold uppercase",
                                log.level === 'info' && "text-blue-400",
                                log.level === 'warn' && "text-yellow-400",
                                log.level === 'error' && "text-red-500",
                                log.level === 'debug' && "text-gray-500",
                            )}>
                                {log.level}
                            </span>
                            <span className="text-purple-400 w-32 shrink-0 truncate">
                                [{log.category}]
                            </span>
                            <span className="break-all">
                                {log.message}
                            </span>
                        </div>
                    ))}
                    {filteredLogs.length === 0 && (
                        <div className="text-gray-600 text-center py-8">
                            No logs to display
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
}
