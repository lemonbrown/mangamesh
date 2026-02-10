import { useState, useEffect } from 'react';
import { getKeys, generateKeys } from '../api/keys';
import type { KeyPair } from '../types/api';

export default function Keys() {
    const [currentKey, setCurrentKey] = useState<string>('');
    const [newKeyPair, setNewKeyPair] = useState<KeyPair | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        loadKeys();
    }, []);

    async function loadKeys() {
        try {
            const keys = await getKeys();
            setCurrentKey(keys.publicKeyBase64);
        } catch (e) {
            console.error(e);
            setError('Failed to load current keys');
        }
    }

    async function handleGenerate() {
        if (!confirm("Careful! Generating a new key pair will invalidate your previous identity. Are you sure?")) {
            return;
        }

        setLoading(true);
        setError(null);
        setNewKeyPair(null);

        try {
            const keys = await generateKeys();
            setNewKeyPair(keys);
            setCurrentKey(keys.publicKeyBase64);
        } catch (e) {
            console.error(e);
            setError('Failed to generate new keys');
        } finally {
            setLoading(false);
        }
    }

    const copyToClipboard = (text: string) => {
        navigator.clipboard.writeText(text);
        // Could add a toast here
    };

    return (
        <div className="max-w-4xl mx-auto space-y-8">
            <div>
                <h1 className="text-2xl font-bold text-gray-900 mb-6">Key Management</h1>

                {error && (
                    <div className="bg-red-50 text-red-800 p-4 rounded-md mb-6">
                        {error}
                    </div>
                )}

                <div className="bg-white p-8 rounded-lg shadow-sm border border-gray-200 space-y-8">

                    {/* Current Public Key Section */}
                    <div>
                        <h2 className="text-lg font-medium text-gray-900 mb-2">Current Public Key</h2>
                        <p className="text-sm text-gray-500 mb-4">
                            This is your public identity on the network. You can share this with others.
                        </p>
                        <div className="flex items-center space-x-2">
                            <code className="flex-1 bg-gray-50 p-3 rounded border border-gray-200 font-mono text-sm break-all">
                                {currentKey || 'Loading...'}
                            </code>
                            <button
                                onClick={() => copyToClipboard(currentKey)}
                                className="p-2 text-gray-500 hover:text-blue-600 hover:bg-blue-50 rounded-md transition-colors"
                                title="Copy Public Key"
                            >
                                <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                                    <path d="M8 3a1 1 0 011-1h2a1 1 0 110 2H9a1 1 0 01-1-1z" />
                                    <path d="M6 3a2 2 0 00-2 2v11a2 2 0 002 2h8a2 2 0 002-2V5a2 2 0 00-2-2 3 3 0 01-3 3H9a3 3 0 01-3-3z" />
                                </svg>
                            </button>
                        </div>
                    </div>

                    <div className="border-t border-gray-100 my-6"></div>

                    {/* New Key Section (Ephemeral) */}
                    {newKeyPair && newKeyPair.privateKeyBase64 && (
                        <div className="bg-green-50 border border-green-200 rounded-md p-6 animate-fade-in">
                            <div className="flex items-start space-x-3">
                                <div className="text-green-600 mt-0.5">
                                    <svg xmlns="http://www.w3.org/2000/svg" className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                                    </svg>
                                </div>
                                <div className="flex-1">
                                    <h3 className="text-green-800 font-medium text-lg">New Key Pair Generated!</h3>
                                    <p className="text-green-700 text-sm mt-1 mb-4">
                                        Your Private Key is shown below. <strong>You must save this now.</strong> It will not be shown again if you refresh or leave this page.
                                    </p>

                                    <div className="space-y-4">
                                        <div>
                                            <label className="block text-xs font-semibold text-green-800 uppercase tracking-wide mb-1">Private Key (SECRET)</label>
                                            <div className="flex items-center space-x-2">
                                                <code className="flex-1 bg-white p-3 rounded border border-green-200 font-mono text-sm break-all text-red-600 select-all">
                                                    {newKeyPair.privateKeyBase64}
                                                </code>
                                                <button
                                                    onClick={() => copyToClipboard(newKeyPair.privateKeyBase64!)}
                                                    className="p-2 text-green-700 hover:text-green-900 hover:bg-green-100 rounded-md transition-colors"
                                                    title="Copy Private Key"
                                                >
                                                    <svg xmlns="http://www.w3.org/2000/svg" className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                                                        <path d="M8 3a1 1 0 011-1h2a1 1 0 110 2H9a1 1 0 01-1-1z" />
                                                        <path d="M6 3a2 2 0 00-2 2v11a2 2 0 002 2h8a2 2 0 002-2V5a2 2 0 00-2-2 3 3 0 01-3 3H9a3 3 0 01-3-3z" />
                                                    </svg>
                                                </button>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    )}

                    <div className="border-t border-gray-100 my-6"></div>

                    {/* Generate Action */}
                    <div>
                        <h2 className="text-lg font-medium text-gray-900 mb-2">Generate New Keys</h2>
                        <p className="text-sm text-gray-500 mb-4">
                            If you have lost your private key or wish to rotate your identity, you can generate a new pair.
                        </p>
                        <button
                            onClick={handleGenerate}
                            disabled={loading}
                            className="bg-white border border-gray-300 text-gray-700 font-medium py-2 px-4 rounded-md hover:bg-gray-50 hover:text-red-600 hover:border-red-300 transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-red-500 disabled:opacity-50"
                        >
                            {loading ? 'Generating...' : 'Generate New Key Pair'}
                        </button>
                    </div>

                </div>
            </div>
        </div>
    );
}
