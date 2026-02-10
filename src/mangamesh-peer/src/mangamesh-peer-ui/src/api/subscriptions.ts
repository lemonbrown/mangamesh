import type { SeriesSummaryResponse, SeriesSubscription } from '../types/api';

const API_BASE_URL = '';

export async function getSubscriptions(): Promise<SeriesSubscription[]> {
    const response = await fetch(`${API_BASE_URL}/api/subscriptions/list`);
    if (!response.ok) throw new Error('Failed to fetch subscriptions');
    return await response.json();
}

export async function addSubscription(seriesId: string, subscription: Partial<SeriesSubscription> = {}): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/api/subscriptions/subscribe`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            seriesId,
            language: subscription.language || 'en',
            autoFetch: true,
            autoFetchScanlators: subscription.autoFetchScanlators || []
        })
    });
    if (!response.ok) throw new Error('Failed to subscribe');
}

export async function removeSubscription(seriesId: string): Promise<void> { // Removed payload, expecting object or just ID? Controller expects object
    const response = await fetch(`${API_BASE_URL}/api/subscriptions/unsubscribe`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ seriesId })
    });
    if (!response.ok) throw new Error('Failed to unsubscribe');
}

export async function updateSubscription(seriesId: string, scanlators: string[]): Promise<void> {
    // We treat update as re-add with new settings
    return addSubscription(seriesId, { autoFetchScanlators: scanlators });
}

export function subscribe(seriesId: string): Promise<void> {
    return addSubscription(seriesId);
}

export function unsubscribe(seriesId: string): Promise<void> {
    return removeSubscription(seriesId);
}

export async function getSubscriptionUpdates(): Promise<SeriesSummaryResponse[]> {
    const response = await fetch(`${API_BASE_URL}/api/subscriptions/updates`);
    if (!response.ok) throw new Error('Failed to fetch subscription updates');
    return await response.json();
}
