import client from './client';
import type { SeriesSearchResult, SeriesDetailsResponse, ChapterSummaryResponse, ChapterDetailsResponse, MangaMetadata, FullChapterManifest } from '../types/api';

export async function searchMetadata(query: string): Promise<MangaMetadata[]> {
    const response = await client.get(`/api/mangametadata/search?query=${encodeURIComponent(query)}`);
    return response.data;
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
    const response = await client.get(`/api/Series?${params.toString()}`);
    return response.data;
}

export async function getSeriesDetails(seriesId: string): Promise<SeriesDetailsResponse> {
    const response = await client.get(`/api/Series/${encodeURIComponent(seriesId)}`);
    return response.data;
}

export async function getSeriesChapters(seriesId: string): Promise<ChapterSummaryResponse[]> {
    const response = await client.get(`/api/Series/${encodeURIComponent(seriesId)}/chapters`);
    return response.data;
}

export async function getChapterDetails(seriesId: string, chapterId: string): Promise<ChapterDetailsResponse> {
    const response = await client.get(`/api/Series/${encodeURIComponent(seriesId)}/chapters/${encodeURIComponent(chapterId)}`);
    return response.data;
}

// Updated to use the client but note that reader content is usually fetched differently (images)
// but the manifest itself is JSON.
export async function readChapter(seriesId: string, chapterId: string, manifestHash: string): Promise<FullChapterManifest> {
    const response = await client.get(`/api/Series/${encodeURIComponent(seriesId)}/chapter/${encodeURIComponent(chapterId)}/manifest/${encodeURIComponent(manifestHash)}/read`);
    return response.data;
}
