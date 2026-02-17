/** Fixture data for mocked E2E tests. Typed as plain objects to avoid JSON import issues. */

export const seriesList = [
    {
        seriesId: 'test-series-001',
        title: 'Test Manga',
        seedCount: 3,
        chapterCount: 2,
        lastUploadedAt: '2026-02-01T10:00:00Z',
        source: 0,
        externalMangaId: 'ext-001',
        year: 2023,
        latestChapterNumber: 2,
        latestChapterTitle: 'Return',
    },
];

export const seriesDetails = {
    seriesId: 'test-series-001',
    title: 'Test Manga',
    description: 'A test manga series used for E2E testing.',
    status: 'Ongoing',
    year: 2023,
    externalMangaId: 'ext-001',
};

export const seriesChapters = [
    {
        chapterId: 'ch-001',
        chapterNumber: '1',
        volume: '1',
        title: 'The Beginning',
        uploadedAt: '2026-01-15T10:00:00Z',
    },
    {
        chapterId: 'ch-002',
        chapterNumber: '2',
        volume: '1',
        title: 'Return',
        uploadedAt: '2026-02-01T10:00:00Z',
    },
];

export const chapterDetails = {
    chapterId: 'ch-001',
    seriesId: 'test-series-001',
    chapterNumber: '1',
    title: 'The Beginning',
    manifests: [
        {
            manifestHash: 'aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899',
            language: 'en',
            scanGroup: 'TestScans',
            isVerified: false,
            quality: 'HQ',
            uploadedAt: '2026-01-15T10:00:00Z',
        },
    ],
    pages: [],
};

export const chapterManifest = {
    version: '1',
    seriesId: 'test-series-001',
    chapterId: 'ch-001',
    chapterNumber: 1,
    files: [
        { hash: 'page001hash000000000000000000000000000000000000000000000000000000', filename: 'page001.jpg', size: 12345 },
        { hash: 'page002hash000000000000000000000000000000000000000000000000000000', filename: 'page002.jpg', size: 11234 },
    ],
    nodes: ['localhost:9000'],
};
