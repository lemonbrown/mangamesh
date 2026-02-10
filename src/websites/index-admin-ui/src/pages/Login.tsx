import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ShieldCheck, ArrowRight, Lock } from 'lucide-react';

export default function Login() {
    const navigate = useNavigate();
    const [step, setStep] = useState<'credentials' | 'mfa'>('credentials');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [mfaCode, setMfaCode] = useState('');
    const [error, setError] = useState('');

    const handleCredentialsSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        setError('');
        if (email === 'admin' && password === 'admin') {
            setStep('mfa');
        } else {
            setError('Invalid credentials');
        }
    };

    const handleMfaSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        setError('');
        // Accept any 6 digit code for fake MFA
        if (mfaCode.length === 6) {
            localStorage.setItem('isAdminAuthenticated', 'true');
            navigate('/');
        } else {
            setError('Invalid MFA code');
        }
    };

    return (
        <div className="min-h-screen bg-slate-900 flex items-center justify-center p-4">
            <div className="w-full max-w-md bg-white rounded-2xl shadow-xl overflow-hidden">
                <div className="bg-slate-800 p-8 text-center border-b border-slate-700">
                    <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-blue-500/10 mb-4">
                        <ShieldCheck className="w-8 h-8 text-blue-400" />
                    </div>
                    <h1 className="text-2xl font-bold text-white">Index Admin Portal</h1>
                    <p className="text-slate-400 mt-2 text-sm">Secure Access Required</p>
                </div>

                <div className="p-8">
                    {step === 'credentials' ? (
                        <form onSubmit={handleCredentialsSubmit} className="space-y-6">
                            <div>
                                <label className="block text-sm font-medium text-gray-700 mb-1">Username</label>
                                <input
                                    type="text"
                                    value={email}
                                    onChange={(e) => setEmail(e.target.value)}
                                    className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none transition-all"
                                    placeholder="admin"
                                    required
                                />
                            </div>
                            <div>
                                <label className="block text-sm font-medium text-gray-700 mb-1">Password</label>
                                <input
                                    type="password"
                                    value={password}
                                    onChange={(e) => setPassword(e.target.value)}
                                    className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none transition-all"
                                    placeholder="••••••••"
                                    required
                                />
                            </div>

                            {error && <div className="text-red-500 text-sm">{error}</div>}

                            <button
                                type="submit"
                                className="w-full bg-slate-900 text-white py-2.5 rounded-lg font-medium hover:bg-slate-800 transition-colors flex items-center justify-center group"
                            >
                                Continue
                                <ArrowRight className="ml-2 w-4 h-4 group-hover:translate-x-1 transition-transform" />
                            </button>
                        </form>
                    ) : (
                        <form onSubmit={handleMfaSubmit} className="space-y-6 animate-in slide-in-from-right-8 fade-in duration-300">
                            <div className="text-center mb-6">
                                <div className="inline-flex items-center justify-center w-12 h-12 rounded-full bg-blue-50 mb-4">
                                    <Lock className="w-6 h-6 text-blue-600" />
                                </div>
                                <h2 className="text-lg font-semibold text-gray-900">MFA Verification</h2>
                                <p className="text-sm text-gray-500 mt-1">Enter the 6-digit code from your authenticator app.</p>
                            </div>

                            <div>
                                <label className="block text-sm font-medium text-gray-700 mb-1">Authentication Code</label>
                                <input
                                    type="text"
                                    value={mfaCode}
                                    onChange={(e) => setMfaCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                                    className="w-full px-4 py-3 text-center text-2xl tracking-widest border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none transition-all font-mono"
                                    placeholder="000000"
                                    required
                                />
                                <p className="text-xs text-gray-500 mt-2 text-center">Use any 6 digits (Fake MFA)</p>
                            </div>

                            {error && <div className="text-red-500 text-sm text-center">{error}</div>}

                            <button
                                type="submit"
                                className="w-full bg-blue-600 text-white py-2.5 rounded-lg font-medium hover:bg-blue-700 transition-colors shadow-lg shadow-blue-500/30"
                            >
                                Verify & Login
                            </button>

                            <button
                                type="button"
                                onClick={() => setStep('credentials')}
                                className="w-full text-sm text-gray-500 hover:text-gray-700"
                            >
                                Back to Login
                            </button>
                        </form>
                    )}
                </div>

                <div className="bg-gray-50 p-4 border-t border-gray-100 text-center text-xs text-gray-400">
                    &copy; 2026 MangaMesh Index Service
                </div>
            </div>
        </div>
    );
}
