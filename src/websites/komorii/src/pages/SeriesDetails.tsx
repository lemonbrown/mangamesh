import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { getSeriesChapters, getSeriesDetails, getChapterDetails } from '../api/series';
// import { getSubscriptions, subscribe, unsubscribe } from '../api/subscriptions'; // Not porting subscriptions yet? Or maybe stub it.
import type { ChapterSummaryResponse, SeriesDetailsResponse, ChapterManifest } from '../types/api';

export default function SeriesDetails() {
    const { seriesId } = useParams<{ seriesId: string }>();
    const [chapters, setChapters] = useState<ChapterSummaryResponse[]>([]);
    const [chapterManifests, setChapterManifests] = useState<Record<string, ChapterManifest[]>>({});
    const [seriesInfo, setSeriesInfo] = useState<SeriesDetailsResponse | null>(null);
    const [loading, setLoading] = useState(true);
    const [manifestsLoading, setManifestsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        async function load() {
            if (!seriesId) return;
            try {
                // Not getting subscriptions in web view for now as it requires auth/local state
                const [chapterData, details] = await Promise.all([
                    getSeriesChapters(seriesId),
                    getSeriesDetails(seriesId)
                ]);
                const sortedChapters = (chapterData || []).sort((a, b) => {
                    const numA = parseFloat(a.chapterNumber) || 0;
                    const numB = parseFloat(b.chapterNumber) || 0;
                    return numB - numA;
                });
                setChapters(sortedChapters);
                setSeriesInfo(details);

                // Fetch manifests for each chapter
                if (chapterData.length > 0) {
                    setManifestsLoading(true);

                    const detailsResults = await Promise.allSettled(
                        chapterData.map(ch => getChapterDetails(seriesId, ch.chapterId))
                    );

                    const manifestsMap: Record<string, ChapterManifest[]> = {};
                    const titleUpdates: Record<string, string> = {};

                    detailsResults.forEach((result, index) => {
                        if (result.status === 'fulfilled') {
                            const chapterId = chapterData[index].chapterId;
                            const val = result.value;
                            manifestsMap[chapterId] = val.manifests || (val as any).Manifests || [];

                            const title = val.title || (val as any).Title;
                            if (title) {
                                titleUpdates[chapterId] = title;
                            }
                        }
                    });

                    setChapterManifests(manifestsMap);

                    if (Object.keys(titleUpdates).length > 0) {
                        setChapters(prev => prev.map(ch => ({
                            ...ch,
                            title: titleUpdates[ch.chapterId] || ch.title
                        })));
                    }

                    setManifestsLoading(false);
                }
            } catch (r) {
                console.error(r);
                setError('Failed to load series data');
            } finally {
                setLoading(false);
            }
        }
        load();
    }, [seriesId]);

    if (loading) return (
        <div className="flex flex-col items-center justify-center min-h-[50vh]">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mb-4"></div>
            <div className="text-gray-500">Loading series details...</div>
        </div>
    );


    if (error) return (
        <div className="p-8 text-center">
            <div className="text-red-500 text-xl font-semibold mb-2">Error</div>
            <div className="text-gray-600">{error}</div>
            <Link to="/" className="mt-4 inline-block text-blue-600 hover:underline">Return Home</Link>
        </div>
    );

    return (
        <div className="space-y-8 animate-fade-in">
            <div className="bg-white p-6 rounded-2xl shadow-sm border border-gray-100">
                <div className="flex flex-col md:flex-row md:items-start gap-6">
                    {/* Cover Image */}
                    <div className="w-32 h-48 bg-gray-100 rounded-lg overflow-hidden shrink-0 border border-gray-200 shadow-sm mx-auto md:mx-0">
                        {seriesInfo?.externalMangaId ? (
                            <img
                                src={`/covers/${seriesInfo.externalMangaId}.thumb.webp`}
                                alt={seriesInfo.title}
                                className="w-full h-full object-cover"
                                onError={(e) => {
                                    (e.target as HTMLImageElement).src = 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIGZpbGw9Im5vbmUiIHZpZXdCb3g9IjAgMCAyNCAyNCIgc3Ryb2tlPSIjOWNhM2FmIj48cGF0aCBzdHJva2UtbGluZWNhcD0icm91bmQiIHN0cm9rZS1saW5lam9pbj0icm91bmQiIHN0cm9rZS13aWR0aD0iMSIgZD0iTTQgMTZsNC41ODYtNC41ODZhMiAyIDAgMDEyLjgyOCAwTTE2IDE2bS0yLTJsMS41ODYtMS41ODZhMiAyIDAgMDEyLjgyOCAwTDIwIDE0bS02LTZoLjAxTTYgMjBoMTJhMiAyIDAgMDAyLTJWNmEyIDAgMDAtMi0ySDZhMiAyIDAgMDAtMiAydjEyYTIgMiAwIDAwMiAyeiIgLz48L3N2Zz4=';
                                }}
                            />
                        ) : (
                            <div className="w-full h-full flex items-center justify-center text-gray-400">
                                <svg className="w-10 h-10" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1} d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
                                </svg>
                            </div>
                        )}
                    </div>

                    <div className="flex-1">
                        <div className="flex items-center gap-3 mb-1">
                            <h1 className="text-3xl font-bold text-gray-900">{seriesInfo?.title || seriesId}</h1>
                            {seriesInfo?.status && (
                                <span className={`px-2 py-0.5 rounded text-xs font-bold uppercase tracking-wide ${seriesInfo.status.toLowerCase() === 'ongoing' ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600'
                                    }`}>
                                    {seriesInfo.status}
                                </span>
                            )}
                        </div>
                        <div className="text-sm text-gray-500 flex flex-wrap gap-x-4 gap-y-2">
                            <span>Year: {seriesInfo?.year || 'Unknown'}</span>
                            <span>•</span>
                            <span>{chapters.length} chapters</span>
                        </div>
                        {seriesInfo?.description && (
                            <p className="mt-4 text-gray-700 leading-relaxed max-w-3xl">
                                {seriesInfo.description}
                            </p>
                        )}
                    </div>
                </div>
            </div>

            <div className="space-y-4">
                <h2 className="text-xl font-bold text-gray-900 px-1">Chapters</h2>

                {chapters.length === 0 ? (
                    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-12 text-center text-gray-500">
                        No chapters found for this series.
                    </div>
                ) : (
                    <div className="grid gap-4">
                        {chapters.map((chapter) => {
                            const manifests = chapterManifests[chapter.chapterId] || [];

                            return (
                                <div key={chapter.chapterId} className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden hover:border-blue-200 transition-colors">
                                    <div className="px-5 py-3 bg-gray-50 border-b border-gray-100 flex items-center justify-between">
                                        <h3 className="text-base font-bold text-gray-800">
                                            Chapter {chapter.chapterNumber}
                                            {chapter.title && <span className="ml-2 font-normal text-gray-500">- {chapter.title}</span>}
                                        </h3>
                                        {chapter.volume && <span className="text-xs font-bold text-gray-400 uppercase tracking-widest">Vol. {chapter.volume}</span>}
                                    </div>

                                    <div className="divide-y divide-gray-50">
                                        {manifestsLoading && manifests.length === 0 ? (
                                            <div className="p-4 text-sm text-gray-400 italic flex items-center gap-2">
                                                <div className="animate-spin h-3 w-3 border-b-2 border-gray-400 rounded-full"></div>
                                                Loading versions...
                                            </div>
                                        ) : manifests.length === 0 ? (
                                            <div className="p-4 text-sm text-gray-400 italic">
                                                No versions available.
                                            </div>
                                        ) : (
                                            manifests.map((manifest) => {
                                                const mHash = manifest.manifestHash || (manifest as any).ManifestHash;
                                                const mLang = manifest.language || (manifest as any).Language;
                                                const mQuality = manifest.quality || (manifest as any).Quality;
                                                const mScanGroup = manifest.scanGroup || (manifest as any).ScanGroup;
                                                let mIsVerified = manifest.isVerified !== undefined ? manifest.isVerified : (manifest as any).IsVerified;
                                                // Temporary Mock from original code
                                                if (mScanGroup?.toLowerCase().includes('opscan')) {
                                                    mIsVerified = true;
                                                }

                                                const mUploadedAt = manifest.uploadedAt || (manifest as any).UploadedAt;

                                                return (
                                                    <Link
                                                        key={mHash}
                                                        to={`/series/${seriesId}/read/${chapter.chapterId}?manifest=${mHash}`}
                                                        className="block p-4 hover:bg-blue-50/50 transition-colors flex justify-between items-center group relative overflow-hidden"
                                                    >
                                                        {/* Hover highlight bar */}
                                                        <div className="absolute left-0 top-0 bottom-0 w-1 bg-blue-500 opacity-0 group-hover:opacity-100 transition-opacity"></div>

                                                        <div className="flex-1 min-w-0 pl-2">
                                                            <div className="flex items-center flex-wrap gap-2">
                                                                <span className="bg-blue-100 text-blue-700 px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider">
                                                                    {mLang}
                                                                </span>
                                                                <span className="bg-gray-100 text-gray-600 px-2 py-0.5 rounded text-[10px] font-medium uppercase tracking-wider">
                                                                    {mQuality}
                                                                </span>
                                                                {mScanGroup && (
                                                                    <div className="flex items-center gap-1 min-w-0 ml-1">
                                                                        <span className="text-sm font-medium text-gray-700 truncate max-w-[200px]">
                                                                            {mScanGroup}
                                                                        </span>
                                                                        {mIsVerified && (
                                                                            <svg className="w-3.5 h-3.5 text-blue-500 fill-current shrink-0" viewBox="0 0 20 20">
                                                                                <path d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" />
                                                                            </svg>
                                                                        )}
                                                                    </div>
                                                                )}
                                                            </div>
                                                            <div className="text-[11px] text-gray-400 mt-1 flex items-center gap-2">
                                                                <span>Uploaded {new Date(mUploadedAt).toLocaleDateString()}</span>
                                                            </div>
                                                        </div>
                                                        <div className="flex items-center gap-4 ml-4">
                                                            <span className="text-gray-300 text-[10px] font-mono opacity-0 group-hover:opacity-100 transition-opacity">
                                                                {mHash?.substring(0, 8)}
                                                            </span>
                                                            <div className="h-8 w-8 rounded-full bg-gray-100 group-hover:bg-blue-100 flex items-center justify-center transition-colors">
                                                                <svg className="w-4 h-4 text-gray-400 group-hover:text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                                                                </svg>
                                                            </div>
                                                        </div>
                                                    </Link>
                                                );
                                            })
                                        )}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                )}
            </div>
        </div>
    );
}
