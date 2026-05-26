import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api } from "../api/client";
import { useState } from "react";

export default function ApplicationsList() {
  const [includeInactive, setIncludeInactive] = useState(false);
  const apps = useQuery({
    queryKey: ["applications", { includeInactive }],
    queryFn: () => api.listApplications(includeInactive),
  });

  return (
    <div>
      <header className="flex items-center justify-between mb-5">
        <div>
          <h2 className="text-xl font-semibold">Applications</h2>
          <p className="text-sm text-slate-500">
            Registered console apps available for execution.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <label className="text-sm flex items-center gap-2">
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(e) => setIncludeInactive(e.target.checked)}
            />
            Show inactive
          </label>
          <Link to="/applications/new" className="btn-primary">
            + New application
          </Link>
        </div>
      </header>

      {apps.isLoading && <p className="text-slate-500">Loading…</p>}
      {apps.isError && (
        <p className="text-red-700 bg-red-50 border border-red-200 rounded p-3">
          {(apps.error as Error).message}
        </p>
      )}

      {apps.data && apps.data.length === 0 && (
        <div className="bg-white border border-slate-200 rounded p-10 text-center">
          <p className="text-slate-500">No applications registered yet.</p>
          <Link to="/applications/new" className="btn-primary mt-3">
            Register your first application
          </Link>
        </div>
      )}

      {apps.data && apps.data.length > 0 && (
        <div className="bg-white border border-slate-200 rounded overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-slate-600 text-left">
              <tr>
                <th className="px-4 py-2">Name</th>
                <th className="px-4 py-2">Executable</th>
                <th className="px-4 py-2">Parameters</th>
                <th className="px-4 py-2">Timeout</th>
                <th className="px-4 py-2">Status</th>
              </tr>
            </thead>
            <tbody>
              {apps.data.map((app) => (
                <tr key={app.id} className="border-t border-slate-100 hover:bg-slate-50">
                  <td className="px-4 py-3">
                    <Link to={`/applications/${app.id}`} className="text-brand-700 hover:underline font-medium">
                      {app.name}
                    </Link>
                    {app.description && (
                      <p className="text-xs text-slate-500 mt-0.5">{app.description}</p>
                    )}
                  </td>
                  <td className="px-4 py-3 font-mono text-xs text-slate-600">{app.executablePath}</td>
                  <td className="px-4 py-3">{app.parameterCount}</td>
                  <td className="px-4 py-3">{app.timeoutSeconds}s</td>
                  <td className="px-4 py-3">
                    {app.isActive ? (
                      <span className="badge bg-green-100 text-green-800">Active</span>
                    ) : (
                      <span className="badge bg-slate-200 text-slate-600">Inactive</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
