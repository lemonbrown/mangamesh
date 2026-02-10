import type { MangaMetadata } from '../types/api';

export async function searchMetadata(query: string): Promise<MangaMetadata[]> {
    if (!query || query.length < 2) return [];

    const response = await fetch(`/api/mangametadata/search?query=${encodeURIComponent(query)}`);

    if (!response.ok) {
        // Return empty list on failure to avoid breaking UI flow? Or throw?
        // Throwing is better for debugging, component can handle.
        throw new Error(`Metadata search failed: ${response.statusText}`);
    }

    return await response.json();
}

export async function getMangaDetails(seriesId: string): Promise<import('../types/api').MangaDetails> {
    const response = await fetch(`/api/mangametadata/${encodeURIComponent(seriesId)}`);

    if (!response.ok) {
        throw new Error(`Failed to fetch manga details: ${response.statusText}`);
    }

    return await response.json();
}
