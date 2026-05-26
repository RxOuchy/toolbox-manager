import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { api } from "../api/client";
import type { RunStatus } from "../api/types";
import StatusBadge from "../components/StatusBadge";

const STATUSES: RunStatus[] = ["Queued", "Running", "Succeeded", "Failed", "Cancelled", "TimedOut"];

export default function RunHistory() {
  const [status, setStatus] = useState<RunStatus | "">("");

  const runs = useQuery({
    queryKey: ["runs", { status }],
    queryFn: () => api.listRuns({ status: status === "" ? undefined : (status as RunStatus), limit: 200 }),
    refetchInterval: 5_000,
  });

  return (
    <div>
      <header className="flex items-center justify-between mb-4">
        <h2 className="text-xl font-semibold">Run history</h2>
        <select
          className="field max-w-xs"
          value={status}
          onChange={(e) => setStatus(e.target.value as RunStatus | "")}
        >
          <option value="">All statuses</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
      </header>

      {runs.isLoading && <p className="text-slate-500">Loading…</p>}
      {runs.isError && (
        <p className="text-red-700 bg-red-50 border border-red-200 rounded p-3">
          {(runs.error as Error).message}
        </p>
      )}

      {runs.data && (
        <table className="w-full text-sm bg-white border border-slate-200 rounded overflow-hidden">
          <thead className="bg-slate-50 text-slate-600 text-left">
            <tr>
              <th className="px-3 py-2">Queued</th>
              <th className="px-3 py-2">Application</th>
              <th className="px-3 py-2">Status</th>
              <th className="px-3 py-2">Exit</th>
              <th className="px-3 py-2">Requested by</th>
            </tr>
          </thead>
          <tbody>
            {runs.data.map((r) => (
              <tr key={r.id} className="border-t border-slate-100 hover:bg-slate-50">
                <td className="px-3 py-2">
                  <Link to={`/runs/${r.id}`} className="text-brand-700 hover:underline">
                    {new Date(r.queuedAt).toLocaleString()}
                  </Link>
                </td>
                <td className="px-3 py-2">
                  <Link to={`/applications/${r.applicationId}`} className="text-slate-700 hover:underline">
                    {r.applicationName}
                  </Link>
                </td>
                <td className="px-3 py-2"><StatusBadge status={r.status} /></td>
                <td className="px-3 py-2 font-mono text-xs">{r.exitCode ?? "—"}</td>
                <td className="px-3 py-2">{r.requestedBy}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {runs.data && runs.data.length === 0 && (
        <p className="text-sm text-slate-500 mt-4">No runs match the current filter.</p>
      )}
    </div>
  );
}
