import { NavLink, Outlet } from "react-router-dom";

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  `px-3 py-2 rounded text-sm font-medium ${
    isActive ? "bg-brand-700 text-white" : "text-slate-200 hover:bg-brand-700/40"
  }`;

export default function Layout() {
  return (
    <div className="min-h-screen flex flex-col">
      <header className="bg-brand-900 text-white">
        <div className="max-w-6xl mx-auto px-4 py-3 flex items-center gap-6">
          <h1 className="text-lg font-bold tracking-tight">Toolbox Manager</h1>
          <nav className="flex gap-1">
            <NavLink to="/" end className={navLinkClass}>
              Applications
            </NavLink>
            <NavLink to="/runs" className={navLinkClass}>
              Run history
            </NavLink>
          </nav>
        </div>
      </header>
      <main className="flex-1 max-w-6xl mx-auto px-4 py-6 w-full">
        <Outlet />
      </main>
      <footer className="text-xs text-slate-400 text-center py-3">
        Toolbox Manager · console apps via the web
      </footer>
    </div>
  );
}
