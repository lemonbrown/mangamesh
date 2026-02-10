import { useState, useEffect } from 'react';
import { Key, Plus, Shield, AlertTriangle } from 'lucide-react';
import clsx from 'clsx';
import { api } from '../services/api';
import type { PublicKey } from '../services/api';

export default function PublicKeys() {
    const [keys, setKeys] = useState<PublicKey[]>([]);
    const [isAdding, setIsAdding] = useState(false);
    const [newName, setNewName] = useState('');
    const [newKey, setNewKey] = useState('');

    const fetchKeys = async () => {
        try {
            const data = await api.getPublicKeys();
            setKeys(data);
        } catch (error) {
            console.error("Failed to fetch public keys:", error);
        }
    };

    useEffect(() => {
        fetchKeys();
    }, []);

    const handleAddKey = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await api.registerPublicKey({ publicKeyBase64: newKey, comment: newName });
            await fetchKeys();
            setIsAdding(false);
            setNewName('');
            setNewKey('');
        } catch (error) {
            console.error("Failed to register key:", error);
            alert("Failed to register key. Check console for details.");
        }
    };

    const toggleRevoke = async (id: string, currentStatus: string) => {
        try {
            if (currentStatus === 'Active') {
                await api.revokePublicKey(id);
            } else {
                await api.reactivatePublicKey(id);
            }
            await fetchKeys();
        } catch (error) {
            console.error("Failed to toggle key status:", error);
        }
    };

    return (
        <div className="space-y-6">
            <div className="flex justify-between items-center">
                <div>
                    <h1 className="text-2xl font-bold text-gray-900">Public Key Registry</h1>
                    <p className="text-gray-500 mt-1">Manage keys authorized to sign and import manifests.</p>
                </div>
                <button
                    onClick={() => setIsAdding(true)}
                    className="px-4 py-2 bg-blue-600 text-white rounded-lg font-medium hover:bg-blue-700 transition-colors flex items-center"
                >
                    <Plus className="w-4 h-4 mr-2" />
                    Register Key
                </button>
            </div>

            {isAdding && (
                <div className="bg-white rounded-xl shadow-sm border border-blue-200 p-6 animate-in slide-in-from-top-4 fade-in">
                    <h3 className="text-lg font-medium text-gray-900 mb-4">Register New Public Key</h3>
                    <form onSubmit={handleAddKey} className="space-y-4">
                        <div>
                            <label className="block text-sm font-medium text-gray-700 mb-1">Owner Name / Description</label>
                            <input
                                type="text"
                                value={newName}
                                onChange={(e) => setNewName(e.target.value)}
                                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none"
                                placeholder="e.g., ScanGroup Official Key"
                                required
                            />
                        </div>
                        <div>
                            <label className="block text-sm font-medium text-gray-700 mb-1">Public Key (Base64)</label>
                            <textarea
                                value={newKey}
                                onChange={(e) => setNewKey(e.target.value)}
                                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none font-mono text-sm"
                                rows={3}
                                placeholder="Paste Base64 encoded public key here..."
                                required
                            />
                        </div>
                        <div className="flex justify-end space-x-3">
                            <button
                                type="button"
                                onClick={() => setIsAdding(false)}
                                className="px-4 py-2 text-gray-700 hover:text-gray-900 font-medium"
                            >
                                Cancel
                            </button>
                            <button
                                type="submit"
                                className="px-4 py-2 bg-blue-600 text-white rounded-lg font-medium hover:bg-blue-700"
                            >
                                Register
                            </button>
                        </div>
                    </form>
                </div>
            )}

            <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
                <table className="min-w-full divide-y divide-gray-200">
                    <thead className="bg-gray-50">
                        <tr>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Owner</th>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Key Preview</th>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Registered</th>
                            <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Actions</th>
                        </tr>
                    </thead>
                    <tbody className="bg-white divide-y divide-gray-200">
                        {keys.map((key) => (
                            <tr key={key.id} className={clsx("hover:bg-gray-50", key.status === 'Revoked' && "bg-gray-50")}>
                                <td className="px-6 py-4 whitespace-nowrap">
                                    <div className="flex items-center">
                                        <div className={clsx("p-2 rounded-lg mr-3", key.status === 'Active' ? "bg-green-100" : "bg-gray-200")}>
                                            <Key className={clsx("w-4 h-4", key.status === 'Active' ? "text-green-700" : "text-gray-500")} />
                                        </div>
                                        <div>
                                            <div className="text-sm font-medium text-gray-900">{key.name}</div>
                                            <div className="text-xs text-gray-500">ID: {key.id}</div>
                                        </div>
                                    </div>
                                </td>
                                <td className="px-6 py-4 whitespace-nowrap text-sm font-mono text-gray-500">
                                    {key.keyPreview}
                                </td>
                                <td className="px-6 py-4 whitespace-nowrap">
                                    <span className={clsx(
                                        "inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium",
                                        key.status === 'Active' ? "bg-green-100 text-green-800" : "bg-red-100 text-red-800"
                                    )}>
                                        {key.status === 'Active' ? <Shield className="w-3 h-3 mr-1" /> : <AlertTriangle className="w-3 h-3 mr-1" />}
                                        {key.status}
                                    </span>
                                </td>
                                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                                    {new Date(key.addedAt).toLocaleDateString()}
                                </td>
                                <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium">
                                    <button
                                        onClick={() => toggleRevoke(key.id, key.status)}
                                        className={clsx(
                                            "text-sm font-medium px-3 py-1 rounded transition-colors",
                                            key.status === 'Active'
                                                ? "text-red-600 hover:bg-red-50 hover:text-red-900"
                                                : "text-green-600 hover:bg-green-50 hover:text-green-900"
                                        )}
                                    >
                                        {key.status === 'Active' ? 'Revoke' : 'Reactivate'}
                                    </button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
