import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getSubscriptions, removeSubscription } from '../api/subscriptions';
import type { Subscription } from '../types/api';


export default function Subscriptions() {
    const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);


    async function load() {
        try {
            const data = await getSubscriptions();
            setSubscriptions(data);
            setError(null);
        } catch (e) {
            setError('Failed to load subscriptions');
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => {
        load();
    }, []);



    async function handleRemove(sub: Subscription) {
        if (!confirm(`Unsubscribe from ${sub.seriesId}?`)) return;
        try {
            await removeSubscription(sub.seriesId);
            load();
        } catch (e) {
            alert('Failed to remove subscription');
        }
    }

    return (
        <div className="space-y-8">


            <div>
                <h2 className="text-lg font-medium text-gray-900 mb-4">Active Subscriptions</h2>
                {loading && <div className="text-gray-500">Loading...</div>}
                {error && <div className="text-red-500">{error}</div>}

                {!loading && !error && subscriptions.length === 0 && (
                    <div className="text-gray-500 italic">No subscriptions yet.</div>
                )}

                <div className="grid gap-4">
                    {subscriptions.map((sub, i) => (
                        <div key={i} className="flex items-center justify-between bg-white p-4 rounded-lg shadow-sm border border-gray-200">
                            <div className="flex-1">
                                <span className="text-xs text-gray-500 uppercase tracking-wide">Series</span>
                                <div className="font-medium text-lg">
                                    <Link
                                        to={`/series/${sub.seriesId}`}
                                        className="text-blue-600 hover:underline"
                                    >
                                        {sub.seriesId}
                                    </Link>
                                </div>
                                <div className="text-sm text-gray-500 mt-1">
                                    Auto-fetch: {sub.autoFetchScanlators.length > 0 ? sub.autoFetchScanlators.join(', ') : 'None'}
                                </div>
                            </div>
                            <button
                                onClick={() => handleRemove(sub)}
                                className="text-red-600 hover:text-red-800 text-sm font-medium px-3 py-1 rounded hover:bg-red-50"
                            >
                                Unsubscribe
                            </button>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
}
