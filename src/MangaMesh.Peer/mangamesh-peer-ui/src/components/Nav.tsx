import { Link, useLocation } from 'react-router-dom';
import classNames from 'classnames';

import NodeStatusIndicator from './NodeStatusIndicator';

const NavLink = ({ to, children }: { to: string; children: React.ReactNode }) => {
    const location = useLocation();
    const isActive = location.pathname === to;

    return (
        <Link
            to={to}
            className={classNames(
                'px-4 py-2 rounded-md text-sm font-medium transition-colors',
                {
                    'bg-gray-200 text-gray-900': isActive,
                    'text-gray-600 hover:bg-gray-100 hover:text-gray-900': !isActive,
                }
            )}
        >
            {children}
        </Link>
    );
};

export default function Nav() {
    return (
        <nav className="bg-white border-b border-gray-200 px-6 py-3 flex items-center justify-between">
            <div className="flex items-center space-x-8">
                <div className="text-xl font-bold text-gray-800 tracking-tight">
                    MangaMesh Peer Manager
                </div>
                <div className="flex space-x-2">
                    {/* <NavLink to="/">Dashboard</NavLink>
                    <NavLink to="/subscriptions">Subscriptions</NavLink>
                    <NavLink to="/series">Series</NavLink> */}
                    <NavLink to="/import">Import</NavLink>
                    <NavLink to="/storage">Storage</NavLink>
                    <NavLink to="/keys">Keys</NavLink>
                    <NavLink to="/nodes">Nodes</NavLink>
                    <NavLink to="/logs">Logs</NavLink>
                </div>
            </div>
            <div>
                <NodeStatusIndicator />
            </div>
        </nav>
    );
}
