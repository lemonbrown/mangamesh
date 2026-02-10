import type { ChapterSummary, ChapterMetadata } from '../types/api';
import { mockApi } from './mock';

export async function getChapters(seriesId: string): Promise<ChapterSummary[]> {
    return mockApi.getChapters(seriesId);
}

export async function getRecentChapters(limit: number): Promise<ChapterSummary[]> {
    return mockApi.getRecentChapters(limit);
}

export async function getChapterMetadata(manifestHash: string): Promise<ChapterMetadata> {
    return mockApi.getChapterMetadata(manifestHash);
}

export async function getPageImage(manifestHash: string, pageIndex: number): Promise<Blob> {
    return mockApi.getPageImage(manifestHash, pageIndex);
}
