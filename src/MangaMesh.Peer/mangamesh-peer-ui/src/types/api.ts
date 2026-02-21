export interface NodeStatus {
    nodeId: string;
    isConnected: boolean;
    lastPingUtc: string | null;
    peerCount: number;
    seededManifests: number;
    storageUsedMb: number;
    trackerUrl: string;
}

export interface SeriesSubscription {
    seriesId: string;
    language: string;
    autoFetch: boolean;             // Matches backend
    autoFetchScanlators: string[]; // Matches backend
    subscribedAt?: string;
}

export type Subscription = SeriesSubscription; // Alias for backward compatibility

export interface ImportChapterRequest {
    seriesId: string;
    scanlatorId: string;
    language: string;
    chapterNumber: number;
    sourcePath: string;
    displayName: string;
    releaseType: string;
    source: number; // 0=MangaDex, 1=AniList, 2=MAL
    externalMangaId: string;
    quality: string;
}

export interface StorageStats {
    totalMb: number;
    usedMb: number;
    manifestCount: number;
    blobCount: number;
}

export interface StoredBlob {
    hash: string;
    sizeBytes: number;
}

export interface ChapterSummary {
    manifestHash: string;
    seriesId: string;
    scanlatorId: string;
    language: string;
    chapterNumber: number;
    title?: string;
    uploadedAt: string; // ISO Date string
}

export interface ChapterMetadata {
    manifestHash: string; // Added for convenience in UI
    seriesId: string;
    scanlatorId: string;
    language: string;
    chapterNumber: number;
    pageCount: number;
    pages: string[]; // List of page identifiers/filenames
}

export interface SeriesSearchResult {
    seriesId: string;
    title: string;
    seedCount?: number;
    chapterCount?: number;
    lastUploadedAt?: string; // ISO Date
    source: number;
    externalMangaId: string;
    year?: number;
    latestChapterNumber?: number;
    latestChapterTitle?: string;
}

export type SeriesSummaryResponse = SeriesSearchResult;

export interface SeriesDetailsResponse {
    seriesId: string;
    title: string;
    description?: string;
    status?: string;
    year?: number;
}

export interface ChapterSummaryResponse {
    chapterId: string;
    chapterNumber: string;
    volume?: string;
    title?: string;
}

export interface ChapterManifest {
    manifestHash: string;
    language: string;
    scanGroup?: string;
    isVerified?: boolean;
    quality: string;
    uploadedAt: string;
}

export interface ChapterDetailsResponse {
    chapterId: string;
    seriesId: string;
    chapterNumber: string;
    title?: string;
    manifests: ChapterManifest[];
    pages: string[];
}

export interface ImportedChapter {
    seriesId: string;
    scanlatorId: string;
    language: string;
    chapterNumber: number;
    sourcePath: string;
    displayName: string;
    releaseType: string;
}

export interface MangaMetadata {
    source: number;
    externalMangaId: string;
    title: string;
    altTitles: string[];
    status: string;
    year: number;
}

export interface MangaChapter {
    chapterId: string;
    mangaId: string;
    source: number;
    chapterNumber: string;
    volume: string;
    title: string;
    language: string;
    publishDate: string;
}

export interface MangaDetails {
    mangaId: string;
    source: number;
    language: string;
    chapters: MangaChapter[];
}

export interface KeyPair {
    publicKeyBase64: string;
    privateKeyBase64?: string;
}

export interface KeyChallenge {
    challengeId: string;
    nonce: string;
    expiresAt: string;
}

export interface VerifySignatureResponse {
    valid: boolean;
}

export interface AnalyzedChapterDto {
    sourcePath: string;
    suggestedChapterNumber: number;
    fileCount: number;
}


export interface ManifestFile {
    hash: string;
    filename: string;
    size: number;
}

export interface FullChapterManifest {
    version: string;
    seriesId: string;
    chapterId: string;
    chapterNumber: number; // or ensure type match
    files: ManifestFile[];
}

export interface ImportChapterResult {
    manifestHash: string;
    fileCount: number;
    alreadyExists: boolean;
}


export interface StoredManifest {
    hash: string;
    seriesId: string;
    chapterNumber: string;
    volume?: string;
    language: string;
    scanGroup: string;
    title: string;
    sizeBytes: number;
    fileCount: number;
    createdUtc: string;
}

export interface PagedResult<T> {
    items: T[];
    total: number;
    offset: number;
    limit: number;
}

export interface KnownNode {
    nodeId: string;
    host: string;
    port: number;
    httpApiPort: number;
    lastSeenUtc: string;
}
