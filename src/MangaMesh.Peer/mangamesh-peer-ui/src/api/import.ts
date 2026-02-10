import type { ImportChapterRequest, ImportedChapter, AnalyzedChapterDto, ImportChapterResult } from '../types/api';


export async function uploadChapters(files: FormData): Promise<AnalyzedChapterDto[]> {
    const response = await fetch('/api/import/upload', {
        method: 'POST',
        body: files
    });

    if (!response.ok) {
        throw new Error(`Upload failed: ${response.statusText}`);
    }

    return await response.json();
}


export async function importChapter(request: ImportChapterRequest): Promise<ImportChapterResult> {
    const response = await fetch('/api/import/chapter', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(request),
    });

    if (!response.ok) {
        let message = response.statusText;
        try {
            const error = await response.json();
            if (error.message) message = error.message;
        } catch { }
        throw new Error(message);
    }

    return await response.json();
}

export async function getImportedChapters(): Promise<ImportedChapter[]> {
    const response = await fetch('/api/import/chapters');

    if (!response.ok) {
        throw new Error(`Failed to fetch history: ${response.statusText}`);
    }

    return await response.json();
}
