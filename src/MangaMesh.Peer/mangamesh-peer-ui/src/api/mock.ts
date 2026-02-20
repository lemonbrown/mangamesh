import type { NodeStatus, Subscription, StorageStats, ChapterSummary, ChapterMetadata, SeriesSearchResult, ImportedChapter } from '../types/api';

export const mockNodeStatus: NodeStatus = {
    nodeId: "mock-node-12345",
    peerCount: 42,
    seededManifests: 12,
    storageUsedMb: 450,
    isConnected: true,
    lastPingUtc: new Date().toISOString(),
    trackerUrl: "http://mock-tracker:7030"
};

export const mockStorageStats: StorageStats = {
    totalMb: 51200, // 50 GB
    usedMb: 12450,
    manifestCount: 156,
    blobCount: 0
};

// Mutable mock data
let mockSubscriptions: Subscription[] = [
    { seriesId: "one-piece", autoFetchScanlators: ["tcb-scans"], language: "en", autoFetch: true },
    { seriesId: "jujutsu-kaisen", autoFetchScanlators: [], language: "en", autoFetch: false },
    { seriesId: "chainsaw-man", autoFetchScanlators: ["viz"], language: "en", autoFetch: true }
];

export const mockApi = {
    getNodeStatus: async (): Promise<NodeStatus> => {
        return new Promise(resolve => setTimeout(() => resolve(mockNodeStatus), 500));
    },

    getStorageStats: async (): Promise<StorageStats> => {
        return new Promise(resolve => setTimeout(() => resolve(mockStorageStats), 500));
    },

    getSubscriptions: async (): Promise<Subscription[]> => {
        return new Promise(resolve => setTimeout(() => resolve([...mockSubscriptions]), 600));
    },

    addSubscription: async (sub: Subscription): Promise<void> => {
        return new Promise(resolve => {
            setTimeout(() => {
                if (!mockSubscriptions.find(s => s.seriesId === sub.seriesId)) {
                    mockSubscriptions.push({ ...sub, autoFetchScanlators: [] });
                }
                resolve();
            }, 400);
        });
    },

    removeSubscription: async (sub: Subscription): Promise<void> => {
        return new Promise(resolve => {
            setTimeout(() => {
                mockSubscriptions = mockSubscriptions.filter(s => s.seriesId !== sub.seriesId);
                resolve();
            }, 400);
        });
    },

    updateSubscription: async (seriesId: string, autoFetchScanlators: string[]): Promise<void> => {
        return new Promise(resolve => {
            setTimeout(() => {
                const sub = mockSubscriptions.find(s => s.seriesId === seriesId);
                if (sub) {
                    sub.autoFetchScanlators = autoFetchScanlators;
                }
                resolve();
            }, 300);
        });
    },

    importChapter: async (): Promise<void> => {
        return new Promise(resolve => setTimeout(resolve, 1500));
    },

    // Chapter API
    getChapters: async (seriesId: string): Promise<ChapterSummary[]> => {
        return new Promise(resolve => {
            setTimeout(() => {
                // Generate some mock chapters for the requested series
                const chapters: ChapterSummary[] = [];
                const scanlators = ["tcb-scans", "viz", "fan-scans"];

                // Determine scanlators based on series roughly for simulation
                const seriesScanlators = scanlators;

                for (let i = 1; i <= 15; i++) {
                    const scanlatorId = seriesScanlators[i % seriesScanlators.length];
                    const chapterNum = 1100 + Math.floor(i / 2); // Duplicate chapters for different scanlators

                    chapters.push({
                        manifestHash: `hash-${seriesId}-${i}`,
                        seriesId,
                        scanlatorId,
                        language: "en",
                        chapterNumber: chapterNum,
                        title: `Chapter ${chapterNum}`,
                        uploadedAt: new Date(Date.now() - i * 86400000).toISOString()
                    });
                }
                resolve(chapters.sort((a, b) => b.chapterNumber - a.chapterNumber));
            }, 400);
        });
    },

    getRecentChapters: async (limit: number = 5): Promise<ChapterSummary[]> => {
        return new Promise(resolve => {
            setTimeout(() => {
                const chapters: ChapterSummary[] = [];
                // Mock recent chapters from random monitored series
                const baseTime = new Date();
                const series = ["one-piece", "jujutsu-kaisen", "chainsaw-man", "sakamoto-days"];

                for (let i = 0; i < limit; i++) {
                    const seriesId = series[i % series.length];
                    const time = new Date(baseTime.getTime() - i * 3600 * 1000); // 1 hour intervals
                    chapters.push({
                        manifestHash: `hash-recent-${i}`,
                        seriesId: seriesId,
                        scanlatorId: "mock-scans",
                        language: "en",
                        chapterNumber: 1100 - i,
                        title: `Recent Chapter ${1100 - i}`,
                        uploadedAt: time.toISOString()
                    });
                }
                resolve(chapters);
            }, 400);
        });
    },

    getChapterMetadata: async (manifestHash: string): Promise<ChapterMetadata> => {
        return new Promise((resolve, reject) => {
            setTimeout(() => {
                const parts = manifestHash.split('-');
                if (parts.length < 3) {
                    reject(new Error("Invalid hash"));
                    return;
                }
                // hash-seriesId-chapterNum (fake parsing)
                const seriesId = parts[1];

                resolve({
                    manifestHash,
                    seriesId: seriesId || "unknown",
                    scanlatorId: "mock-scan",
                    language: "en",
                    chapterNumber: 1000,
                    pageCount: 5,
                    pages: ["p1", "p2", "p3", "p4", "p5"]
                });
            }, 500);
        });
    },



    getPageImage: async (manifestHash: string, pageIndex: number): Promise<Blob> => {
        return new Promise(resolve => {
            setTimeout(() => {
                const svg = `
                  <svg width="600" height="800" xmlns="http://www.w3.org/2000/svg">
                      <rect width="100%" height="100%" fill="#eee" />
                      <text x="50%" y="50%" font-family="Arial" font-size="24" text-anchor="middle" fill="#555">
                          Page ${pageIndex + 1}
                      </text>
                      <text x="50%" y="55%" font-family="Arial" font-size="14" text-anchor="middle" fill="#999">
                          ${manifestHash}
                      </text>
                  </svg>
                `;
                const blob = new Blob([svg], { type: 'image/svg+xml' });
                resolve(blob);
            }, 300);
        });
    },

    searchSeries: async (query: string): Promise<SeriesSearchResult[]> => {
        return new Promise(resolve => {
            setTimeout(() => {
                if (!query) {
                    resolve([]);
                    return;
                }
                const q = query.toLowerCase();
                // Mock database of series
                const allSeries: SeriesSearchResult[] = [
                    { seriesId: "one-piece", title: "One Piece", seedCount: 1542, chapterCount: 1110, lastUploadedAt: new Date().toISOString(), source: 0, externalMangaId: "mock-op", latestChapterNumber: 1110, latestChapterTitle: "The Star" },
                    { seriesId: "jujutsu-kaisen", title: "Jujutsu Kaisen", seedCount: 890, chapterCount: 250, lastUploadedAt: new Date(Date.now() - 86400000).toISOString(), source: 0, externalMangaId: "mock-jjk", latestChapterNumber: 250, latestChapterTitle: "Inhuman Makyo Shinjuku Showdown, Part 22" },
                    { seriesId: "chainsaw-man", title: "Chainsaw Man", seedCount: 1200, chapterCount: 160, lastUploadedAt: new Date(Date.now() - 3600000).toISOString(), source: 0, externalMangaId: "mock-csm", latestChapterNumber: 160, latestChapterTitle: "That's a Chainsaw" },
                    { seriesId: "sakamoto-days", title: "Sakamoto Days", seedCount: 450, chapterCount: 160, lastUploadedAt: new Date(Date.now() - 7200000).toISOString(), source: 0, externalMangaId: "mock-sd", latestChapterNumber: 160, latestChapterTitle: "Hard Boiled" },
                    { seriesId: "dandadan", title: "Dandadan", seedCount: 300, chapterCount: 140, lastUploadedAt: new Date(Date.now() - 172800000).toISOString(), source: 0, externalMangaId: "mock-dd", latestChapterNumber: 140, latestChapterTitle: "Aliens vs Yokai" },
                    { seriesId: "spy-x-family", title: "Spy x Family", seedCount: 800, chapterCount: 95, lastUploadedAt: new Date(Date.now() - 604800000).toISOString(), source: 0, externalMangaId: "mock-spy", latestChapterNumber: 95, latestChapterTitle: "Mission 95" },
                    { seriesId: "berserk", title: "Berserk", seedCount: 2000, chapterCount: 375, lastUploadedAt: new Date(Date.now() - 2592000000).toISOString(), source: 0, externalMangaId: "mock-berserk", latestChapterNumber: 375, latestChapterTitle: "Eclipse" },
                ];

                const results = allSeries.filter(s =>
                    s.seriesId.toLowerCase().includes(q) || s.title.toLowerCase().includes(q)
                );
                resolve(results);
            }, 300); // Fast mock search
        });
    },

    getImportedChapters: async (): Promise<ImportedChapter[]> => {
        return new Promise(resolve => {
            setTimeout(() => {
                const imported: ImportedChapter[] = [
                    {
                        seriesId: "one-piece",
                        scanlatorId: "tcb-scans",
                        language: "en",
                        chapterNumber: 1110,
                        sourcePath: "C:\\Users\\cameron\\Documents\\mangamesh-test-chapters\\one-piece\\chapter-1110",
                        displayName: "One Piece 1110",
                        releaseType: "manual"
                    },
                    {
                        seriesId: "jujutsu-kaisen",
                        scanlatorId: "tcb-scans",
                        language: "en",
                        chapterNumber: 255,
                        sourcePath: "C:\\Users\\cameron\\Documents\\mangamesh-test-chapters\\jjk\\chapter-255",
                        displayName: "JJK 255",
                        releaseType: "auto"
                    },
                    {
                        seriesId: "chainsaw-man",
                        scanlatorId: "viz",
                        language: "en",
                        chapterNumber: 165,
                        sourcePath: "C:\\Users\\cameron\\Documents\\mangamesh-test-chapters\\csm\\chapter-165",
                        displayName: "CSM 165",
                        releaseType: "manual"
                    }
                ];
                resolve(imported);
            }, 500);
        });
    },

    // Keys
    getKeys: async (): Promise<import('../types/api').KeyPair> => {
        const stored = localStorage.getItem('mock_keys');
        if (stored) return Promise.resolve(JSON.parse(stored));
        return Promise.resolve({ publicKeyBase64: 'mock-public-key-not-generated-yet' });
    },

    generateKeys: async (): Promise<import('../types/api').KeyPair> => {
        const newKeys = {
            publicKeyBase64: 'start1' + Math.random().toString(36).substring(7) + 'end',
            privateKeyBase64: 'priv_' + Math.random().toString(36).substring(2) + Math.random().toString(36).substring(2)
        };
        localStorage.setItem('mock_keys', JSON.stringify({ publicKeyBase64: newKeys.publicKeyBase64 })); // Don't store private
        return Promise.resolve(newKeys);
    }
};
