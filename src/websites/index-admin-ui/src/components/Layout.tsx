import { Outlet, NavLink, useNavigate } from 'react-router-dom';
import { LayoutDashboard, FileText, Key, Activity, Settings, LogOut, Library } from 'lucide-react';
import clsx from 'clsx';
import { useState } from 'react';

export default function Layout() {
    const navigate = useNavigate();
    const [isServiceRunning, setIsServiceRunning] = useState(true);

    const handleLogout = () => {
        // Clear auth token (mock)
        localStorage.removeItem('isAdminAuthenticated');
        navigate('/login');
    };

    return (
        <div className="flex h-screen bg-gray-100 font-sans">
            {/* Sidebar */}
            <aside className="w-64 bg-slate-900 text-white flex flex-col">
                <div className="p-6 border-b border-slate-800">
                    <h1 className="text-xl font-bold tracking-tight text-blue-400">Index Admin</h1>
                    <div className="mt-2 flex items-center text-xs text-slate-400">
                        <div className={clsx("w-2 h-2 rounded-full mr-2", isServiceRunning ? "bg-green-500 shadow-[0_0_5px_rgba(34,197,94,0.5)]" : "bg-red-500")} />
                        Service {isServiceRunning ? 'Running' : 'Stopped'}
                    </div>
                </div>

                <nav className="flex-1 p-4 space-y-1">
                    <NavItem to="/" icon={<LayoutDashboard size={20} />} label="Dashboard" />
                    <NavItem to="/manifests" icon={<FileText size={20} />} label="Manifests" />
                    <NavItem to="/series" icon={<Library size={20} />} label="Series" />
                    <NavItem to="/keys" icon={<Key size={20} />} label="Public Keys" />
                    <NavItem to="/logs" icon={<Activity size={20} />} label="Logs" />
                    <NavItem to="/settings" icon={<Settings size={20} />} label="Settings" />
                </nav>

                <div className="p-4 border-t border-slate-800">
                    <button
                        onClick={handleLogout}
                        className="flex items-center w-full px-4 py-2 text-sm text-slate-400 hover:text-white hover:bg-slate-800 rounded-md transition-colors"
                    >
                        <LogOut size={18} className="mr-3" />
                        Sign Out
                    </button>
                </div>
            </aside>

            {/* Main Content */}
            <main className="flex-1 overflow-auto">
                <header className="bg-white border-b border-gray-200 h-16 flex items-center justify-between px-8">
                    <h2 className="text-lg font-medium text-gray-800">Overview</h2>
                    <div className="flex items-center space-x-4">
                        <button
                            className={clsx(
                                "px-3 py-1.5 text-sm font-medium rounded-md transition-colors",
                                isServiceRunning ? "bg-red-50 text-red-700 hover:bg-red-100 border border-red-200" : "bg-green-50 text-green-700 hover:bg-green-100 border border-green-200"
                            )}
                            onClick={() => setIsServiceRunning(!isServiceRunning)}
                        >
                            {isServiceRunning ? 'Stop Service' : 'Start Service'}
                        </button>
                    </div>
                </header>
                <div className="p-8">
                    <Outlet />
                </div>
            </main>
        </div>
    );
}

function NavItem({ to, icon, label }: { to: string; icon: React.ReactNode; label: string }) {
    return (
        <NavLink
            to={to}
            className={({ isActive }) => clsx(
                "flex items-center px-4 py-3 text-sm font-medium rounded-md transition-colors",
                isActive
                    ? "bg-blue-600 text-white shadow-md shadow-blue-900/20"
                    : "text-slate-400 hover:bg-slate-800 hover:text-white"
            )}
        >
            {icon}
            <span className="ml-3">{label}</span>
        </NavLink>
    );
}
