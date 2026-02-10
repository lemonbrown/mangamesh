import { useState, useEffect, useRef } from 'react';
import { importChapter, getImportedChapters, uploadChapters } from '../api/import';
import { getKeys, requestChallenge, solveChallenge, verifySignature } from '../api/keys';
import { searchMetadata } from '../api/series'; // Changed import
import type { ImportChapterRequest, KeyPair, AnalyzedChapterDto, SeriesSearchResult } from '../types/api';

export default function ImportChapter() {
    // ... existing ...

    // Series Search State
    const [menuTitle, setMenuTitle] = useState('');
    const [seriesSearchResults, setSeriesSearchResults] = useState<SeriesSearchResult[]>([]);
    const [showSeriesDropdown, setShowSeriesDropdown] = useState(false);
    const [isSearchingSeries, setIsSearchingSeries] = useState(false);

    const isSelectionUpdate = useRef(false);

    useEffect(() => {
        const timer = setTimeout(async () => {
            if (isSelectionUpdate.current) {
                isSelectionUpdate.current = false;
                return;
            }

            if (menuTitle.length >= 3) {
                setIsSearchingSeries(true);
                try {
                    const results = await searchMetadata(menuTitle); // Use new API
                    // Map metadata to SeriesSearchResult format
                    const mappedResults: SeriesSearchResult[] = results.map(r => ({
                        seriesId: '', // Metadata search results don't have a seriesId yet
                        title: r.title,
                        source: r.source,
                        externalMangaId: r.externalMangaId,
                        year: r.year
                    }));
                    setSeriesSearchResults(mappedResults);
                    setShowSeriesDropdown(true);
                } catch (e) {
                    console.error(e);
                } finally {
                    setIsSearchingSeries(false);
                }
            } else {
                setSeriesSearchResults([]);
                setShowSeriesDropdown(false);
            }
        }, 500);
        return () => clearTimeout(timer);
    }, [menuTitle]);

    // ... existing ...
    const [chapterInputValue, setChapterInputValue] = useState('');

    const handleChapterChange = (value: string) => {
        setChapterInputValue(value);
        const num = parseFloat(value);
        if (!isNaN(num)) {
            setForm({ ...form, chapterNumber: num });
        }
    };

    const [form, setForm] = useState<ImportChapterRequest>({
        seriesId: '',
        scanlatorId: '',
        language: '',
        chapterNumber: 0,
        sourcePath: '',
        displayName: '',
        releaseType: 'manual',
        source: 0,
        externalMangaId: ''
    });
    const [submitting, setSubmitting] = useState(false);
    const [message, setMessage] = useState<{ type: 'success' | 'error', text: string } | null>(null);
    const [history, setHistory] = useState<import('../types/api').ImportedChapter[]>([]);
    const [search, setSearch] = useState('');

    // Signature verification state
    const [keys, setKeys] = useState<KeyPair | null>(null);
    const [privateKeyInput, setPrivateKeyInput] = useState('');
    const [isVerifying, setIsVerifying] = useState(false);
    const [signatureStatus, setSignatureStatus] = useState<'idle' | 'success' | 'error'>('idle');
    const [verificationError, setVerificationError] = useState<string | null>(null);
    const hasAutoVerified = useRef(false);

    // Upload/Batch state
    const [uploadBatch, setUploadBatch] = useState<AnalyzedChapterDto[]>([]);
    const [isUploading, setIsUploading] = useState(false);
    const [batchReviewMode, setBatchReviewMode] = useState(false);

    useEffect(() => {
        loadHistory();
        loadKeys();
    }, []);

    async function loadKeys() {
        try {
            const k = await getKeys();
            setKeys(k);
            if (k.privateKeyBase64 && k.publicKeyBase64 && !hasAutoVerified.current) {
                setPrivateKeyInput(k.privateKeyBase64);
                hasAutoVerified.current = true;
                handleVerifySignature(k.publicKeyBase64, k.privateKeyBase64);
            }
        } catch (e) {
            console.error('Failed to load keys', e);
        }
    }

    async function loadHistory() {
        try {
            const data = await getImportedChapters();
            setHistory(data);
        } catch (e) {
            console.error('Failed to load history', e);
        }
    }

    async function handleSubmit(e: React.FormEvent) {
        e.preventDefault();

        if (signatureStatus !== 'success') {
            setMessage({ type: 'error', text: 'Please verify your signature before importing' });
            return;
        }

        setSubmitting(true);
        setMessage(null);

        try {
            if (batchReviewMode) {
                // Batch import
                let importedCount = 0;
                let ignoredCount = 0;
                for (const item of uploadBatch) {
                    try {
                        const result = await importChapter({
                            ...form,
                            chapterNumber: item.suggestedChapterNumber,
                            sourcePath: item.sourcePath,
                            displayName: form.displayName || `${form.externalMangaId} Ch. ${item.suggestedChapterNumber}`,
                            releaseType: form.releaseType || 'manual'
                        });

                        if (result.alreadyExists) {
                            ignoredCount++;
                        } else {
                            importedCount++;
                        }
                    } catch (e: any) {
                        const msg = e.message || '';
                        if (msg.includes('Manifest already exists')) {
                            ignoredCount++;
                        } else {
                            console.error(`Failed to import ${item.sourcePath}`, e);
                        }
                    }
                }

                if (ignoredCount > 0) {
                    setMessage({ type: 'success', text: `Completed: ${importedCount} imported, ${ignoredCount} skipped (duplicates).` });
                } else {
                    setMessage({ type: 'success', text: `Successfully imported ${importedCount} chapters.` });
                }

                setBatchReviewMode(false);
                setUploadBatch([]);
            } else {
                // Single legacy import
                try {
                    const result = await importChapter({
                        ...form,
                        displayName: form.displayName || `${form.externalMangaId} Ch. ${form.chapterNumber}`,
                        releaseType: form.releaseType || 'manual'
                    });

                    if (result.alreadyExists) {
                        setMessage({ type: 'error', text: 'This chapter has already been imported (Manifest exists).' });
                    } else {
                        setMessage({ type: 'success', text: 'Chapter imported successfully' });
                        setForm(prev => ({ ...prev, chapterNumber: prev.chapterNumber + 1 }));
                        setChapterInputValue((form.chapterNumber + 1).toString());
                    }
                } catch (e: any) {
                    const msg = e.message || '';
                    if (msg.includes('Manifest already exists')) {
                        setMessage({ type: 'error', text: 'This chapter has already been imported (Manifest exists).' });
                    } else {
                        throw e; // Re-throw to be caught by outer catch
                    }
                }
                loadHistory();
            }

            loadHistory();
        } catch (e: any) {
            console.error(e);
            setMessage({ type: 'error', text: e.message || 'Failed to import chapters' });
        } finally {
            setSubmitting(false);
        }
    }

    const [isDragging, setIsDragging] = useState(false);

    // Recursively traverse FileSystemEntry to get all files with paths
    async function traverseFileTree(item: any, path: string = ''): Promise<{ file: File, path: string }[]> {
        if (item.isFile) {
            return new Promise(resolve => {
                item.file((file: File) => {
                    resolve([{ file, path: path + file.name }]);
                });
            });
        } else if (item.isDirectory) {
            const dirReader = item.createReader();
            const entries: any[] = [];

            const readEntries = async () => {
                const result = await new Promise<any[]>((resolve) => dirReader.readEntries(resolve));
                if (result.length > 0) {
                    entries.push(...result);
                    await readEntries();
                }
            };

            await readEntries();

            const promises = entries.map(entry => traverseFileTree(entry, path + item.name + "/"));
            const results = await Promise.all(promises);
            return results.flat();
        }
        return [];
    }

    const handleFiles = async (fileList: File[], fromDrop = false, items?: DataTransferItemList) => {
        setIsUploading(true);
        const formData = new FormData();

        try {
            if (fromDrop && items) {
                // Handle Drag & Drop with folder traversal
                const promises: Promise<{ file: File, path: string }[]>[] = [];
                for (let i = 0; i < items.length; i++) {
                    const item = items[i].webkitGetAsEntry?.();
                    if (item) {
                        promises.push(traverseFileTree(item));
                    }
                }
                const filesWithPath = (await Promise.all(promises)).flat();

                if (filesWithPath.length === 0) {
                    setMessage({ type: 'error', text: 'No files found in dropped items.' });
                    setIsUploading(false);
                    return;
                }

                filesWithPath.forEach(({ file, path }) => {
                    // Pass the full relative path as the filename
                    formData.append('files', file, path);
                });

            } else {
                // Handle Input Selection
                fileList.forEach(file => {
                    // webkitRelativePath is available for input webkitdirectory
                    formData.append('files', file, file.webkitRelativePath || file.name);
                });
            }

            const batch = await uploadChapters(formData);
            if (batch.length > 0) {
                setUploadBatch(batch);
                setBatchReviewMode(true);
            } else {
                setMessage({ type: 'error', text: 'No chapters detected in the uploaded folder.' });
            }
        } catch (e) {
            console.error(e);
            setMessage({ type: 'error', text: 'Failed to upload files.' });
        } finally {
            setIsUploading(false);
            setIsDragging(false);
        }
    };

    const handleFolderSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
        if (e.target.files?.length) {
            handleFiles(Array.from(e.target.files));
        }
        e.target.value = '';
    };

    const onDragOver = (e: React.DragEvent) => {
        e.preventDefault();
        setIsDragging(true);
    };

    const onDragLeave = (e: React.DragEvent) => {
        e.preventDefault();
        setIsDragging(false);
    };

    const onDrop = async (e: React.DragEvent) => {
        e.preventDefault();
        e.stopPropagation();
        setIsDragging(false);

        const items = e.dataTransfer.items;
        if (items && items.length > 0) {
            await handleFiles([], true, items);
        } else if (e.dataTransfer.files.length > 0) {
            await handleFiles(Array.from(e.dataTransfer.files));
        }
    };

    async function handleVerifySignature(pubKey?: string, privKey?: string) {
        const publicToUse = pubKey || keys?.publicKeyBase64;
        const privateToUse = privKey || privateKeyInput;

        if (!publicToUse) {
            setVerificationError('No public key found for this node');
            setSignatureStatus('error');
            return;
        }

        if (!privateToUse) {
            setVerificationError('Please enter your private key');
            setSignatureStatus('error');
            return;
        }

        setIsVerifying(true);
        setVerificationError(null);
        setSignatureStatus('idle');

        try {
            // Step 1: Request Challenge
            const challenge = await requestChallenge(publicToUse);

            // Step 2: Solve Challenge
            const signature = await solveChallenge(challenge.nonce, privateToUse);

            // Step 3: Verify Signature
            const result = await verifySignature(publicToUse, challenge.challengeId, signature);

            if (result.valid) {
                setSignatureStatus('success');
            } else {
                setSignatureStatus('error');
                setVerificationError('Signature verification failed');
            }
        } catch (e: any) {
            console.error('Signature verification error', e);
            setSignatureStatus('error');
            setVerificationError(e.message || 'Verification failed');
        } finally {
            setIsVerifying(false);
        }
    }

    const filteredHistory = history.filter(item =>
        item.seriesId.toLowerCase().includes(search.toLowerCase()) ||
        item.displayName.toLowerCase().includes(search.toLowerCase())
    );

    return (
        <div className="max-w-4xl mx-auto space-y-8">
            <div>
                <h1 className="text-2xl font-bold text-gray-900 mb-6">Import Chapter</h1>

                {signatureStatus !== 'success' ? (
                    <div className="bg-white p-8 rounded-lg shadow-sm border border-gray-200">
                        <div className="mb-8 p-6 bg-blue-50 rounded-lg border border-blue-100">
                            <h2 className="text-lg font-semibold text-blue-900 mb-2">Verification Required</h2>
                            <p className="text-blue-800">
                                Importing on the MangaMesh network is currently restricted to approved signatures.
                                Please apply the <a href="https://discord.gg/mangamesh" className="underline font-bold" target="_blank" rel="noopener noreferrer">MangaMesh Discord</a> for access.
                            </p>
                        </div>

                        <div className="space-y-6">
                            <div>
                                <label className="block text-sm font-medium text-gray-700 mb-2">Enter Private Key (Base64)</label>
                                <div className="flex gap-3">
                                    <input
                                        type="password"
                                        className="flex-1 px-4 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 font-mono text-sm"
                                        placeholder="Paste your node private key here..."
                                        value={privateKeyInput}
                                        onChange={e => {
                                            setPrivateKeyInput(e.target.value);
                                            setSignatureStatus('idle');
                                        }}
                                    />
                                    <button
                                        type="button"
                                        onClick={() => handleVerifySignature()}
                                        disabled={isVerifying || !privateKeyInput}
                                        className="px-6 py-2 bg-blue-600 text-white font-medium rounded-md hover:bg-blue-700 disabled:opacity-50 transition-colors"
                                    >
                                        {isVerifying ? 'Verifying...' : 'Verify Signature'}
                                    </button>
                                </div>
                                {verificationError && (
                                    <p className="mt-2 text-sm text-red-600 font-medium">{verificationError}</p>
                                )}
                            </div>

                            <div className="pt-4 border-t border-gray-100">
                                <p className="text-xs text-gray-500 mb-1">Your Node Public Key:</p>
                                <code className="block p-2 bg-gray-50 rounded text-[10px] break-all text-gray-600 border border-gray-200">
                                    {keys?.publicKeyBase64 || 'Loading public key...'}
                                </code>
                            </div>
                        </div>
                    </div>
                ) : (
                    <>
                        <div className="bg-white p-8 rounded-lg shadow-sm border border-gray-200">
                            {message && (
                                <div className={`mb-6 p-4 rounded-md ${message.type === 'success' ? 'bg-green-50 text-green-800' : 'bg-red-50 text-red-800'}`}>
                                    {message.text}
                                </div>
                            )}

                            <form onSubmit={handleSubmit} className="space-y-6">
                                <div className="grid grid-cols-2 gap-6">
                                    {/* Series Search / Title */}
                                    <div className="col-span-2 relative">
                                        <label className="block text-sm font-medium text-gray-700 mb-1">Series Title</label>
                                        <input
                                            type="text"
                                            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500"
                                            value={menuTitle}
                                            onChange={e => setMenuTitle(e.target.value)}
                                            placeholder="Search for series..."
                                            required
                                            autoComplete="off"
                                        />

                                        {/* Search Results Dropdown */}
                                        {showSeriesDropdown && seriesSearchResults.length > 0 && (
                                            <div className="absolute z-10 w-full bg-white mt-1 rounded-md shadow-lg border border-gray-200 max-h-60 overflow-y-auto">
                                                {seriesSearchResults.map((series) => (
                                                    <div
                                                        key={`${series.source}-${series.externalMangaId}`}
                                                        className="px-4 py-2 hover:bg-gray-100 cursor-pointer text-sm"
                                                        onClick={() => {
                                                            isSelectionUpdate.current = true;
                                                            setForm({
                                                                ...form,
                                                                source: series.source,
                                                                externalMangaId: series.externalMangaId,
                                                                // Use formatting if needed, or just let user override displayName later
                                                            });
                                                            setMenuTitle(series.title);
                                                            setShowSeriesDropdown(false);
                                                        }}
                                                    >
                                                        <div className="font-medium text-gray-900">
                                                            {series.title}
                                                            {series.year && <span className="text-gray-500 font-normal ml-2">({series.year})</span>}
                                                        </div>
                                                        <div className="text-xs text-gray-500">
                                                            Source: {series.source === 0 ? 'MangaDex' : series.source === 1 ? 'AniList' : 'MAL'}
                                                            {series.seriesId ? ' • Registered' : ' • New'}
                                                        </div>
                                                    </div>
                                                ))}
                                            </div>
                                        )}
                                        {isSearchingSeries && (
                                            <div className="absolute right-3 top-9">
                                                <div className="animate-spin h-4 w-4 border-2 border-blue-500 rounded-full border-t-transparent"></div>
                                            </div>
                                        )}
                                    </div>

                                    <div>
                                        <label className="block text-sm font-medium text-gray-700 mb-1">Scanlator ID</label>
                                        <input
                                            type="text"
                                            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500"
                                            value={form.scanlatorId}
                                            onChange={e => setForm({ ...form, scanlatorId: e.target.value })}
                                            required
                                        />
                                    </div>

                                    <div>
                                        <label className="block text-sm font-medium text-gray-700 mb-1">Language</label>
                                        <input
                                            type="text"
                                            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500"
                                            value={form.language}
                                            onChange={e => setForm({ ...form, language: e.target.value })}
                                            required
                                        />
                                    </div>

                                    <div className="relative">
                                        <label className="block text-sm font-medium text-gray-700 mb-1">Chapter Number {batchReviewMode && '(Default / Override)'}</label>
                                        <input
                                            type="text"
                                            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:bg-gray-100"
                                            value={chapterInputValue}
                                            onChange={e => handleChapterChange(e.target.value)}
                                            placeholder="Select or type chapter number"
                                            required={!batchReviewMode}
                                            disabled={batchReviewMode}
                                            autoComplete="off"
                                        />
                                    </div>

                                    <div className="col-span-2">
                                        <label className="block text-sm font-medium text-gray-700 mb-1">Source Folder</label>

                                        {!batchReviewMode ? (
                                            <div
                                                className={`mt-1 flex justify-center px-6 pt-5 pb-6 border-2 border-dashed rounded-md transition-colors ${isDragging ? 'border-blue-500 bg-blue-50' : 'border-gray-300 hover:border-blue-400'
                                                    }`}
                                                onDragOver={onDragOver}
                                                onDragLeave={onDragLeave}
                                                onDrop={onDrop}
                                            >
                                                <div className="space-y-1 text-center">
                                                    <svg className="mx-auto h-12 w-12 text-gray-400" stroke="currentColor" fill="none" viewBox="0 0 48 48" aria-hidden="true">
                                                        <path d="M28 8H12a4 4 0 00-4 4v20m32-12v8m0 0v8a4 4 0 01-4 4H12a4 4 0 01-4-4v-4m32-4l-3.172-3.172a4 4 0 00-5.656 0L28 28M8 32l9.172-9.172a4 4 0 015.656 0L28 28m0 0l4 4m4-24h8m-4-4v8m-12 4h.02" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                                                    </svg>
                                                    <div className="flex text-sm text-gray-600">
                                                        <label className="relative cursor-pointer bg-white rounded-md font-medium text-blue-600 hover:text-blue-500 focus-within:outline-none focus-within:ring-2 focus-within:ring-offset-2 focus-within:ring-blue-500">
                                                            <span>Upload Folder</span>
                                                            <input
                                                                type="file"
                                                                className="sr-only"
                                                                // @ts-ignore - webkitdirectory is standard in modern browsers but missing in types
                                                                webkitdirectory=""
                                                                directory=""
                                                                onChange={handleFolderSelect}
                                                                multiple
                                                            />
                                                        </label>
                                                    </div>
                                                    <div className="mt-2 text-sm text-gray-500">
                                                        <span>or </span>
                                                        <label className="relative cursor-pointer bg-white rounded-md font-medium text-blue-600 hover:text-blue-500 focus-within:outline-none hover:underline">
                                                            <span>Upload Archive (Zip/CBZ)</span>
                                                            <input
                                                                type="file"
                                                                className="sr-only"
                                                                accept=".zip,.cbz,.rar,.cbr"
                                                                onChange={handleFolderSelect}
                                                                multiple
                                                            />
                                                        </label>
                                                    </div>
                                                </div>
                                            </div>
                                        ) : (
                                            <div className="bg-blue-50 border border-blue-200 rounded-md p-4">
                                                <div className="flex justify-between items-center mb-4">
                                                    <h3 className="font-medium text-blue-900">Batch Upload: {uploadBatch.length} Chapters Detected</h3>
                                                    <button
                                                        type="button"
                                                        className="text-xs text-blue-700 hover:text-blue-900 underline"
                                                        onClick={() => { setBatchReviewMode(false); setUploadBatch([]); }}
                                                    >
                                                        Cancel Batch
                                                    </button>
                                                </div>
                                                <div className="max-h-60 overflow-y-auto space-y-2">
                                                    {uploadBatch.map((item, idx) => (
                                                        <div key={idx} className="bg-white p-2 rounded border border-blue-100 flex justify-between items-center text-sm">
                                                            <span className="font-mono text-gray-600 truncate max-w-[60%]" title={item.sourcePath}>
                                                                .../{item.sourcePath.split(/[/\\]/).pop()}
                                                            </span>
                                                            <div className="flex items-center gap-2">
                                                                <span className="text-gray-500 text-xs">Ch.</span>
                                                                <input
                                                                    type="number"
                                                                    className="w-16 px-1 py-0.5 border border-gray-300 rounded text-right"
                                                                    value={item.suggestedChapterNumber}
                                                                    onChange={e => {
                                                                        const val = parseFloat(e.target.value);
                                                                        const newBatch = [...uploadBatch];
                                                                        newBatch[idx].suggestedChapterNumber = isNaN(val) ? 0 : val;
                                                                        setUploadBatch(newBatch);
                                                                    }}
                                                                />
                                                            </div>
                                                        </div>
                                                    ))}
                                                </div>
                                            </div>
                                        )}
                                    </div>
                                </div>

                                <div className="pt-4 flex items-center justify-between border-t border-gray-100">
                                    <div className="flex items-center text-xs text-green-600 font-medium">
                                        <svg className="w-4 h-4 mr-1" fill="currentColor" viewBox="0 0 20 20">
                                            <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                                        </svg>
                                        Signature Verified
                                    </div>
                                    <button
                                        type="submit"
                                        disabled={submitting || (batchReviewMode && uploadBatch.length === 0) || isUploading}
                                        className="px-8 py-2 bg-blue-600 text-white font-medium rounded-md hover:bg-blue-700 transition-colors disabled:opacity-50"
                                    >
                                        {isUploading ? 'Uploading...' : submitting ? 'Importing...' : batchReviewMode ? 'Import All Chapters' : 'Start Import'}
                                    </button>
                                </div>
                            </form>
                        </div>

                        <div>
                            <h2 className="text-xl font-bold text-gray-900 mb-4">Import History</h2>
                            <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
                                <div className="p-4 border-b border-gray-200 bg-gray-50">
                                    <input
                                        type="text"
                                        placeholder="Filter imports..."
                                        className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500 text-sm"
                                        value={search}
                                        onChange={e => setSearch(e.target.value)}
                                    />
                                </div>

                                <div className="divide-y divide-gray-200">
                                    {filteredHistory.length === 0 ? (
                                        <div className="p-8 text-center text-gray-500">
                                            No imports found matching your search.
                                        </div>
                                    ) : (
                                        filteredHistory.map((item, i) => (
                                            <div key={i} className="p-4 hover:bg-gray-50 transition-colors">
                                                <div className="flex justify-between items-start">
                                                    <div>
                                                        <h3 className="font-medium text-gray-900">{item.displayName}</h3>
                                                        <p className="text-sm text-gray-600 mt-1">
                                                            {item.seriesId} • {item.scanlatorId} • {item.language.toUpperCase()}
                                                        </p>
                                                        <p className="text-xs text-gray-400 mt-1 font-mono break-all">
                                                            {item.sourcePath}
                                                        </p>
                                                    </div>
                                                    <div className="flex flex-col items-end">
                                                        <span className={`px-2 py-0.5 text-xs rounded-full ${item.releaseType === 'manual'
                                                            ? 'bg-blue-100 text-blue-800'
                                                            : 'bg-purple-100 text-purple-800'
                                                            }`}>
                                                            {item.releaseType}
                                                        </span>
                                                    </div>
                                                </div>
                                            </div>
                                        ))
                                    )}
                                </div>
                            </div>
                        </div>
                    </>
                )}
            </div>
        </div>
    );
}
