import { useEffect, useState } from 'react';
import { getPageImage } from '../api/chapters';

interface PageImageProps {
    manifestHash: string;
    pageIndex: number;
    preload?: boolean; // If true, fetches but doesn't render visible (handled by browser cache/blob?)
    // Actually, for blobs we need to manage object URLs.
}

export default function PageImage({ manifestHash, pageIndex }: PageImageProps) {
    const [imageUrl, setImageUrl] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(false);

    useEffect(() => {
        let active = true;
        setLoading(true);
        setError(false);

        getPageImage(manifestHash, pageIndex)
            .then(blob => {
                if (!active) return;
                const url = URL.createObjectURL(blob);
                setImageUrl(url);
                setLoading(false);
            })
            .catch(() => {
                if (active) {
                    setError(true);
                    setLoading(false);
                }
            });

        return () => {
            active = false;
            if (imageUrl) {
                URL.revokeObjectURL(imageUrl);
            }
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [manifestHash, pageIndex]);

    if (loading) {
        return (
            <div className="w-full h-96 bg-gray-100 flex items-center justify-center text-gray-400 animate-pulse">
                Loading Page {pageIndex + 1}...
            </div>
        );
    }

    if (error || !imageUrl) {
        return (
            <div className="w-full h-96 bg-red-50 flex items-center justify-center text-red-500">
                Failed to load page {pageIndex + 1}
            </div>
        );
    }

    return (
        <img
            src={imageUrl}
            alt={`Page ${pageIndex + 1}`}
            className="max-w-full h-auto mx-auto shadow-sm"
        />
    );
}
