import { useParams, Link, useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { api } from "../api/client";
import RunModal from "../components/RunModal";
import StatusBadge from "../components/StatusBadge";

export default function ApplicationDetail() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [runOpen, setRunOpen] = useState(false);

  const appQuery = useQuery({
    queryKey: ["application", id],
    queryFn: () => api.getApplication(id),
    enabled: !!id,
  });

  const runsQuery = useQuery({
    queryKey: ["runs", { applicationId: id }],
    queryFn: () => api.listRuns({ applicationId: id, limit: 20 }),
    enabled: !!id,
    refetchInterval: 5_000,
  });

  const deleteMut = useMutation({
    mutationFn: () => api.deleteApplication(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["applications"] });
      navigate("/");
    },
  });

  if (appQuery.isLoading) return <p className="text-slate-500">Loading…</p>;
  if (appQuery.isError) return <p className="text-red-700">{(appQuery.error as Error).message}</p>;
  if (!appQuery.data) return null;

  const app = appQuery.data;

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between">
        <div>
          <h2 className="text-xl font-semibold">{app.name}</h2>
          {app.description && <p className="text-sm text-slate-500 mt-1">{app.description}</p>}
          <dl className="mt-3 text-xs text-slate-500 space-y-0.5">
            <div><dt className="inline font-semibold">Executable:</dt> <code className="font-mono">{app.executablePath}</code></div>
            {app.workingDirectory && <div><dt className="inline font-semibold">Working dir:</dt> <code className="font-mono">{app.workingDirectory}</code></div>}
            <div><dt className="inline font-semibold">Timeout:</dt> {app.timeoutSeconds}s</div>
          </dl>
        </div>
        <div className="flex gap-2">
          <button className="btn-primary" disabled={!app.isActive} onClick={() => setRunOpen(true)}>
            ▶ Run
          </button>
          <Link to={`/applications/${app.id}/edit`} className="btn-secondary">Edit</Link>
          <button
            className="btn-danger"
            onClick={() => {
              if (confirm(`Deactivate '${app.name}'?`)) deleteMut.mutate();
            }}
            disabled={deleteMut.isPending}
          >
            Deactivate
          </button>
        </div>
      </header>

      <section>
        <h3 className="font-semibold text-sm text-slate-700 mb-2">Parameters</h3>
        {app.parameters.length === 0 ? (
          <p className="text-sm text-slate-500">No parameters declared.</p>
        ) : (
          <table className="w-full text-sm bg-white border border-slate-200 rounded overflow-hidden">
            <thead className="bg-slate-50 text-slate-600 text-left">
              <tr>
                <th className="px-3 py-2">Order</th>
                <th className="px-3 py-2">Name</th>
                <th className="px-3 py-2">Label</th>
                <th className="px-3 py-2">Type</th>
                <th className="px-3 py-2">Required</th>
                <th className="px-3 py-2">Default</th>
              </tr>
            </thead>
            <tbody>
              {app.parameters
                .slice()
                .sort((a, b) => a.displayOrder - b.displayOrder)
                .map((p) => (
                  <tr key={p.id} className="border-t border-slate-100">
                    <td className="px-3 py-2">{p.displayOrder}</td>
                    <td className="px-3 py-2 font-mono text-xs">{p.parameterName}</td>
                    <td className="px-3 py-2">{p.displayLabel}</td>
                    <td className="px-3 py-2">{p.parameterType}</td>
                    <td className="px-3 py-2">{p.isRequired ? "Yes" : "No"}</td>
                    <td className="px-3 py-2 font-mono text-xs">{p.defaultValue ?? "—"}</td>
                  </tr>
                ))}
            </tbody>
          </table>
        )}
      </section>

      <section>
        <h3 className="font-semibold text-sm text-slate-700 mb-2">Recent runs</h3>
        {runsQuery.data && runsQuery.data.length === 0 && (
          <p className="text-sm text-slate-500">No runs yet.</p>
        )}
        {runsQuery.data && runsQuery.data.length > 0 && (
          <table className="w-full text-sm bg-white border border-slate-200 rounded overflow-hidden">
            <thead className="bg-slate-50 text-slate-600 text-left">
              <tr>
                <th className="px-3 py-2">Queued</th>
                <th className="px-3 py-2">Status</th>
                <th className="px-3 py-2">Exit</th>
                <th className="px-3 py-2">Requested by</th>
                <th className="px-3 py-2">Duration</th>
              </tr>
            </thead>
            <tbody>
              {runsQuery.data.map((r) => (
                <tr key={r.id} className="border-t border-slate-100 hover:bg-slate-50">
                  <td className="px-3 py-2">
                    <Link to={`/runs/${r.id}`} className="text-brand-700 hover:underline">
                      {new Date(r.queuedAt).toLocaleString()}
                    </Link>
                  </td>
                  <td className="px-3 py-2"><StatusBadge status={r.status} /></td>
                  <td className="px-3 py-2 font-mono text-xs">{r.exitCode ?? "—"}</td>
                  <td className="px-3 py-2">{r.requestedBy}</td>
                  <td className="px-3 py-2">{duration(r.startedAt, r.completedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      {runOpen && <RunModal application={app} onClose={() => setRunOpen(false)} />}
    </div>
  );
}

function duration(start: string | null, end: string | null) {
  if (!start || !end) return "—";
  const ms = new Date(end).getTime() - new Date(start).getTime();
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}
