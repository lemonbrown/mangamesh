import { Save } from 'lucide-react';

export default function Settings() {
    return (
        <div className="max-w-2xl space-y-8">
            <h1 className="text-2xl font-bold text-gray-900">Settings</h1>

            <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                <h3 className="text-lg font-medium text-gray-900 mb-4">General Configuration</h3>
                <div className="space-y-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">Index Service Name</label>
                        <input type="text" defaultValue="MangaMesh Official Index" className="w-full px-4 py-2 border border-gray-300 rounded-lg outline-none focus:ring-2 focus:ring-blue-500" />
                    </div>
                    <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">Public Announcement URL</label>
                        <input type="text" defaultValue="https://index.mangamesh.org" className="w-full px-4 py-2 border border-gray-300 rounded-lg outline-none focus:ring-2 focus:ring-blue-500" />
                    </div>
                </div>
            </div>

            <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
                <h3 className="text-lg font-medium text-gray-900 mb-4">Security</h3>
                <div className="space-y-4">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="font-medium text-gray-900">Require MFA for Admin</p>
                            <p className="text-sm text-gray-500">Enforce multi-factor authentication for all admin logins.</p>
                        </div>
                        <input type="checkbox" defaultChecked className="w-5 h-5 text-blue-600 rounded focus:ring-blue-500" />
                    </div>
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="font-medium text-gray-900">Allow New Peer Registration</p>
                            <p className="text-sm text-gray-500">If disabled, only manually added peers can connect.</p>
                        </div>
                        <input type="checkbox" defaultChecked className="w-5 h-5 text-blue-600 rounded focus:ring-blue-500" />
                    </div>
                </div>
            </div>

            <button className="flex items-center justify-center px-6 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 font-medium">
                <Save className="w-4 h-4 mr-2" />
                Save Changes
            </button>
        </div>
    );
}
