import Nav from './Nav';
import { Outlet } from 'react-router-dom';

export default function Layout() {
    return (
        <div className="min-h-screen bg-gray-50 font-sans text-gray-900">
            <Nav />
            <main className="max-w-7xl mx-auto py-8 px-6">
                <Outlet />
            </main>
        </div>
    );
}
