import { useEffect, useState } from 'react';
import { useParams, Link, useSearchParams } from 'react-router-dom';
import { readChapter } from '../api/series';
import type { FullChapterManifest } from '../types/api';

export default function Reader() {
    const { seriesId, chapterId } = useParams<{ seriesId: string, chapterId: string }>();
    const [searchParams] = useSearchParams();
    const manifestHash = searchParams.get('manifest');

    const [manifest, setManifest] = useState<FullChapterManifest | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        async function load() {
            if (!seriesId || !chapterId || !manifestHash) {
                setError('Missing required parameters (seriesId, chapterId, or manifest hash)');
                setLoading(false);
                return;
            }

            try {
                // This call ensures content is synced locally via P2P
                const data = await readChapter(seriesId, chapterId, manifestHash);
                setManifest(data);
            } catch (e) {
                console.error(e);
                setError('Failed to load chapter content. Ensure peers are online.');
            } finally {
                setLoading(false);
            }
        }
        load();
    }, [seriesId, chapterId, manifestHash]);

    if (loading) return (
        <div className="flex flex-col items-center justify-center min-h-screen bg-gray-100">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mb-4"></div>
            <div className="text-gray-600 font-medium">Syncing chapter from peers...</div>
        </div>
    );

    if (error) return (
        <div className="min-h-screen bg-gray-100 flex items-center justify-center p-4">
            <div className="bg-white p-8 rounded-lg shadow-md max-w-md w-full text-center">
                <div className="text-red-500 mb-4">
                    <svg className="w-12 h-12 mx-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                    </svg>
                </div>
                <h2 className="text-xl font-bold text-gray-900 mb-2">Error Loading Chapter</h2>
                <p className="text-gray-600 mb-6">{error}</p>
                <Link to={`/series/${seriesId}`} className="inline-block bg-blue-600 text-white px-6 py-2 rounded-md hover:bg-blue-700 transition-colors">
                    Back to Series
                </Link>
            </div>
        </div>
    );

    if (!manifest) return null;

    return (
        <div className="bg-black min-h-screen pb-20">
            {/* Sticky Header */}
            <div className="sticky top-0 z-10 bg-gray-900/90 border-b border-gray-800 px-4 py-3 flex items-center justify-between shadow-lg backdrop-blur-sm transition-opacity hover:opacity-100 opacity-0 md:opacity-100">
                <div>
                    <h1 className="font-bold text-white text-lg">
                        Chapter {manifest.chapterNumber}
                    </h1>
                    <div className="text-xs text-gray-400 font-mono">
                        {manifestHash?.substring(0, 8)} • {manifest.files.length} pages
                    </div>
                </div>

                <div className="flex space-x-4">
                    <Link
                        to={`/series/${seriesId}`}
                        className="text-sm font-medium text-gray-300 hover:text-white transition-colors"
                    >
                        Close
                    </Link>
                </div>
            </div>

            {/* Pages Container */}
            <div className="max-w-3xl mx-auto space-y-2 py-4">
                {manifest.files.map((file, index) => (
                    <div key={file.hash} className="relative bg-gray-900 min-h-[50vh] flex items-center justify-center">
                        {/* We use /api/blob/{hash} to serve images locally from the blob store */}
                        <img
                            src={`/api/blob/${file.hash}`}
                            alt={`Page ${index + 1}`}
                            className="w-full h-auto shadow-2xl"
                            loading="lazy"
                            onError={(e) => {
                                (e.target as HTMLImageElement).style.display = 'none';
                                (e.target as HTMLImageElement).parentElement!.innerHTML = `
                                    <div class="text-gray-500 p-8 text-center flex flex-col items-center">
                                        <span class="text-4xl mb-2">⚠️</span>
                                        <span>Failed to load page ${index + 1}</span>
                                    </div>
                                `;
                            }}
                        />
                    </div>
                ))}
            </div>

            {/* Footer Navigation Hints */}
            <div className="max-w-3xl mx-auto mt-8 px-4 text-center">
                <div className="inline-flex items-center gap-4 bg-gray-800 rounded-full px-6 py-3 shadow-lg">
                    <Link to={`/series/${seriesId}`} className="text-gray-300 hover:text-white transition-colors text-sm font-medium">
                        Return to Series
                    </Link>
                </div>
            </div>
        </div>
    );
}
