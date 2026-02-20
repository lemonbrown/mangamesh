export interface LogEntry {
    timestamp: string;
    level: number;
    category: string;
    message: string;
    exception?: string;
}

export async function getLogs(minLevel?: number): Promise<LogEntry[]> {
    const params = new URLSearchParams();
    if (minLevel !== undefined) params.set('minLevel', String(minLevel));
    const qs = params.size > 0 ? `?${params}` : '';
    const response = await fetch(`/api/node/logs${qs}`);
    if (!response.ok) throw new Error('Failed to fetch logs');
    return response.json();
}

export async function clearLogs(): Promise<void> {
    const response = await fetch('/api/node/logs', { method: 'DELETE' });
    if (!response.ok) throw new Error('Failed to clear logs');
}
