import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { getSeriesChapters, getSeriesDetails, getChapterDetails } from '../api/series';
import { getSubscriptions, subscribe, unsubscribe } from '../api/subscriptions';
import type { ChapterSummaryResponse, Subscription, SeriesDetailsResponse, ChapterManifest } from '../types/api';

export default function SeriesDetails() {
    const { seriesId } = useParams<{ seriesId: string }>();
    const [chapters, setChapters] = useState<ChapterSummaryResponse[]>([]);
    const [chapterManifests, setChapterManifests] = useState<Record<string, ChapterManifest[]>>({});
    const [seriesInfo, setSeriesInfo] = useState<SeriesDetailsResponse | null>(null);
    const [subscription, setSubscription] = useState<Subscription | null>(null);
    const [loading, setLoading] = useState(true);
    const [manifestsLoading, setManifestsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        async function load() {
            if (!seriesId) return;
            try {
                const [chapterData, subs, details] = await Promise.all([
                    getSeriesChapters(seriesId),
                    getSubscriptions(),
                    getSeriesDetails(seriesId)
                ]);
                const sortedChapters = (chapterData || []).sort((a, b) => {
                    const numA = parseFloat(a.chapterNumber) || 0;
                    const numB = parseFloat(b.chapterNumber) || 0;
                    return numB - numA;
                });
                setChapters(sortedChapters);
                setSeriesInfo(details);
                const sub = subs.find(s => s.seriesId === seriesId);
                setSubscription(sub || null);

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
            } catch (e) {
                setError('Failed to load series data');
            } finally {
                setLoading(false);
            }
        }
        load();
    }, [seriesId]);



    async function handleSubscribeToggle() {
        if (!seriesId) return;
        try {
            if (subscription) {
                if (confirm('Are you sure you want to unsubscribe?')) {
                    await unsubscribe(seriesId);
                    setSubscription(null);
                }
            } else {
                await subscribe(seriesId);
                // Refresh subscription state
                const subs = await getSubscriptions();
                setSubscription(subs.find(s => s.seriesId === seriesId) || null);
            }
        } catch (e) {
            alert('Failed to update subscription');
        }
    }

    if (loading) return <div className="p-8 text-gray-500">Loading chapters...</div>;
    if (error) return <div className="p-8 text-red-500">{error}</div>;



    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-gray-900">{seriesInfo?.title || seriesId}</h1>
                    <div className="text-sm text-gray-500 mt-1 flex gap-3">
                        <span>{chapters.length} chapters found</span>
                        {seriesInfo?.status && <span>•</span>}
                        {seriesInfo?.status && <span>{seriesInfo.status}</span>}
                    </div>
                </div>
                <div className="flex items-center gap-4">
                    <button
                        onClick={handleSubscribeToggle}
                        className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${subscription
                            ? 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                            : 'bg-blue-600 text-white hover:bg-blue-700 shadow-sm'
                            }`}
                    >
                        {subscription ? 'Subscribed' : 'Subscribe'}
                    </button>
                    <Link to="/subscriptions" className="text-blue-600 hover:underline text-sm">
                        Back to Subscriptions
                    </Link>
                </div>
            </div>



            <div className="space-y-4">
                {chapters.length === 0 ? (
                    <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-8 text-center text-gray-500">No chapters found.</div>
                ) : (
                    chapters.map((chapter) => {
                        const manifests = chapterManifests[chapter.chapterId] || [];

                        return (
                            <div key={chapter.chapterId} className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
                                <div className="px-4 py-2 bg-gray-50 border-b border-gray-100 flex items-center justify-between">
                                    <h3 className="text-sm font-bold text-gray-700">
                                        Chapter {chapter.chapterNumber}
                                        {chapter.title && <span className="ml-2 font-normal text-gray-500">- {chapter.title}</span>}
                                    </h3>
                                    {chapter.volume && <span className="text-[10px] font-medium text-gray-400 uppercase">Vol. {chapter.volume}</span>}
                                </div>

                                <div className="divide-y divide-gray-50">
                                    {manifestsLoading && manifests.length === 0 ? (
                                        <div className="p-4 text-sm text-gray-400 italic">
                                            Loading available versions...
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

                                            // Temporary Mock: Mark 'opscan' as verified for demonstration
                                            if (mScanGroup?.toLowerCase().includes('opscan')) {
                                                mIsVerified = true;
                                            }

                                            const mUploadedAt = manifest.uploadedAt || (manifest as any).UploadedAt;

                                            return (
                                                <Link
                                                    key={mHash}
                                                    to={`/series/${seriesId}/read/${chapter.chapterId}?manifest=${mHash}`}
                                                    className="block p-4 hover:bg-gray-50 transition-colors flex justify-between items-center group"
                                                >
                                                    <div className="flex-1 min-w-0">
                                                        <div className="flex items-center gap-3">
                                                            <span className="bg-blue-100 text-blue-700 px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider">
                                                                {mLang}
                                                            </span>
                                                            <span className="bg-gray-100 text-gray-600 px-2 py-0.5 rounded text-[10px] font-medium uppercase tracking-wider">
                                                                {mQuality}
                                                            </span>
                                                            {mScanGroup && (
                                                                <div className="flex items-center gap-1 min-w-0">
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
                                                        <div className="text-[11px] text-gray-500 mt-1 flex items-center gap-2">
                                                            <span>Uploaded {new Date(mUploadedAt).toLocaleDateString()}</span>
                                                        </div>
                                                    </div>
                                                    <div className="flex items-center gap-4 ml-4">
                                                        <span className="text-gray-400 text-[10px] font-mono opacity-0 group-hover:opacity-100 transition-opacity">
                                                            {mHash?.substring(0, 8)}
                                                        </span>
                                                        <svg className="w-4 h-4 text-gray-300 group-hover:text-blue-400 transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                                                        </svg>
                                                    </div>
                                                </Link>
                                            );
                                        })
                                    )}
                                </div>
                            </div>
                        );
                    })
                )}
            </div>
        </div>
    );
}
