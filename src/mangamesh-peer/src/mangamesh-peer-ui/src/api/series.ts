import type { SeriesSearchResult, SeriesDetailsResponse, ChapterSummaryResponse, ChapterDetailsResponse, MangaMetadata, FullChapterManifest } from '../types/api';

export async function searchMetadata(query: string): Promise<MangaMetadata[]> {
    const response = await fetch(`/api/mangametadata/search?query=${encodeURIComponent(query)}`);
    if (!response.ok) {
        throw new Error('Failed to search metadata');
    }
    return await response.json();
}

export async function searchSeries(q: string, limit: number = 20, offset: number = 0, sort?: string): Promise<SeriesSearchResult[]> {
    const params = new URLSearchParams({
        q,
        limit: limit.toString(),
        offset: offset.toString()
    });
    if (sort) {
        params.append('sort', sort);
    }
    const response = await fetch(`/api/Series?${params.toString()}`);
    if (!response.ok) {
        throw new Error(`Failed to search series: ${response.statusText}`);
    }
    return await response.json();
}

export async function getSeriesDetails(seriesId: string): Promise<SeriesDetailsResponse> {
    const response = await fetch(`/api/Series/${encodeURIComponent(seriesId)}`);
    if (!response.ok) {
        throw new Error(`Failed to fetch series details: ${response.statusText}`);
    }
    return await response.json();
}

export async function getSeriesChapters(seriesId: string): Promise<ChapterSummaryResponse[]> {
    const response = await fetch(`/api/Series/${encodeURIComponent(seriesId)}/chapters`);
    if (!response.ok) {
        throw new Error(`Failed to fetch series chapters: ${response.statusText}`);
    }
    return await response.json();
}

export async function getChapterDetails(seriesId: string, chapterId: string): Promise<ChapterDetailsResponse> {
    const response = await fetch(`/api/Series/${encodeURIComponent(seriesId)}/chapters/${encodeURIComponent(chapterId)}`);
    if (!response.ok) {
        throw new Error(`Failed to fetch chapter details: ${response.statusText}`);
    }
    return await response.json();
}

export async function readChapter(seriesId: string, chapterId: string, manifestHash: string): Promise<FullChapterManifest> {
    const response = await fetch(`/api/Series/${encodeURIComponent(seriesId)}/chapter/${encodeURIComponent(chapterId)}/manifest/${encodeURIComponent(manifestHash)}/read`);
    if (!response.ok) {
        throw new Error(`Failed to read chapter: ${response.statusText}`);
    }
    return await response.json();
}
